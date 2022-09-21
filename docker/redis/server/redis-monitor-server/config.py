import os

MEMORY_FIELDS_TO_LOG = [
    "used_memory",
    "used_memory_rss",
    "used_memory_peak",
]


### REDIS DB CONFIG

REDIS_HOST = "localhost"
REDIS_PORT = 6379

#### REDIS DB INDEXES
REDIS_SALES_DB = os.environ.get('REDIS_SALES_DB')
#REDIS_LISTINGS_DB = os.environ.get('REDIS_LISTINGS_DB')
REDIS_RECENT_DB = os.environ.get('REDIS_RECENT_CLEANING_DB')
REDIS_TIMESERIES_DB = os.environ.get('REDIS_TIMESERIES_DB')

#LOGGING

LOGS_FILE = os.environ.get('REDIS_STATS_LOG_FILE').replace('.status', '.log')

SLEEP_TIME = os.environ.get('REDIS_STATUS_UPDATER_INTERVAL')