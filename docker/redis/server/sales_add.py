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
import external
import pprint

###########################
#    ENTRY MANIPULATION   #
###########################

def add_entry(hash, field, new_entry):
    if(database.DB_SALES.json().set(hash, Path.root_path(), new_entry) == 1):
        log.action("{"+ str(config.REDIS_SALES_DB) + "}{JSON_SET}Added sale " + field + " to " + hash)
        return True
    else:
        log.error("[ERROR]{"+ str(config.REDIS_SALES_DB) + "}{JSON_SET} Could not add " + hash + " to db")
        return False

def update_entry(hash, field, updated_entry):
    if(database.DB_SALES.json().set(hash, Path.root_path(), updated_entry) == 1):
        log.action("{"+ str(config.REDIS_SALES_DB) + "}{JSON_SET(UPDATE)} Added sale " + field + " to " + hash)
    else:
        log.error("[ERROR]{"+ str(config.REDIS_SALES_DB) + "}{JSON_SET(UPDATE)} Could not update " + hash)

def update_timeseries(field):

    world_id_int = int(field['worldID'])
    field["datacenter"] = config.WORLDS[world_id_int]["datacenter"]
    field["region"] = config.WORLDS[world_id_int]["region"]
    field["worldName"] = config.WORLDS[world_id_int]["name"]
    world_hash = str(field['worldName'] + "_" + field['itemID'])
    timestamp = int(field['timestamp']);
    value = field['total']
    if(database.DB_TIMESERIES.ts().add(str(world_hash), str(timestamp), float(value)) == int(timestamp)):
        log.action("{"+ str(config.REDIS_TIMESERIES_DB) + "}{TS_ADD} Added " + str(world_hash) + "->" + str(value) + " @ " + str(timestamp))
    else:
        log.error("[ERROR]{"+ str(config.REDIS_TIMESERIES_DB) + "}{TS_ADD} Could not add " + str(world_hash) + "->" + str(value) + " @ " + str(timestamp))

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
    item_id = str(decoded_message['item'])
    world_name = str(config.WORLDS[int(world)]["name"])

    #Set hash and sales
    hash = str(world_name + "_" + item_id)
    sales = (json.loads(json.dumps(decoded_message['sales'])))

    for sale in sales:
        if (sale['buyerName'] in config.BANNED_SALE_BUYERS):
            continue
        sale['worldID'] = world
        sale['worldName'] = world_name
        sale['itemID'] = item_id
        handle_add_sale(hash, sale)
        update_timeseries(item, sale)
    
    external.warn_backend_to_update_item(hash)
    return


def subscribe(ws_sales_add):
    world_ids_to_use = []

    for world in config.WORLDS:
        for world_to_use in config.WORLDS_TO_USE:
            if(config.WORLDS[world]["name"] == config.WORLDS_TO_USE[world_to_use]):
                world_ids_to_use.append(world)

    for world in config.WORLDS:
        for dc_to_use in config.DCS_TO_USE:
            if(config.WORLDS[world]["datacenter"] == config.DCS_TO_USE[dc_to_use]):
                world_ids_to_use.append(world)

    for world in config.WORLDS:
        for region_to_use in config.REGIONS_TO_USE:
            if(config.WORLDS[world]["region"] == config.REGIONS_TO_USE[region_to_use]):
                world_ids_to_use.append(world)

    for world_id in world_ids_to_use:
        world_subscribe(ws_sales_add, config.WORLDS[world_id]["name"], str(world_id))


def world_subscribe(ws_sales_add, world_name, world_id):
    ws_sales_add.send(bson.encode({"event": "subscribe", "channel": "sales/add{world=" + str(world_id)+"}"}))
    log.debug("Subscribed to sales/add on world " + world_name)



def start_sales_add():
    ws_sales_add = websocket.WebSocketApp(
        config.UNIVERSALLIS_URL, on_open=subscribe, on_message=on_message)
    ws_sales_add.run_forever()
    log.error("sales_add died")


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

    #hset(hash, field, value)
    db_entry = database.DB_SALES.json().get(hash)
    
    if(db_entry is None):
        add_entry_result = add_entry(hash, field, sale_object)
    else:
        updated_entry = db_entry
        updated_entry.update(sale_object)
        update_entry(hash, field, updated_entry)

    return