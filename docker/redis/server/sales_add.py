from base64 import decode
from posixpath import split
import websocket
import bson
import redis
import json
import config
import database
import pprint

def handle_add_sale(hash, value):

    #Field is a string concat so we set it beforehand
    field = str(value['buyerName']) + "_" + str(value['timestamp'])

    #Commit to db (1 = SUCESS, 0 = FAIL)
    #hset(hash, field, value)
    if(database.DB_SALES.hset(str(hash), str(field), str(value)) == 1):
        print("Added sale " + field + " to " + hash)
    return


def on_message(ws_sales_add, message):
    #prepare vars
    decoded_message = (bson.decode(message))
    world = str(decoded_message['world'])
    item = str(decoded_message['item'])

    #Set hash and sales
    hash = world + "_" + item
    #Field is set later, so we don't need to set it here
    sales = (json.loads(json.dumps(decoded_message['sales'])))



    for sale in sales:
        if (sale['buyerName'] in config.BANNED_SALE_BUYERS):
            continue
        handle_add_sale(hash, sale)


def subscribe(ws_sales_add):
    ws_sales_add.send(bson.encode({"event": "subscribe", "channel": "sales/add"}))
    print("Sent subscribe event for sales/add")


def start_sales_add():
    ws_sales_add = websocket.WebSocketApp(
        config.UNIVERSALLIS_URL, on_open=subscribe, on_message=on_message)
    ws_sales_add.run_forever()

