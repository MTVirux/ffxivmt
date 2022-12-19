from base64 import decode
from importlib.resources import path
from posixpath import split
import websocket
import bson
import json
import config
import database
import time
import log
import external
import pprint

###########################
#    ENTRY MANIPULATION   #
###########################

def add_entry(hash, field, new_entry):
    log.debug("kekw")
    return
    query = "INSERT INTO table_name (buyerName, hq, onMannequin, pricePerUnit, quantity, timestamp, total, worldID, worldName, itemID) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s)";
    params = (
        str(new_entry['buyerName']),
        bool(new_entry['hq']),
        bool(new_entry['onMannequin']),
        float(new_entry['pricePerUnit']),
        int(new_entry['quantity']),
        float(new_entry['timestamp']),
        float(new_entry['total']),
        int(new_entry['worldID']),
        str(new_entry['worldName']),
        int(new_entry['itemID']),
    )
    database.SCYLLA_DB.execute(query, params)


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
    log.debug("test")

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


###########################
#       MAIN FUNCTION     #
###########################

def handle_add_sale(hash, value):
    log.debug("test")

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
            "itemID":           int(value['itemID']),
        }
    }

    #hset(hash, field, value)
    add_entry(hash, field, sale_object)
    
    return