"""Bulk historical sales importer (asyncio + aiohttp).

Pipeline:
    URL queue -> N fetcher tasks -> response queue -> M poster tasks.

Two aiohttp sessions: one for Universalis (rate-limited via a token bucket
sized from config.MAX_REQUESTS_PER_SECOND), one for the backend POSTs. A
watcher task writes a status snapshot once per second.
"""
from __future__ import annotations

import asyncio
import datetime
import math
import os
import time
from typing import Iterable

import aiohttp

import config
import external
from common import log
from common import world_data
from metrics import METRICS


###########################
#       URL BUILDER       #
###########################
def build_url_list() -> list[str]:
    item_ids = world_data.marketable_item_id_list()
    worlds: list[str] = []
    for region in config.REGIONS_TO_IMPORT:
        worlds.extend(world_data.world_names_for_region(region))

    if config.IMPORT_ALL_TIME:
        within_ms = str(math.floor(time.time() * 1000))
    else:
        within_ms = str(config.TIME_AGO_TO_IMPORT_SALES)
    entries = str(config.ENTRIES_TO_RETURN)

    chunk = config.ITEMS_PER_REQUEST
    urls: list[str] = []
    for world in worlds:
        for i in range(0, len(item_ids), chunk):
            ids = ",".join(str(x) for x in item_ids[i : i + chunk])
            urls.append(
                f"{config.UNIVERSALIS_URL}{config.UNIVERSALIS_SALES_ENDPOINT}"
                f"{world}/{ids}?entriesToReturn={entries}"
                f"&statsWithin={within_ms}&entriesWithin={within_ms}"
            )
    return urls


###########################
#       PIPELINE          #
###########################
class Pipeline:
    """Holds queues and a single in-flight counter so termination is
    detectable. Single-threaded asyncio = no locks required."""

    def __init__(self) -> None:
        self.url_queue: asyncio.Queue[str] = asyncio.Queue()
        self.response_queue: asyncio.Queue[dict] = asyncio.Queue(
            maxsize=config.RESPONSE_QUEUE_LIMIT
        )
        self.failed_urls: asyncio.Queue[str] = asyncio.Queue()
        self.in_flight: int = 0
        self.done = asyncio.Event()


async def fetcher(
    pipeline: Pipeline,
    http: aiohttp.ClientSession,
    bucket: external.TokenBucket,
) -> None:
    while True:
        url = await pipeline.url_queue.get()
        pipeline.in_flight += 1
        try:
            text = await external.fetch_one(http, bucket, url)
        except Exception as e:
            log.error(f"fetcher crashed on {url}: {e!r}", exc_info=True)
            text = None

        if text is None:
            await pipeline.failed_urls.put(url)
        else:
            await pipeline.response_queue.put({"url": url, "json": text})

        pipeline.in_flight -= 1
        pipeline.url_queue.task_done()


async def poster(
    pipeline: Pipeline,
    http: aiohttp.ClientSession,
) -> None:
    while True:
        item = await pipeline.response_queue.get()
        pipeline.in_flight += 1
        ok = False
        try:
            ok = await external.post_response(http, item)
        except Exception as e:
            log.error(f"poster crashed on {item.get('url')}: {e!r}", exc_info=True)

        if not ok:
            await pipeline.failed_urls.put(item["url"])

        pipeline.in_flight -= 1
        pipeline.response_queue.task_done()


###########################
#         WATCHER         #
###########################
async def watcher(pipeline: Pipeline) -> None:
    if not os.path.exists(config.STATUS_FILE_PATH):
        open(config.STATUS_FILE_PATH, "w").close()
    os.chmod(config.STATUS_FILE_PATH, 0o666)

    try:
        while not pipeline.done.is_set():
            try:
                _write_status(pipeline)
            except Exception as e:
                log.error(f"watcher write failed: {e!r}")
            await asyncio.sleep(1)
    finally:
        try:
            _write_status(pipeline)
        except Exception:
            pass
        print("IMPORT FINISHED - WATCHER EXITING")


def _write_status(pipeline: Pipeline) -> None:
    failed = pipeline.failed_urls.qsize()
    url_size = pipeline.url_queue.qsize()
    resp_size = pipeline.response_queue.qsize()

    eta = "N/A"
    try:
        if METRICS.total_sales_parsed > 0 and METRICS.php_requests_completed > 0:
            elapsed = time.time() - METRICS.start_time
            time_per_sale = elapsed / METRICS.total_sales_parsed
            avg_sales_per_req = METRICS.total_sales_parsed / METRICS.php_requests_completed
            remaining = METRICS.total_requests - METRICS.php_requests_completed
            eta_seconds = time_per_sale * avg_sales_per_req * remaining
            eta = str(datetime.timedelta(seconds=eta_seconds))
    except Exception:
        pass

    lines = [
        f"Queue size: {url_size} ({config.ITEMS_PER_REQUEST} items per request)",
        f"PHP request queue size: {resp_size}",
        f"PHP requests completed: {METRICS.php_requests_completed} / {METRICS.total_requests}",
        f"Sales parsed: {METRICS.total_sales_parsed}",
        f"Requests retried: {METRICS.retried_requests}",
        f"Failed (awaiting retry): {failed}",
        f"In flight: {pipeline.in_flight}",
        f"ETA: {eta}",
        "",
        "------ QUEUE MONITOR ------",
        "",
        "Last response sent to PHP:",
        str(METRICS.last_response)[:500],
    ]
    with open(config.STATUS_FILE_PATH, "w") as fh:
        fh.write("\n".join(lines))


###########################
#         DRIVER          #
###########################
async def _drive(pipeline: Pipeline, retry_delay: float = 0.25) -> None:
    """Drain failed URLs back into the URL queue and detect completion."""
    while True:
        # Re-queue any failures.
        while not pipeline.failed_urls.empty():
            try:
                url = pipeline.failed_urls.get_nowait()
            except asyncio.QueueEmpty:
                break
            await pipeline.url_queue.put(url)
            METRICS.inc("retried_requests")

        if (
            pipeline.url_queue.empty()
            and pipeline.response_queue.empty()
            and pipeline.failed_urls.empty()
            and pipeline.in_flight == 0
        ):
            return

        await asyncio.sleep(retry_delay)


def _enabled_log_channels(map_: dict[str, bool]) -> list[str]:
    return [k.lower() for k, v in map_.items() if v]


###########################
#          MAIN           #
###########################
async def amain() -> None:
    log.setup(
        logs_dir=config.LOGS_DIR,
        enabled_channels=_enabled_log_channels(config.PRINT_TO_LOG) + ["panic"],
        print_channels=_enabled_log_channels(config.PRINT_TO_SCREEN),
    )

    urls = build_url_list()
    pipeline = Pipeline()
    for u in urls:
        pipeline.url_queue.put_nowait(u)
    METRICS.set("total_requests", len(urls))
    log.debug(f"queued {len(urls)} url batches for import")

    bucket = external.TokenBucket(
        rate=config.MAX_REQUESTS_PER_SECOND,
        capacity=config.MAX_REQUESTS_PER_SECOND_BURST,
    )

    universalis_conn = aiohttp.TCPConnector(
        limit=config.UNIVERSALIS_CONN_POOL,
        limit_per_host=config.UNIVERSALIS_CONN_POOL,
    )
    backend_conn = aiohttp.TCPConnector(
        limit=config.BACKEND_CONN_POOL,
        limit_per_host=config.BACKEND_CONN_POOL,
    )

    async with aiohttp.ClientSession(connector=universalis_conn) as univ_http, \
               aiohttp.ClientSession(connector=backend_conn) as backend_http:

        watcher_task = asyncio.create_task(watcher(pipeline), name="watcher")
        fetchers = [
            asyncio.create_task(fetcher(pipeline, univ_http, bucket), name=f"fetch-{i}")
            for i in range(config.MAX_FETCH_WORKERS)
        ]
        posters = [
            asyncio.create_task(poster(pipeline, backend_http), name=f"post-{i}")
            for i in range(config.MAX_POST_WORKERS)
        ]

        try:
            await _drive(pipeline)
        finally:
            pipeline.done.set()
            await _cancel_all(fetchers + posters + [watcher_task])

    log.debug("import complete")


async def _cancel_all(tasks: Iterable[asyncio.Task]) -> None:
    for t in tasks:
        t.cancel()
    await asyncio.gather(*tasks, return_exceptions=True)


def main() -> None:
    asyncio.run(amain())


if __name__ == "__main__":
    main()
