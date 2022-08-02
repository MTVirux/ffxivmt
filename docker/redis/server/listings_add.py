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
    if(database.DB_LISTINGS_CLEAN.hset(str(timestamp), str(hash), str(field)) == 1):
        print("{"+ str(config.REDIS_LISTINGS_CLEANING_DB) + "}{HSET} Set expiry time for " + field + " in " + hash)

def update_recent(hash, field, timestamp):
    list_name = "recent_" + str(hash).split("_")[0]
    list_entry = str(hash) + "/" + str(field)


    #########################
    #UPDATE WORLD RECENT LIST
    #########################
    if (int(database.DB_LISTING.lpush(list_name, list_entry)) > 0):
        if(int(database.DB_LISTING.ltrim(list_name, 0, 1000)) > 0):
            print("{"+ str(config.REDIS_LISTINGS_DB) + "}{LPUSH} Added " + list_entry + " to " + list_name)
        else:
            print("[ERROR]{"+ str(config.REDIS_LISTINGS_DB) + "}{LTRIM} Could not trim " + list_name)
    else:
        print("[ERROR]{"+ str(config.REDIS_LISTINGS_DB) + "}{LPUSH} Could not update " + list_name)

    #######################################
    #UPDATE DATACENTER RECENT LISTINGS LIST
    #######################################
    
    ###################################
    #UPDATE REGION RECENT LISTINGS LIST
    ###################################

    ###################################
    #UPDATE GLOBAL RECENT LISTINGS LIST
    ###################################
    if(int(database.DB_LISTING.lpush("recent_listings", list_entry)) > 0):
        if(int(database.DB_LISTING.ltrim("recent_listings", 0, 1000)) > 0):
            print("{"+ str(config.REDIS_LISTINGS_DB) + "}{LPUSH} Added " + list_entry + " to recent_listings")
        else:
            print("[ERROR]{"+ str(config.REDIS_LISTINGS_DB) + "}{LTRIM} Could not trim recent_listings")
    else:
        print("[ERROR]{"+ str(config.REDIS_LISTINGS_DB) + "}{LPUSH} Could not update recent_listings")


def handle_add_listing(hash, listing):
    
    #Commit to db (1 = SUCESS, 0 = FAIL)
    #hset(hash, field, value)
    field = str(listing['listingID'])
    if(database.DB_LISTING.hset(str(hash), str(field), str(listing)) == 1):
        print("{0}{HSET}Added listing " + field + " to " + hash)
        set_field_expiry(str(hash), str(field), time.time())
        update_recent(hash, field, time.time())
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

