import os

#API CONFIG

ENTRIES_TO_RETURN = 999999                                          #Max number of entries to return (Max: 999999)

IMPORT_ALL_TIME = True                                              #Set to true to import all time, false to import the time set in TIME_TO_IMPORT_SALES

TIME_AGO_TO_IMPORT_SALES = 7776000000                               #Time in milliseconds to import sales for, only used if IMPORT_ALL_TIME is false

UNIVERSALIS_URL = "https://universalis.app/api/v2/"
UNIVERSALIS_SALES_ENDPOINT = "history/"



#LOGGING

LOGS_DIR = "/sales_importer/logs/" #Directory to store logs in

#What channels should server print to logs
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


### EXTERNAL CONTAINERS

BACKEND_HOST_CONTAINER = os.environ.get('BACKEND_HOST')