from json import JSONDecoder, decoder
from logging import exception
from sqlite3 import Timestamp
import sys
import time
import websocket
import json
import pprint
import numpy
from binance.enums import *
from binance.client import Client
import threading
import bson
import json_util


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
