from base64 import decode
from posixpath import split
import websocket
import bson
import json
import config
import database
import log


def remove_from_recent(world_name, listing_id):
    list_name = "recent_" + world_name
    if(database.DB_LISTINGS.lrem(list_name, 1, listing_id) > 0):
        log.err_write("Removed listing " + listing_id + " from " + list_name)
        log.action("{"+str(config.REDIS_LISTINGS_DB)+"}{LREM} Removed listing " + listing_id + " from " + list_name)
    list_name = "recent_listings"
    if(database.DB_LISTINGS.lrem(list_name, 1, listing_id) > 0):
        log.err_write("Removed listing " + listing_id + " from " + list_name)
        log.action("{"+str(config.REDIS_LISTINGS_DB)+"}{LREM} Removed listing " + listing_id + " from " + list_name)

def handle_remove_listings(hash, listing):
    #Commit to db (1 = SUCESS, 0 = FAIL)
    #hdel(hash, field)
    if(database.DB_LISTINGS.json().delete(hash, listing['listingID']) > 0):
        log.action("{"+str(config.REDIS_LISTINGS_DB)+"}{JSON_DEL} Removed listing " + listing['listingID'] + " from " + hash)
    return


def on_message(ws_listing_remove, message):
    decoded_message = (bson.decode(message))
    world = str(decoded_message['world'])
    item = str(decoded_message['item'])
    world_name = str(config.WORLDS[int(world)])
    hash = world_name+"_"+str(item)
    listings = (json.loads(json.dumps(decoded_message['listings'])))
    if(world == "None" or item == "None" or len(world) == 0 or len(item) == 0):
        return

    
    for listing in listings:
        if(listing['listingID'] is None):
            continue
        if(listing['listingID'] in config.BANNED_LISTING_IDS):
            continue
        handle_remove_listings(hash, listing)
        remove_from_recent(world_name, listing['listingID'])


def subscribe(ws_listing_remove):
    for i in config.WORLDS_TO_USE:
        for k in config.WORLDS_TO_USE[i]:
            sub_list_value = config.WORLDS_TO_USE[i][k]
            world_id = "{world=" + str(k) + "}"
            log.action("listings/remove" + world_id)
            ws_listing_remove.send(bson.encode({"event": "subscribe", "channel": "listings/remove" + world_id}))
            log.action("Sent subscribe event for listings/remove on world " + sub_list_value + "(" + str(k) + ")")



def start_listing_remove():
    ws_listing_remove = websocket.WebSocketApp(
        config.UNIVERSALLIS_URL, on_open=subscribe, on_message=on_message)
    ws_listing_remove.run_forever()

