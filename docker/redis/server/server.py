from base64 import decode
from posixpath import split
import websocket
import bson
import redis
import json

UNIVERSALLIS_SOCKET = "wss://universalis.app/api/ws"

db = redis.Redis(host='localhost', port=6379, db=0)

#ADD TO REDIS DB AS JSON
def handle_add(hash, listing):
    listingID = listing["listingID"]
    
    #Must have valid ID
    if(type(listingID) != str):
        return

    print("Add: ", listingID)
    if(redis.Redis.hset(hash, listingID, listing) == 1):
        print("Added " + listingID + " to " + hash)
    return


#REMOVE FROM REDIS DB
def handle_remove(hash, listing):
    listingID = listing["listingID"]
    
    #Must have valid ID
    if(type(listingID) != str):
        return
    if(listingID == "df05cb60221600bc3b26823e170b3cc8f9dcd5e7c7813676e3f3323c7d04384c"):
        return

    print("Remove: ", listingID)
    if(db.hdel(hash, listingID) == 1):
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

    hash = item + "_" + world

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
    print("Sending subscribe event")
    wsapp.send(bson.encode({"event": "subscribe", "channel": "listings/add"}))
    print("Sent subscribe event for add")
    wsapp.send(bson.encode({"event": "subscribe", "channel": "listings/remove"}))
    print("Sent subscribe event for remove")




wsapp = websocket.WebSocketApp(
    UNIVERSALLIS_SOCKET, on_open=subscribe, on_message=on_message)
wsapp.run_forever()
