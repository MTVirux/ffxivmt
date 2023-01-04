from pprint import pprint
import requests
import config
import log
import pprint
import database
from cassandra.query import dict_factory

def warn_backend_to_update_item(world_name, item_id):
    #Split hash into item_id and world_name

    req = requests.post("http://" + config.BACKEND_HOST_CONTAINER + "/test/python_update", data={"item_id": item_id, "world_name": world_name})
    if(req.status_code == 200):
        log.action("Backend updated item " + hash)
    else:
        log.error("Backend could not update item " + hash)

    pass

def get_item_name_dict():
    result = database.SCYLLA_DB.execute("SELECT id, name FROM items");
    item_name_dict = {}
    for row in result:
        item_name_dict[row.id] = row.name
    
    return item_name_dict

ITEM_NAME_DICT = get_item_name_dict()