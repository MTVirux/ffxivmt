"""HTTP helpers for the sales importer (aiohttp).

Holds the async rate limiter and the backend POST routine. The session
objects themselves are owned by sales_importer.main() — we just provide the
verbs.
"""
from __future__ import annotations

import asyncio
import json
import time

import aiohttp

import config
from common import log
from metrics import METRICS


class TokenBucket:
    """Asyncio token bucket — caps Universalis request rate independently of
    fetcher concurrency."""

    def __init__(self, rate: float, capacity: float | None = None) -> None:
        self.rate = float(rate)
        self.capacity = float(capacity) if capacity is not None else max(1.0, rate)
        self._tokens = self.capacity
        self._last = time.monotonic()
        self._lock = asyncio.Lock()

    async def acquire(self) -> None:
        async with self._lock:
            while True:
                now = time.monotonic()
                self._tokens = min(
                    self.capacity, self._tokens + (now - self._last) * self.rate
                )
                self._last = now
                if self._tokens >= 1.0:
                    self._tokens -= 1.0
                    return
                deficit = 1.0 - self._tokens
                await asyncio.sleep(deficit / self.rate)


async def fetch_one(
    http: aiohttp.ClientSession, bucket: TokenBucket, url: str
) -> str | None:
    """GET url, return body text on success or None on failure (caller will
    re-queue). Increments METRICS.requests_completed on success."""
    await bucket.acquire()
    log.request(f"GET {url}")
    try:
        async with http.get(url, timeout=aiohttp.ClientTimeout(total=60)) as resp:
            if resp.status != 200:
                return None
            text = await resp.text()
            if not text:
                return None
    except (aiohttp.ClientError, asyncio.TimeoutError) as e:
        log.error(f"fetch error {url}: {e!r}")
        return None

    METRICS.inc("requests_completed")
    return text


async def post_response(
    http: aiohttp.ClientSession, response_item: dict
) -> bool:
    """POST a Universalis response to the backend. True on success, False on
    failure (caller will re-queue the URL)."""
    url = response_item["url"]
    text = response_item["json"]
    METRICS.set("last_response", text)

    try:
        async with http.post(
            f"http://{config.BACKEND_HOST_CONTAINER}/api/v1/updatedb/python_request",
            data=text.encode("utf-8"),
            headers={"Content-type": "application/json"},
            timeout=aiohttp.ClientTimeout(total=120),
        ) as resp:
            body = await resp.text()
            if resp.status != 200:
                log.error(f"PHP non-200 ({resp.status}) for {url}: {body}")
                METRICS.inc("php_requests_failed")
                return False
    except (aiohttp.ClientError, asyncio.TimeoutError) as e:
        log.error(f"error posting to PHP {url}: {e!r}")
        return False

    try:
        obj = json.loads(body)
        parsed = int(obj["data"]["parsed_sales"])
    except (ValueError, KeyError, TypeError) as e:
        log.error(f"could not parse PHP response for {url}: {e!r} body={body[:500]}")
        return False

    METRICS.inc("php_requests_completed")
    METRICS.inc("total_sales_parsed", parsed)
    log.action(body)
    return True
