from base64 import decode
from posixpath import split
import string
import websocket
import bson
import redis
import json
import config
import database

def handle_add_listing(hash, listing):
    
    #Commit to db (1 = SUCESS, 0 = FAIL)
    #hset(hash, field, value)
    if(database.DB_LISTING.hset(str(hash), str(listing['listingID']), str(listing)) == 1):
        print("Added listing " + listing['listingID'] + " to " + hash)
    return


def on_message(ws_listing_add, message):
    decoded_message = (bson.decode(message))
    world = str(decoded_message['world'])
    item = str(decoded_message['item'])
    world_name = str(config.WORLDS[int(world)])
    hash = world_name+"_"+str(item)
    listings = (json.loads(json.dumps(decoded_message['listings'])))

    if(world == "None" or item == "None" or len(world) == 0 or len(item) == 0):
        return
    
    for listing in listings:
        if(type(listing['listingID']) != str):
            continue
        if(listing['listingID'] in config.BANNED_LISTING_IDS):
            continue
        handle_add_listing(hash, listing)


def subscribe(ws_listing_add):
    for i in config.WORLDS_TO_USE:
        for k in config.WORLDS_TO_USE[i]:
            sub_list_value = config.WORLDS_TO_USE[i][k]
            world_id = "{world=" + str(k) + "}"
            print("listings/add" + world_id)
            ws_listing_add.send(bson.encode({"event": "subscribe", "channel": "listings/add" + world_id}))
            print("Sent subscribe event for listings/add on world " + sub_list_value + "(" + str(k) + ")")



def start_listing_add():
    ws_listing_add = websocket.WebSocketApp(
        config.UNIVERSALLIS_URL, on_open=subscribe, on_message=on_message)
    ws_listing_add.run_forever()

