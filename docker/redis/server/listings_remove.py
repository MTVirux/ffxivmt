from base64 import decode
from posixpath import split
import websocket
import bson
import redis
import json
import config
import database

def handle_add_listing(hash, listing):

    #Commit to db (1 = SUCESS, 0 = FAIL)
    #hdel(hash, field)
    
    if(database.DB_LISTING.hdel(str(hash), str(listing['listingID'])) == 1 ):
        print("Removed listing " + listing['listingID'] + " from " + hash)
    return


def on_message(ws_listing_remove, message):
    decoded_message = (bson.decode(message))
    world = str(decoded_message['world'])
    item = str(decoded_message['item'])
    world_name = str(config.WORLDS[int(world)])
    hash = world_name+"_"+str(item)
    listings = (json.loads(json.dumps(decoded_message['listings'])))

    if(not(world != "None" and item != "None" and len(world) != 0 and len(item) != 0)):
        return
    
    for listing in listings:
        if(listing['listingID'] in config.BANNED_LISTING_IDS):
            continue
        handle_add_listing(hash, listing)


def subscribe(ws_listing_remove):
    for i in config.WORLDS_TO_USE:
        for k in config.WORLDS_TO_USE[i]:
            sub_list_value = config.WORLDS_TO_USE[i][k]
            world_id = "{world=" + str(k) + "}"
            print("listings/remove" + world_id)
            ws_listing_remove.send(bson.encode({"event": "subscribe", "channel": "listings/remove" + world_id}))
            print("Sent subscribe event for listings/remove on world " + sub_list_value + "(" + str(k) + ")")



def start_listing_remove():
    ws_listing_remove = websocket.WebSocketApp(
        config.UNIVERSALLIS_URL, on_open=subscribe, on_message=on_message)
    ws_listing_remove.run_forever()

