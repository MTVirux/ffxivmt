import os
import database
import pprint

### WORLD CONFIG

def get_world_dict():
    result = database.SCYLLA_DB.execute("SELECT * FROM worlds");
    world_dict = {}

    for row in result:
        # id: row
        world_dict[row.id] = {"id" : row.id, "name" : row.name, "datacenter" : row.datacenter, "region" : row.region}
    
    return world_dict

WORLDS = get_world_dict()

WORLDS_TO_USE = []

DCS_TO_USE = {
}

#REGIONS_TO_USE = ["Europe", "North-America", "Japan", "Oceania", "中国", "NA-Cloud-DC"]
REGIONS_TO_USE = ["Europe"]
#REGIONS_TO_USE = [];

#LOGGING

LOGS_DIR = "/server/logs/"

#Should server print to logs
PRINT_TO_LOG = { 
"DEBUG":False,
"ERROR":True,
"ACTION":False,
}

#Line limit on each type of log
LIMIT_LOGS = {
"DEBUG":0,
"ERROR":0,
"ACTION":0,
}


#Should server print to console
PRINT_TO_SCREEN = {
"DEBUG":True,
"ERROR":True,
"ACTION":False,
}

### UNIVERSALLIS CONFIG

UNIVERSALLIS_URL = "wss://universalis.app/api/ws"

### BANNED IDs

BANNED_LISTING_IDS = ["5feceb66ffc86f38d952786c6d696c79c2dbc239dd4e91b46729d73a27fb57e9"]
BANNED_SALE_BUYERS = [""]

### REDIS DB CONFIG

REDIS_HOST = "localhost"
REDIS_PORT = 6379

#### REDIS DB INDEXES

REDIS_SALES_DB = os.environ.get('REDIS_SALES_DB')
REDIS_LISTINGS_DB = os.environ.get('REDIS_LISTINGS_DB')
REDIS_RECENT_DB = os.environ.get('REDIS_RECENT_CLEANING_DB')
REDIS_TIMESERIES_DB = os.environ.get('REDIS_TIMESERIES_DB')

### EXTERNAL CONTAINERS

BACKEND_HOST_CONTAINER = os.environ.get('BACKEND_HOST')