import websocket
import pprint
import bson

UNIVERSALLIS_SOCKET = "wss://universalis.app/api/ws"


def on_message(wsapp, message):
    pprint.pprint(bson.decode(message))


def subscribe(wsapp):
    print("Sending subscribe event")
    print("test", bson.encode({"event": "subscribe", "channel": "listings/add"}))
    wsapp.send(bson.encode({"event": "subscribe", "channel": "listings/add"}))
    print("Sent subscribe event")



bson.encode({"event": "subscribe", "channel": "listings/add"})
wsapp = websocket.WebSocketApp(
    UNIVERSALLIS_SOCKET, on_open=subscribe, on_message=on_message)
wsapp.run_forever()
