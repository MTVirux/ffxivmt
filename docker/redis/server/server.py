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


#ADD TO REDIS DB AS JSON
def handle_add(hash, listing):
    listingID = listing["listingID"]
    
    #Must have valid ID
    if(type(listingID) != str):
        return
    if(listingID == "5feceb66ffc86f38d952786c6d696c79c2dbc239dd4e91b46729d73a27fb57e9"):
        return

    #print("Add: ", listingID)
    if(listing_db.hset(str(hash), str(listingID), str(listing)) == 1):
        print("Added " + listingID + " to " + hash)
    return


#REMOVE FROM REDIS DB
def handle_remove(db, hash, listing):
    listingID = listing["listingID"]
    
    #Must have valid ID
    if(type(listingID) != str):
        return
    if(listingID == "5feceb66ffc86f38d952786c6d696c79c2dbc239dd4e91b46729d73a27fb57e9"):
        return

    #print("Remove: ", listingID)
    if(listing_db.hdel(str(hash), str(listingID)) == 1):
        print("Removed " + listingID + " from " + hash)
    return


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

    #print (json_listing[0])
    #exit()
    #print(json_listing)

    #for i in json_listing:
    #    print(i);exit()
    #
    #exit()
    #for i in (decoded_message):
    #    print(i , " : ", decoded_message[i])
    #exit()
    #redis.Redis.hset(decoded_message['item']+"_"+decoded_message['world'], "test", "test")



def subscribe(wsapp):

    wsapp.send(bson.encode({"event": "subscribe", "channel": "sales/add"}))
    print("Sent subscribe event for sales/add")
    wsapp.send(bson.encode({"event": "subscribe", "channel": "sales/remove"}))
    print("Sent subscribe event for sales/remove")




threads = []

listing_add_thread              = threading.Thread(target=listings_add.start_listing_add);
threads.append(listing_add_thread)

listing_remove_thread           = threading.Thread(target=listings_remove.start_listing_remove);
threads.append(listing_remove_thread)

sales_add_thread               = threading.Thread(target=sales_add.start_sales_add);
threads.append(sales_add_thread)

#sales_remove_thread            = threading.Thread(target=sales_remove.start_sales_remove);
#threads.append(sales_remove_thread)



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