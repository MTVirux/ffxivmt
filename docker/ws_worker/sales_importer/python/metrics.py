"""Thread-safe counters for the sales importer."""
from __future__ import annotations

import threading
import time
from dataclasses import dataclass, field

# Truncation cap for last_response so we don't memcpy entire JSON bodies under
# the metrics lock on every successful fetch.
LAST_RESPONSE_MAX_CHARS = 500


@dataclass
class Metrics:
    requests_completed: int = 0
    php_requests_completed: int = 0
    total_requests: int = 0
    total_sales_parsed: int = 0
    php_requests_failed: int = 0
    retried_requests: int = 0
    last_response: str = ""
    start_time: float = field(default_factory=time.time)
    _lock: threading.Lock = field(default_factory=threading.Lock, repr=False)

    def inc(self, attr: str, by: int = 1) -> None:
        with self._lock:
            setattr(self, attr, getattr(self, attr) + by)

    def set(self, attr: str, value) -> None:
        if attr == "last_response" and isinstance(value, str):
            value = value[:LAST_RESPONSE_MAX_CHARS]
        with self._lock:
            setattr(self, attr, value)


METRICS = Metrics()
