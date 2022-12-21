from base64 import decode
from posixpath import split
import websocket
import bson
import json
import config
import database
import log
import pprint


def remove_entry(hash):
    if(database.DB_LISTINGS.json().delete(hash) == 1):
        log.action("{"+ str(config.REDIS_LISTINGS_DB) + "}{JSON_DEL}Deleted listings for key " + hash)
        return
    else:
        log.error("[ERROR]{"+ str(config.REDIS_LISTINGS_DB) + "}{JSON_DEL} Could not delete " + hash + " from db")