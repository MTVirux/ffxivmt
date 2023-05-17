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
import requests
from concurrent.futures import ThreadPoolExecutor, Future


ITEM_NAME_DICT = external.get_item_name_dict()

###########################
#   WEBSOCKET FUNCTIONS   #
###########################
def on_message(ws_sales_add, message):
    #prepare vars
    decoded_message = (bson.decode(message))
    world = str(decoded_message['world'])
    item_id = str(decoded_message['item'])
    
    try:
        world_name = str(config.WORLDS[int(world)]["name"])
    except Exception as e:
        print(e)
        return

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
    
    update_gilflux_ranking_entry(world, item_id)
    return


def subscribe(ws_sales_add):
    worlds_to_use = []

    for world_id in config.WORLDS:
        if (config.WORLDS[world_id]["name"] in config.WORLDS_TO_USE):
            worlds_to_use.append(config.WORLDS[world_id])
        
        if (config.WORLDS[world_id]["datacenter"] in config.DCS_TO_USE):
            worlds_to_use.append(config.WORLDS[world_id])
        
        if (config.WORLDS[world_id]["region"] in config.REGIONS_TO_USE):
            worlds_to_use.append(config.WORLDS[world_id])

    
    for world in worlds_to_use:
        world_subscribe(ws_sales_add, str(world["name"]), str(world["id"]), str(world["datacenter"]), str(world["region"]))


def world_subscribe(ws_sales_add, world_name, world_id, world_datacenter, world_region):
    print("Subscribed to " + world_id + ": " + world_name + " (" + world_region + ", " + world_datacenter + ")")
    ws_sales_add.send(bson.encode({"event": "subscribe", "channel": "sales/add{world=" + str(world_id)+"}"}))


def start_sales_add():
    ws_sales_add = websocket.WebSocketApp(
        config.UNIVERSALLIS_URL, on_open=subscribe, on_message=on_message)
    ws_sales_add.run_forever()


###########################
#       MAIN FUNCTION     #
###########################

def handle_add_sale(hash, value):
    global ITEM_NAME_DICT

    #Field is a string concat so we set it beforehand
    field = str(value['buyerName']).replace(" ", "_") + "_" + str(value['timestamp'])

    try:
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
                "itemName":         str(external.ITEM_NAME_DICT[int(value['itemID'])]),
                "datacenter":       str(external.WORLD_INFO_DICT[int(value['worldID'])]["datacenter"]),
                "region":           str(external.WORLD_INFO_DICT[int(value['worldID'])]["region"]),
        }

    except Exception as e:
        log.error("Error while creating sale object")
        log.error(e)
        log.error(type(external.WORLD_INFO_DICT))
        log.error(str(external.WORLD_INFO_DICT[int(value['worldID'])]["datacenter"]))
        log.error(str(external.WORLD_INFO_DICT[int(value['worldID'])]["region"]))
        return

    #hset(hash, field, value)
    add_entry(hash, field, sale_object)
    if(sale_object['onMannequin'] == False):
        add_gilflux_entry(hash, field, sale_object)

    return

###########################
#    ENTRY MANIPULATION   #
###########################
def add_entry(hash, field, new_entry):
    try:
        query = """INSERT INTO sales (buyer_name, hq, on_mannequin, unit_price, quantity, sale_time, world_id, item_id, world_name, item_name, total) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)"""
        params = (
            new_entry['buyerName'],
            new_entry['hq'],
            new_entry['onMannequin'],
            int(new_entry['pricePerUnit']),
            new_entry['quantity'],
            int(new_entry['timestamp'])*1000,
            new_entry['worldID'],
            new_entry['itemID'],
            new_entry['worldName'],
            new_entry['itemName'],
            int(new_entry['total']),
        )

        escaped_params = (
            "'" + new_entry['buyerName'].replace("'", "''") + "'",
            new_entry['hq'],
            new_entry['onMannequin'],
            int(new_entry['pricePerUnit']),
            new_entry['quantity'],
            int(new_entry['timestamp'])*1000,
            new_entry['worldID'],
            new_entry['itemID'],
            "'" + new_entry['worldName'].replace("'", "''") + "'",
            "'" + new_entry['itemName'].replace("'", "''") + "'",
            int(new_entry['total']),
        )

        params_dict = {
            "buyer_name": new_entry['buyerName'],
            "hq": new_entry['hq'],
            "on_mannequin": new_entry['onMannequin'],
            "unit_price": int(new_entry['pricePerUnit']),
            "quantity": new_entry['quantity'],
            "new_entry_time": int(new_entry['timestamp'])*1000,
            "world_id": new_entry['worldID'],
            "item_id": new_entry['itemID'],
            "world_name": new_entry['worldName'],
            "item_name": new_entry['itemName'],
            "total": int(new_entry['total']),
        }

        formatted_query = query % escaped_params
        
    except Exception as e:
        log.error(e)
    try:
        result_set_object = database.SCYLLA_DB.execute(query, params)
        log.action(str(json.dumps(params_dict)))
    except Exception as e:
        log.panic(formatted_query)
        log.error(e)
        log.error(formatted_query)
        return False
    return True

def add_gilflux_entry(hash, field, new_entry):

    try:
        query = """INSERT INTO gilflux (item_id, item_name, world_id, world_name, datacenter, region, sale_time, total) VALUES (%s, %s, %s, %s, %s, %s, %s, %s)"""
    
        params = (
            new_entry['itemID'],
            new_entry['itemName'],
            new_entry['worldID'],
            new_entry['worldName'],
            new_entry['datacenter'],
            new_entry['region'],
            int(new_entry['timestamp']*1000),
            int(new_entry['total']),
        )

        escaped_params = (
            new_entry['itemID'],
            "'" + new_entry['itemName'].replace("'", "''") + "'",
            new_entry['worldID'],
            "'" + new_entry['worldName'].replace("'", "''") + "'",
            "'" + new_entry['datacenter'].replace("'", "''") + "'",
            "'" + new_entry['region'].replace("'", "''") + "'",
            new_entry['timestamp']*1000,
            int(new_entry['total']),
        )

        params_dict = {
            "item_id": new_entry['itemID'],
            "item_name": new_entry['itemName'],
            "world_id": new_entry['worldID'],
            "world_name": new_entry['worldName'],
            "datacenter": new_entry['datacenter'],
            "region": new_entry['region'],
            "sale_time": int(new_entry['timestamp']*1000),
            "total": int(new_entry['total']),
        }

        formatted_query = query % escaped_params

    except Exception as e:
        log.error(e)
    
    try:
        result_set_object = database.SCYLLA_DB.execute(query, params)
        log.action(str(json.dumps(params_dict)))

    except Exception as e:
        log.panic(formatted_query)
        log.error(e)
        log.error(formatted_query)
        return False
    
def update_gilflux_ranking_entry(world_id, item_id):

    #send POST request to ffmt_backend/api/v1/gilflux_ranking_update/[worldID]/[itemID]
    #with no data, just the request

    url = "http://mtvirux.app/api/v1/updatedb/gilflux_ranking_update/" + str(world_id) + "/" + str(item_id)
    pprint.pprint(url);

    # Make the POST request
    response = requests.get(url)

    # Check the response status code
    if response.status_code == 200:
        print("Ranking updated successfully!")
    else:
        print(f"Error updating ranking: {response.text}")


