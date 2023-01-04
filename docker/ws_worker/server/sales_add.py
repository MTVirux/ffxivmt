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
from concurrent.futures import ThreadPoolExecutor, Future


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

    #Field is a string concat so we set it beforehand
    field = str(value['buyerName']).replace(" ", "_") + "_" + str(value['timestamp'])

    sale_object = {
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

    #hset(hash, field, value)
    add_entry(hash, field, sale_object)
    
    return

###########################
#    ENTRY MANIPULATION   #
###########################
def add_entry(hash, field, new_entry):
    #create_table_query = "CREATE TABLE IF NOT EXISTS sales (buyer_name text, hq boolean, on_mannequin  boolean, unit_price int, quantity int, sale_time timestamp, world_id int, item_id int, PRIMARY KEY ((item_id, world_id), sale_time))"
    try:
        query = """INSERT INTO sales (buyer_name, hq, on_mannequin, unit_price, quantity, sale_time, world_id, item_id) VALUES (%s, %s, %s, %s, %s, %s, %s, %s)"""
        params = (
            new_entry['buyerName'],
            new_entry['hq'],
            new_entry['onMannequin'],
            int(new_entry['pricePerUnit']),
            new_entry['quantity'],
            int(new_entry['timestamp'])*1000,
            new_entry['worldID'],
            new_entry['itemID'],
        )

        params_dict = {
            "buyer_name": new_entry['buyerName'],
            "hq": new_entry['hq'],
            "on_mannequin": new_entry['onMannequin'],
            "pricePerUnit": int(new_entry['pricePerUnit']),
            "quantity": new_entry['quantity'],
            "sale_time": int(new_entry['timestamp'])*1000,
            "world_id": new_entry['worldID'],
            "item_id": new_entry['itemID'],
            "world_name": config.WORLDS[new_entry['worldID']]["name"],
        }

        formatted_query = query % params

    except Exception as e:
        log.error(e)

    try:
        result_set_object = database.SCYLLA_DB.execute(query, params)
        log.action(str(json.dumps(params_dict)))
    except Exception as e:
        log.error(e)
        log.error(formatted_query)
        return False
    return True