from base64 import decode
from posixpath import split
import websocket
import bson
import redis
import json
import config
import database
import time

def set_field_expiry(hash, field, timestamp):
    if(database.DB_SALES_CLEAN.hset(str(timestamp), str(hash), str(field)) == 0):
        print("ERROR: EXPIRY DATE NOT SET")

def update_recent(hash, field, timestamp):
    list_entry = str(hash) + "/" + str(field)


    #########################
    #UPDATE WORLD RECENT LIST
    #########################
    list_name = "recent_" + str(hash).split("_")[0]
    if (int(database.DB_SALES.lpush(list_name, list_entry)) > 0):
        if(int(database.DB_SALES.ltrim(list_name, 0, 1000)) > 0):
            print("{"+ str(config.REDIS_SALES_DB) + "}{HSET} Added " + list_entry + " to " + list_name)
        else:
            print("[ERROR]{"+ str(config.REDIS_SALES_DB) + "}{HSET} Could not trim " + list_name)
    else:
        print("[ERROR]{"+ str(config.REDIS_SALES_DB) + "}{HSET} Could not update " + list_name)

    #######################################
    #UPDATE DATACENTER RECENT SALES LIST
    #######################################
    
    ###################################
    #UPDATE REGION RECENT SALES LIST
    ###################################

    ###################################
    #UPDATE GLOBAL RECENT SALES LIST
    ###################################
    list_name = "recent_sales"
    if(int(database.DB_SALES.lpush(list_name, list_entry)) > 0):
        if(int(database.DB_SALES.ltrim(list_name, 0, 1000)) > 0):
            print("{"+ str(config.REDIS_SALES_DB) + "}{LPUSH} Added " + list_entry + " to " + list_name)
        else:
            print("[ERROR]{"+ str(config.REDIS_SALES_DB) + "}{LTRIM} Could not trim " + list_name)
    else:
        print("[ERROR]{"+ str(config.REDIS_SALES_DB) + "}{LPUSH} Could not update " + list_name)

def handle_add_sale(hash, value):

    #Field is a string concat so we set it beforehand
    field = str(value['buyerName']).replace(" ", "_") + "_" + str(value['timestamp'])

    #Commit to db (1 = SUCESS, 0 = FAIL)
    #hset(hash, field, value)
    if(database.DB_SALES.hset(str(hash), str(field), str(value)) == 1):
        print("{1}{HSET}Added sale " + field + " to " + hash)
        set_field_expiry(str(hash), str(field), str(time.time()));
        update_recent(hash, field, time.time())
    return


def on_message(ws_sales_add, message):
    #prepare vars
    decoded_message = (bson.decode(message))
    world = str(decoded_message['world'])
    item = str(decoded_message['item'])
    world_name = str(config.WORLDS[int(world)])

    #Set hash and sales
    hash = world_name + "_" + item
    #Field is set later, so we don't need to set it here
    sales = (json.loads(json.dumps(decoded_message['sales'])))



    for sale in sales:
        if (sale['buyerName'] in config.BANNED_SALE_BUYERS):
            continue
        handle_add_sale(hash, sale)


def subscribe(ws_sales_add):
    for i in config.WORLDS_TO_USE:
        for k in config.WORLDS_TO_USE[i]:
            sub_list_value = config.WORLDS_TO_USE[i][k]
            world_id = "{world=" + str(k) + "}"
            print("sales/add" + world_id)
            ws_sales_add.send(bson.encode({"event": "subscribe", "channel": "sales/add" + world_id}))
            print("Sent subscribe event for sales/add on world " + sub_list_value + "(" + str(k) + ")")


def start_sales_add():
    ws_sales_add = websocket.WebSocketApp(
        config.UNIVERSALLIS_URL, on_open=subscribe, on_message=on_message)
    ws_sales_add.run_forever()

