from array import array
from base64 import decode
from importlib.resources import path
from json import encoder
from posixpath import split
import string
from types import NoneType
from uuid import uuid4
import websocket
import bson
import json
import config
import database
import time
from redis.commands.json.path import Path
import log
import external
import pprint
import listings_remove

###########################
#   ENTRY FUNCTIONS   #
###########################

def add_entry(hash, field, new_entry):
    if(database.DB_LISTINGS.json().set(hash, Path.root_path(), new_entry) == 1):
        log.action("{"+ str(config.REDIS_LISTINGS_DB) + "}{JSON_SET}Added listing " + field + " to " + hash)
        return
    else:
        log.error("[ERROR]{"+ str(config.REDIS_LISTINGS_DB) + "}{JSON_SET} Could not add " + hash + " to db")

def update_entry(hash, field, updated_entry):
    if(database.DB_LISTINGS.json().set(hash, Path.root_path(), updated_entry) == 1):
        log.action("{"+ str(config.REDIS_LISTINGS_DB) + "}{JSON_SET(UPDATE)} Added listing " + field + " to " + hash)
    else:
        log.error("[ERROR]{"+ str(config.REDIS_LISTINGS_DB) + "}{JSON_SET(UPDATE)} Could not update " + hash)

###########################
#   WEBSOCKET FUNCTIONS   #
###########################
def on_message(ws_listings_add, message):
    #prepare vars
    decoded_message = (bson.decode(message))
    world = str(decoded_message['world'])
    item = str(decoded_message['item'])
    world_name = str(config.WORLDS[int(world)]["name"])

    #Set hash and listings
    hash = str(world_name + "_" + item)
    listings = (json.loads(json.dumps(decoded_message['listings'])))


    if(database.DB_LISTINGS.json().get(hash) != None):
        listings_remove.remove_entry(hash)

    for listing in listings:
        listing['worldID'] = world
        listing['worldName'] = world_name
        handle_add_listing(hash, listing)



def subscribe(ws_listings_add):
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
        world_subscribe(ws_listings_add, config.WORLDS[world_id]["name"], str(world_id))


def world_subscribe(ws_listings_add, world_name, world_id):
    ws_listings_add.send(bson.encode({"event": "subscribe", "channel": "listings/add{world=" + str(world_id)+"}"}))
    log.debug("Subscribed to listings/add on world " + world_name)



def start_listing_add():
    ws_listings_add = websocket.WebSocketApp(
        config.UNIVERSALLIS_URL, on_open=subscribe, on_message=on_message)
    ws_listings_add.run_forever()


###########################
#       MAIN FUNCTION     #
###########################

def handle_add_listing(hash, value):

    #Field is a string concat so we set it beforehand
    field = str(value['retainerName']).replace(" ", "_") + "_" + uuid4().hex;

    listing_object = {
        field : {
                    'creatorID':            str             (value['creatorID']),
                    'creatorName':          str             (value['creatorName']),
                    'hq':                   str             (value['hq']),
                    'isCrafted':            bool            (value['isCrafted']),
                    'lastReviewTime':       float           (value['lastReviewTime']),
                    'listingID':            str             (value['listingID']),
                    'materia':              json.dumps      (value['materia']),
                    'onMannequin':          bool            (value['onMannequin']),
                    'pricePerUnit':         float           (value['pricePerUnit']),
                    'quantity':             int             (value['quantity']),
                    'retainerCity':         int             (value['retainerCity']),
                    'retainerID':           str             (value['retainerID']),
                    'retainerName':         str             (value['retainerName']),
                    'sellerID':             str             (value['sellerID']),
                    'stainID':              int             (value['stainID']),
                    'total':                float           (value['total']),
                    'worldID':              int             (value['worldID']),
                    'worldName':            str             (value['worldName']),
                }
        }

    #Add listing to redis

    #hset(hash, field, value)
    db_entry = database.DB_LISTINGS.json().get(hash)
    
    if(db_entry is None):
        add_entry_result = add_entry(hash, field, listing_object)
    else:
        updated_entry = db_entry
        updated_entry.update(listing_object)
        update_entry(hash, field, updated_entry)

    #return
    return