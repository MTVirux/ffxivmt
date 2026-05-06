"""ws_worker server configuration."""
import os

### UNIVERSALIS

UNIVERSALLIS_URL = "wss://universalis.app/api/ws"

# Worlds to subscribe to. A world is included if any of its name, datacenter,
# or region appears in WORLDS_TO_USE / DCS_TO_USE / REGIONS_TO_USE.
WORLDS_TO_USE: list[str] = []
DCS_TO_USE: list[str] = []
REGIONS_TO_USE: list[str] = ["Europe", "North-America"]

### BANS

BANNED_SALE_BUYERS: frozenset[str] = frozenset({""})

### LOGGING

LOGS_DIR = "/server/logs/"

# Whether to write each channel to its rotating log file.
PRINT_TO_LOG = {
    "DEBUG": False,
    "ERROR": True,
    "ACTION": False,
}

# Whether to mirror each channel to stdout/stderr.
PRINT_TO_SCREEN = {
    "DEBUG": True,
    "ERROR": True,
    "ACTION": False,
}

### EXTERNAL CONTAINERS

BACKEND_HOST_CONTAINER = os.environ.get("BACKEND_HOST", "ffmt_backend")

### GILFLUX FAN-OUT

# Coalesce repeated ranking refreshes for the same (world, item): if a sale
# arrived for that pair within the last N seconds, drop the new request.
GILFLUX_COALESCE_WINDOW_S: float = 2.0

# Async tasks draining the gilflux update queue.
GILFLUX_WORKERS: int = 8

# Max queued (world, item) refresh requests. Drop-oldest when full so the
# queue can't grow unboundedly if the backend stalls.
GILFLUX_QUEUE_MAX: int = 1000

# HTTP timeout for a single gilflux update call.
GILFLUX_HTTP_TIMEOUT_S: float = 10.0
