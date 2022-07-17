import websocket
import pprint
import bson


UNIVERSALLIS_SOCKET = "wss://universalis.app/api/ws"


def on_message(wsapp, message):
    pprint.pprint(bson.loads(message))


def subscribe(wsapp):
    print("Sending subscribe event")
    wsapp.send(bson.dumps({"event": "subscribe", "channel": "listings/add"}))
    print("Sent subscribe event")



wsapp = websocket.WebSocketApp(
    UNIVERSALLIS_SOCKET, on_open=subscribe, on_message=on_message)
wsapp.run_forever()
