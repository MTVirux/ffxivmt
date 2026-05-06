"""sales_importer configuration."""
import os

### API

ENTRIES_TO_RETURN = 999999  # max history entries per item (Universalis cap: 999999)

# If True, request all-time history; otherwise only the last
# TIME_AGO_TO_IMPORT_SALES milliseconds.
IMPORT_ALL_TIME = False
TIME_AGO_TO_IMPORT_SALES = 432_000_000  # 5 days

UNIVERSALIS_URL = "https://universalis.app/api/v2/"
UNIVERSALIS_SALES_ENDPOINT = "history/"

REGIONS_TO_IMPORT = ["Europe"]

### CONCURRENCY

# Universalis /history accepts up to 100 ids per request.
ITEMS_PER_REQUEST = 100

# Backpressure between fetch and post stages. Sized so a backend hiccup of a
# few seconds doesn't stall the fetchers.
RESPONSE_QUEUE_LIMIT = 200

# Universalis caps simultaneous connections per IP at 8. More fetchers than
# that just contend on the connector with no throughput gain.
MAX_FETCH_WORKERS = 8
MAX_POST_WORKERS = 16

# Universalis API rate limit: 25 req/s sustained, 50 req/s burst. The token
# bucket sustains MAX_REQUESTS_PER_SECOND with capacity = MAX_BURST so a
# brief idle window can be spent in a burst.
MAX_REQUESTS_PER_SECOND = 25
MAX_REQUESTS_PER_SECOND_BURST = 50

# Per-host TCP connection pool sizes (aiohttp connector "limit_per_host").
# Universalis caps at 8 per IP; backend is local, give it a wider lane.
UNIVERSALIS_CONN_POOL = 8
BACKEND_CONN_POOL = 32

### LOGGING

LOGS_DIR = "/sales_importer/logs/"

PRINT_TO_LOG = {
    "DEBUG": False,
    "ERROR": True,
    "ACTION": False,
    "REQUEST": False,
}

PRINT_TO_SCREEN = {
    "DEBUG": True,
    "ERROR": True,
    "ACTION": False,
    "REQUEST": False,
}

### EXTERNAL CONTAINERS

BACKEND_HOST_CONTAINER = os.environ.get("BACKEND_HOST", "ffmt_backend")

### STATUS FILE

STATUS_FILE_PATH = "/sales_importer/IMPORT.status"
