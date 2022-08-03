from base64 import decode
from importlib.resources import path
from posixpath import split
import websocket
import bson
import redis
import json
import config
import database
import time
from redis.commands.json.path import Path
import log
###########################
#    ENTRY MANIPULATION   #
###########################

def add_entry(hash, field, new_entry):
    if(database.DB_SALES.json().set(hash, Path.root_path(), new_entry) == 1):
        log.action("{"+ str(config.REDIS_SALES_DB) + "}{JSON_SET}Added sale " + field + " to " + hash)
    else:
        log.error("[ERROR]{"+ str(config.REDIS_SALES_DB) + "}{JSON_SET} Could not add " + hash + " to db")

def update_entry(hash, field, updated_entry):
    if(database.DB_SALES.json().set(hash, Path.root_path(), updated_entry) == 1):
        log.action("{"+ str(config.REDIS_SALES_DB) + "}{JSON_SET(UPDATE)} Added sale " + field + " to " + hash)
    else:
        log.error("[ERROR]{"+ str(config.REDIS_SALES_DB) + "}{JSON_SET(UPDATE)} Could not update " + hash)
    

###########################
#   VALIDATION FUNCTIONS  #
###########################

def set_field_expiry(hash, field, timestamp):
    if(database.DB_SALES_CLEAN.hset(timestamp, hash, field) == 0):
        log.error("[ERROR]{"+ str(config.REDIS_SALES_CLEANING_DB) + "}{HSET} Could not set field expiry for " + hash)

###########################
#      RECORD LOGGING     #
###########################     

def update_recent(hash, field):
    list_entry = hash + "/" + field


    # UPDATE WORLD RECENT LIST
    list_name = "recent_" + hash.split("_")[0]
    if (int(database.DB_SALES.lpush(list_name, list_entry)) > 0):
        if(int(database.DB_SALES.ltrim(list_name, 0, 1000)) > 0):
            log.action("{"+ str(config.REDIS_SALES_DB) + "}{LPUSH} Added " + list_entry + " to sales " + list_name)
        else:
            log.error("[ERROR]{"+ str(config.REDIS_SALES_DB) + "}{LPUSH} Could not trim sales " + list_name)
    else:
        log.error("[ERROR]{"+ str(config.REDIS_SALES_DB) + "}{LPUSH} Could not add sales " + list_entry + " to " + list_name)

    # UPDATE DATACENTER RECENT SALES LIST
    
    
    # UPDATE REGION RECENT SALES LIST

    # UPDATE GLOBAL RECENT SALES LIST
    list_name = "recent_sales"
    if(int(database.DB_SALES.lpush(list_name, list_entry)) > 0):
        if(int(database.DB_SALES.ltrim(list_name, 0, 1000)) > 0):
            log.action("{"+ str(config.REDIS_SALES_DB) + "}{LPUSH} Added " + list_entry + " to sales " + list_name)
        else:
            log.error("[ERROR]{"+ str(config.REDIS_SALES_DB) + "}{LPUSH} Could not trim sales " + list_name)
    else:
        log.error("[ERROR]{"+ str(config.REDIS_SALES_DB) + "}{LPUSH} Could not add sales " + list_entry + " to " + list_name)




###########################
#   WEBSOCKET FUNCTIONS   #
###########################
def on_message(ws_sales_add, message):
    #prepare vars
    decoded_message = (bson.decode(message))
    world = str(decoded_message['world'])
    item = str(decoded_message['item'])
    world_name = str(config.WORLDS[int(world)])

    #Set hash and sales
    hash = str(world_name + "_" + item)
    sales = (json.loads(json.dumps(decoded_message['sales'])))

    for sale in sales:
        if (sale['buyerName'] in config.BANNED_SALE_BUYERS):
            continue
        sale['worldID'] = world
        sale['worldName'] = world_name
        handle_add_sale(hash, sale)


def subscribe(ws_sales_add):
    for i in config.WORLDS_TO_USE:
        for k in config.WORLDS_TO_USE[i]:
            sub_list_value = config.WORLDS_TO_USE[i][k]
            world_id = "{world=" + str(k) + "}"
            log.action("sales/add" + world_id)
            ws_sales_add.send(bson.encode({"event": "subscribe", "channel": "sales/add" + world_id}))
            log.action("Sent subscribe event for sales/add on world " + sub_list_value + "(" + str(k) + ")")


def start_sales_add():
    ws_sales_add = websocket.WebSocketApp(
        config.UNIVERSALLIS_URL, on_open=subscribe, on_message=on_message)
    ws_sales_add.run_forever()


###########################
#       MAIN FUNCTION     #
###########################

def handle_add_sale(hash, value):

    #Field is a string concat so we set it beforehand
    field = str(value['buyerName']).replace(" ", "_") + "_" + str(value['timestamp'])

    sale_object = {
        field : {
            "buyerName":        str(value['buyerName']),
            "hq":               bool(value['hq']),
            "onMannequin":      bool(value['onMannequin']),
            "pricePerUnit":     float(value['pricePerUnit']),
            "quantity":         int(value['quantity']),
            "timestamp":        float(value['timestamp']),
            "total":            float(value['total']),
            "worldID":          int(value['worldID']),
            "worldName":        str(value['worldName']),
        }
    }

    #Commit to db (1 = SUCESS, 0 = FAIL)
    #hset(hash, field, value)
    db_entry = database.DB_SALES.json().get(hash)
    
    if(db_entry is None):
        add_entry(hash, field, sale_object)
    else:
        updated_entry = db_entry
        updated_entry.update(sale_object)
        update_entry(hash, field, updated_entry)

    set_field_expiry(hash, field, timestamp = str(time.time()))
    update_recent(hash, field)
    return