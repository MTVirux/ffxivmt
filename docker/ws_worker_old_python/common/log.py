"""Shared logging for ws_worker components.

Each "channel" (action / error / debug / panic / request) maps to its own
RotatingFileHandler under <logs_dir>/<channel>/, with optional stderr mirror.

Disk I/O is moved off the calling thread via QueueHandler + a single
QueueListener thread that owns the real file/stream handlers, so log.action()
on the websocket hot path doesn't block on fsync.

Callers configure once via setup() at startup, then use the channel-specific
helpers (action, error, debug, panic, request).
"""
from __future__ import annotations

import atexit
import json
import logging
import logging.handlers
import os
import queue as _queue
import time
from typing import Iterable

_CHANNELS = ("action", "error", "debug", "panic", "request")
_loggers: dict[str, logging.Logger] = {}
_listener: logging.handlers.QueueListener | None = None
_log_queue: _queue.Queue | None = None
_configured = False


def setup(
    logs_dir: str,
    enabled_channels: Iterable[str] = _CHANNELS,
    print_channels: Iterable[str] = ("error", "debug"),
    max_bytes: int = 50 * 1024 * 1024,
    backup_count: int = 5,
) -> None:
    """Wire each channel to its own rotating file + optional stderr.

    All real I/O happens on a single QueueListener thread; loggers attached to
    the calling threads only enqueue records.
    """
    global _configured, _listener, _log_queue

    enabled = set(enabled_channels)
    print_set = set(print_channels)

    # Build the real (sink) handlers; the listener owns them.
    sink_handlers: list[logging.Handler] = []

    for channel in _CHANNELS:
        os.makedirs(os.path.join(logs_dir, channel), exist_ok=True)

        if channel in enabled:
            log_path = os.path.join(logs_dir, channel, f"{channel}.log")
            file_handler = logging.handlers.RotatingFileHandler(
                log_path, maxBytes=max_bytes, backupCount=backup_count, encoding="utf-8"
            )
            file_handler.setFormatter(
                logging.Formatter(
                    f"[{channel.upper()}][%(asctime)s] %(message)s",
                    datefmt="%Y-%m-%d %H:%M:%S",
                )
            )
            file_handler.addFilter(_ChannelFilter(channel))
            sink_handlers.append(file_handler)

        if channel in print_set:
            stream = logging.StreamHandler()
            stream.setFormatter(
                logging.Formatter(
                    f"[{channel.upper()}][%(asctime)s] %(message)s",
                    datefmt="%Y-%m-%d %H:%M:%S",
                )
            )
            stream.addFilter(_ChannelFilter(channel))
            sink_handlers.append(stream)

    # One queue + one listener thread for the whole process.
    _log_queue = _queue.Queue(maxsize=0)  # unbounded; QueueHandler.put_nowait
    _listener = logging.handlers.QueueListener(
        _log_queue, *sink_handlers, respect_handler_level=True
    )
    _listener.start()
    atexit.register(_shutdown_listener)

    queue_handler = logging.handlers.QueueHandler(_log_queue)

    for channel in _CHANNELS:
        logger = logging.getLogger(f"ws_worker.{channel}")
        logger.setLevel(logging.DEBUG if channel in enabled else logging.CRITICAL + 1)
        logger.propagate = False
        for h in list(logger.handlers):
            logger.removeHandler(h)
        # We tag the record with the channel name so the listener can filter
        # per-sink (file vs stream) without each logger needing its own
        # listener.
        logger.addFilter(_TagChannel(channel))
        logger.addHandler(queue_handler)
        _loggers[channel] = logger

    _configured = True


class _TagChannel(logging.Filter):
    """Stamp ws_worker_channel onto records so sink filters can route."""

    def __init__(self, channel: str) -> None:
        super().__init__()
        self.channel = channel

    def filter(self, record: logging.LogRecord) -> bool:
        record.ws_worker_channel = self.channel
        return True


class _ChannelFilter(logging.Filter):
    """Sink-side filter: only let through records tagged for this channel."""

    def __init__(self, channel: str) -> None:
        super().__init__()
        self.channel = channel

    def filter(self, record: logging.LogRecord) -> bool:
        return getattr(record, "ws_worker_channel", None) == self.channel


def _shutdown_listener() -> None:
    global _listener
    if _listener is not None:
        try:
            _listener.stop()
        except Exception:
            pass
        _listener = None


def _get(channel: str) -> logging.Logger:
    if not _configured:
        # Fallback so import-time log calls don't crash before setup().
        setup(os.environ.get("WS_WORKER_LOGS_DIR", "/tmp/ws_worker_logs"))
    return _loggers[channel]


def action(message: str | dict) -> None:
    if isinstance(message, dict):
        payload = dict(message)
        payload.setdefault("timestamp", int(time.time() * 1000))
        message = json.dumps(payload)
    _get("action").info(message)


def error(message, exc_info: bool = False, stack: bool = True) -> None:
    """Log an error. Defaults to including the current call stack (matches the
    pre-refactor behavior). Pass exc_info=True from inside an except: block to
    include the exception traceback instead."""
    _get("error").error(str(message), exc_info=exc_info, stack_info=stack and not exc_info)


def debug(message) -> None:
    _get("debug").debug(str(message))


def request(message) -> None:
    _get("request").info(str(message))


def panic(cql: str) -> None:
    _get("panic").critical(cql + ";")
