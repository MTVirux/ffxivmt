"""Universalis sales/add websocket consumer (asyncio).

Subscribes to sales/add events for the configured worlds, fires each sale
into Scylla via a prepared statement (execute_async, fire-and-forget), and
funnels gilflux ranking refreshes through a coalescing async queue so we
don't hammer the backend with duplicate per-(world, item) refreshes.
"""
from __future__ import annotations

import asyncio
import time
from typing import Iterable

import aiohttp
import bson
import websockets

import config
from common import db as common_db
from common import log
from common import world_data

INSERT_SALE_CQL = (
    "INSERT INTO sales ("
    "buyer_name, hq, on_mannequin, unit_price, quantity, sale_time, "
    "world_id, item_id, world_name, item_name, datacenter, region, total"
    ") VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"
)

_INSERT_STMT = None


def _insert_stmt():
    global _INSERT_STMT
    if _INSERT_STMT is None:
        _INSERT_STMT = common_db.prepare_cached(INSERT_SALE_CQL)
    return _INSERT_STMT


###########################
#       INSERTS           #
###########################
def _add_entry(sale: dict) -> None:
    """Fire an async Scylla insert. Logs on driver-level failure."""
    params = (
        sale["buyerName"],
        sale["hq"],
        sale["onMannequin"],
        int(sale["pricePerUnit"]),
        sale["quantity"],
        int(sale["timestamp"]) * 1000,
        sale["worldID"],
        sale["itemID"],
        sale["worldName"],
        sale["itemName"],
        sale["datacenter"],
        sale["region"],
        int(sale["total"]),
    )
    future = common_db.get_session().execute_async(_insert_stmt(), params)
    future.add_errback(lambda exc, p=params: _on_insert_error(exc, p))
    log.action(
        {
            "buyer_name": sale["buyerName"],
            "world_id": sale["worldID"],
            "item_id": sale["itemID"],
            "unit_price": int(sale["pricePerUnit"]),
            "quantity": sale["quantity"],
            "timestamp": int(sale["timestamp"]) * 1000,
        }
    )


def _on_insert_error(exc: BaseException, params: tuple) -> None:
    log.error(f"sale insert failed: {exc!r} params={params}")
    log.panic(f"-- params: {params}\n{INSERT_SALE_CQL}")


def _build_sale(sale: dict, world_id: int, world_info: dict) -> dict | None:
    item_id = int(sale["itemID"])
    try:
        return {
            "buyerName": str(sale["buyerName"]),
            "hq": bool(sale["hq"]),
            "onMannequin": bool(sale["onMannequin"]),
            "pricePerUnit": float(sale["pricePerUnit"]),
            "quantity": int(sale["quantity"]),
            "timestamp": float(sale["timestamp"]),
            "total": float(sale["total"]),
            "worldID": world_id,
            "worldName": world_info["name"],
            "itemID": item_id,
            "itemName": world_data.item_name_dict()[item_id],
            "datacenter": world_info["datacenter"],
            "region": world_info["region"],
        }
    except Exception as e:
        log.error(f"could not build sale object: {e!r} raw={sale}")
        return None


###########################
#   GILFLUX FAN-OUT       #
###########################
class GilfluxCoalescer:
    """Dedupes (world, item) refresh requests within a time window and bounds
    the in-flight queue so a slow backend can't blow up memory."""

    def __init__(self) -> None:
        self._queue: asyncio.Queue[tuple[int, int]] = asyncio.Queue(
            maxsize=config.GILFLUX_QUEUE_MAX
        )
        self._last_fired: dict[tuple[int, int], float] = {}
        self.dropped_coalesced = 0
        self.dropped_full = 0

    def submit(self, world_id: int, item_id: int) -> None:
        key = (world_id, item_id)
        now = time.monotonic()
        last = self._last_fired.get(key)
        if last is not None and (now - last) < config.GILFLUX_COALESCE_WINDOW_S:
            self.dropped_coalesced += 1
            return
        self._last_fired[key] = now
        try:
            self._queue.put_nowait(key)
        except asyncio.QueueFull:
            # Backend is buried — drop the new request rather than block the
            # websocket consumer. The previous timestamp stamp ensures this
            # pair won't immediately re-enqueue.
            self.dropped_full += 1

    async def get(self) -> tuple[int, int]:
        return await self._queue.get()

    def task_done(self) -> None:
        self._queue.task_done()


async def _gilflux_worker(http: aiohttp.ClientSession, coalescer: GilfluxCoalescer) -> None:
    while True:
        world_id, item_id = await coalescer.get()
        url = (
            f"http://{config.BACKEND_HOST_CONTAINER}/api/v1/updatedb/"
            f"gilflux_ranking_update/{world_id}/{item_id}"
        )
        try:
            async with http.get(
                url, timeout=aiohttp.ClientTimeout(total=config.GILFLUX_HTTP_TIMEOUT_S)
            ) as response:
                if response.status != 200:
                    log.error(f"gilflux update non-200 ({response.status}): {url}")
        except (aiohttp.ClientError, asyncio.TimeoutError) as e:
            log.error(f"gilflux update failed {url}: {e!r}")
        finally:
            coalescer.task_done()


###########################
#   WEBSOCKET HANDLERS    #
###########################
def _resolve_subscribed_world_ids() -> list[int]:
    """Single pass over the world map; dedup IDs that match multiple filters."""
    worlds = world_data.world_dict()
    names = set(config.WORLDS_TO_USE)
    dcs = set(config.DCS_TO_USE)
    regions = set(config.REGIONS_TO_USE)
    target: set[int] = set()
    for world_id, info in worlds.items():
        if info["name"] in names or info["datacenter"] in dcs or info["region"] in regions:
            target.add(world_id)
    return sorted(target)


async def _subscribe(ws, world_ids: Iterable[int]) -> None:
    worlds = world_data.world_dict()
    for world_id in world_ids:
        info = worlds[world_id]
        await ws.send(
            bson.encode(
                {"event": "subscribe", "channel": f"sales/add{{world={world_id}}}"}
            )
        )
        log.debug(
            f"subscribed to sales/add world={world_id} "
            f"({info['name']}, {info['datacenter']}, {info['region']})"
        )


def _handle_message(message: bytes, coalescer: GilfluxCoalescer) -> None:
    msg = bson.decode(message)
    world_id = int(msg["world"])
    item_id = int(msg["item"])

    world_info = world_data.world_dict().get(world_id)
    if world_info is None:
        log.error(f"unknown world id in message: {world_id}")
        return

    banned = config.BANNED_SALE_BUYERS
    for sale in msg.get("sales", ()):
        if sale.get("buyerName") in banned:
            continue
        sale["worldID"] = world_id
        sale["worldName"] = world_info["name"]
        sale["itemID"] = item_id
        built = _build_sale(sale, world_id, world_info)
        if built is not None:
            _add_entry(built)

    coalescer.submit(world_id, item_id)


###########################
#       ENTRY POINT       #
###########################
async def _run_consumer(coalescer: GilfluxCoalescer) -> None:
    """Run the sales/add websocket forever, with auto-reconnect + heartbeat."""
    backoff = 1
    while True:
        try:
            async with websockets.connect(
                config.UNIVERSALLIS_URL,
                ping_interval=30,
                ping_timeout=10,
                max_size=2**24,
            ) as ws:
                log.debug("sales/add websocket open; subscribing")
                await _subscribe(ws, _resolve_subscribed_world_ids())
                backoff = 1
                async for raw in ws:
                    if not isinstance(raw, (bytes, bytearray)):
                        log.error(f"unexpected non-binary frame: {type(raw).__name__}")
                        continue
                    try:
                        _handle_message(raw, coalescer)
                    except Exception as e:
                        log.error(f"on_message crashed: {e!r}", exc_info=True)
        except (websockets.WebSocketException, OSError) as e:
            log.error(f"websocket disconnected: {e!r}")
        except Exception as e:
            log.error(f"websocket loop crashed: {e!r}", exc_info=True)

        await asyncio.sleep(backoff)
        backoff = min(backoff * 2, 60)


async def start_sales_add() -> None:
    coalescer = GilfluxCoalescer()
    connector = aiohttp.TCPConnector(limit=max(32, config.GILFLUX_WORKERS * 2))
    async with aiohttp.ClientSession(connector=connector) as http:
        workers = [
            asyncio.create_task(_gilflux_worker(http, coalescer), name=f"gilflux-{i}")
            for i in range(config.GILFLUX_WORKERS)
        ]
        try:
            await _run_consumer(coalescer)
        finally:
            for w in workers:
                w.cancel()
            await asyncio.gather(*workers, return_exceptions=True)
