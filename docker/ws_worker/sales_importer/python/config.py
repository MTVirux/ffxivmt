import os

#LOGGING

LOGS_DIR = "/sales_importer/logs/"

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

UNIVERSALLIS_URL = "https://universalis.app/api/v2/"


### EXTERNAL CONTAINERS

BACKEND_HOST_CONTAINER = os.environ.get('BACKEND_HOST')