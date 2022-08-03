from array import array
from base64 import decode
from importlib.resources import path
from posixpath import split
import string
import websocket
import bson
import json
import config
import database
import time
from redis.commands.json.path import Path
import errors
import pprint


###########################
#    ENTRY MANIPULATION   #
###########################

def add_entry(hash, field, new_entry):
    if(database.DB_LISTINGS.json().set(hash, Path.root_path(), new_entry) == 1):
        print("{"+ str(config.REDIS_LISTINGS_DB) + "}{JSON_SET}Added listing " + field + " to " + hash)
        return
    else:
        errors.log("[ERROR]{"+ str(config.REDIS_LISTINGS_DB) + "}{JSON_SET} Could not add " + hash + " to db")

def update_entry(hash, field, updated_entry):
    if(database.DB_LISTINGS.json().set(hash, Path.root_path(), updated_entry) == 1):
        print("{"+ str(config.REDIS_LISTINGS_DB) + "}{JSON_SET(UPDATE)} Added listing " + field + " to " + hash)
    else:
        errors.log("[ERROR]{"+ str(config.REDIS_LISTINGS_DB) + "}{JSON_SET(UPDATE)} Could not update " + hash)

###########################
#   VALIDATION FUNCTIONS  #
###########################

def set_field_expiry(hash, field, timestamp):
    if(database.DB_LISTINGS_CLEAN.hset(timestamp, hash, field) == 0):
        errors.log("[ERROR]{"+ str(config.REDIS_LISTINGS_CLEANING_DB) + "}{HSET} Could not set field expiry for " + hash)

###########################
#      RECORD LOGGING     #
###########################     

def update_recent(hash, field):
    list_entry = hash + "/" + field


    # UPDATE WORLD RECENT LIST
    list_name = "recent_" + hash.split("_")[0]
    if (int(database.DB_LISTINGS.lpush(list_name, list_entry)) > 0):
        if(int(database.DB_LISTINGS.ltrim(list_name, 0, 1000)) > 0):
            print("{"+ str(config.REDIS_LISTINGS_DB) + "}{LPUSH} Added " + list_entry + " to listings " + list_name)
        else:
            errors.log("[ERROR]{"+ str(config.REDIS_LISTINGS_DB) + "}{LPUSH} Could not trim listings " + list_name)
    else:
        errors.log("[ERROR]{"+ str(config.REDIS_LISTINGS_DB) + "}{LPUSH} Could not add " + list_entry + " to listings" + list_name)

    # UPDATE DATACENTER RECENT LISTINGS LIST


    # UPDATE REGION RECENT LISTINGS LIST

    # UPDATE GLOBAL RECENT LISTINGS LIST
    list_name = "recent_listings"
    if(int(database.DB_LISTINGS.lpush(list_name, list_entry)) > 0):
        if(int(database.DB_LISTINGS.ltrim(list_name, 0, 1000)) > 0):
            print("{"+ str(config.REDIS_LISTINGS_DB) + "}{LPUSH} Added " + list_entry + " to listings " + list_name)
        else:
            errors.log("[ERROR]{"+ str(config.REDIS_LISTINGS_DB) + "}{LPUSH} Could not trim listings " + list_name)
    else:
        errors.log("[ERROR]{"+ str(config.REDIS_LISTINGS_DB) + "}{LPUSH} Could not add listings " + list_entry + " to " + list_name)


###########################
#       MAIN FUNCTION     #
###########################

def handle_add_listing(hash, listing):
    #Commit to db (1 = SUCESS, 0 = FAIL)
    #hset(hash, field, value)
    field = str(listing['listingID'])
    world_name = str(hash.split('_')[0])

    listing_object = {
        field : {
                'creatorID'         : str(listing['creatorID']),
                'creatorName'       : str(listing['creatorName']),
                'hq'                : bool(listing['hq']),
                'isCrafted'         : bool(listing['isCrafted']),
                'lastReviewTime'    : float(listing['lastReviewTime']),
                'listingID'         : str(listing['listingID']),
                'materia'           : (listing['materia']),
                'onMannequin'       : bool(listing['onMannequin']),
                'pricePerUnit'      : float(listing['pricePerUnit']),
                'quantity'          : int(listing['quantity']),
                'retainerCity'      : int(listing['retainerCity']),
                'retainerID'        : str(listing['retainerID']),
                'retainerName'      : str(listing['retainerName']),
                'sellerID'          : str(listing['sellerID']),
                'stainID'           : int(listing['stainID']),
                'total'             : float(listing['total']),
                'worldID'           : int(listing['worldID']),
                'worldName'         : str(listing['worldName']),
        }
    }

    
    db_entry = database.DB_LISTINGS.json().get(hash)
    
    if(db_entry is None):
        add_entry(hash, field, listing_object)
    else:
        updated_entry = {}
        updated_entry = db_entry
        updated_entry.update(listing_object)
        update_entry(hash, field, updated_entry)
    
    set_field_expiry(str(hash), str(field), time.time())
    update_recent(hash, field)
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
        if(listing['listingID'] in config.BANNED_LISTING_IDS):
            continue
        if listing['listingID'] is None:
            continue
        listing['worldID'] = world
        listing['worldName'] = world_name
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

