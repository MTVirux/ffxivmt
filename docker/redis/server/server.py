from base64 import decode
from posixpath import split
import websocket
import bson
import redis
import json
import config
import threading
import listings_add
import listings_remove
import sales_add
import sales_remove
import db_cleaning
import os
import sys

#Comment the following line to enable print
sys.stdout = open(os.devnull, 'w')

#/Item_world
#/-----/World
#/      /-------listingID
#/      /       /----------[JSON_DATA]
#/      /    
#/      /-------listingID
#/      /       /----------[JSON_DATA]
#/
#/------/World
#/      /-------listingID
#/      /       /----------[JSON_DATA]

def on_message(wsapp, message):
    decoded_message = (bson.decode(message))
    world = str(decoded_message['world'])
    item = str(decoded_message['item'])
    listings = (json.loads(json.dumps(decoded_message['listings'])))
    world_name = str(config.WORLDS[int(world)])
    hash = world_name+"_"+str(item)


    if(not(world != "None" and item != "None" and world != "" and item != "")):
        return
    

    for listing in listings:
        if(decoded_message['event'] == 'listings/remove'):
            handle_remove(hash, listing)
        
        if(decoded_message['event'] == 'listings/add'):
            handle_add(hash, listing)


threads = []

listing_add_thread              = threading.Thread(target=listings_add.start_listing_add);
threads.append(listing_add_thread)

listing_remove_thread           = threading.Thread(target=listings_remove.start_listing_remove);
threads.append(listing_remove_thread)

sales_add_thread                = threading.Thread(target=sales_add.start_sales_add);
threads.append(sales_add_thread)

#sales_remove_thread            = threading.Thread(target=sales_remove.start_sales_remove);
#threads.append(sales_remove_thread)

db_cleaning_thread              = threading.Thread(target=db_cleaning.start_cleaning);
threads.append(db_cleaning_thread)


def start_threads(threads):
    for x in threads:
        try:
            x.start()
        except Exception as thread_error:
            try:
                logs.add_error("THREAD_ERROR: ", thread_error);
            except Exception as log_error:
                logs.add_error("LOG ERROR: ", log_error);


start_threads(threads)
print("ALL THREADS STARTED")