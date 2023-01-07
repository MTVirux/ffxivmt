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

def get_item_id_list():
    result = database.SCYLLA_DB.execute("SELECT id FROM items WHERE marketable = true");
    item_id_list = []
    for row in result:
        item_id_list.append(row.id)
    
    return item_id_list

ITEM_ID_LIST = sorted(get_item_id_list(), reverse=True)

def get_region_list():
    result = database.SCYLLA_DB.execute("SELECT region FROM worlds");
    region_list = []
    for row in result:
        if row.region not in region_list:
            region_list.append(row.region)
    return region_list

REGION_LIST = get_region_list()

def get_item_name_dict():
    result = database.SCYLLA_DB.execute("SELECT id, name FROM items WHERE marketable = true");
    item_name_dict = {}
    for row in result:
        item_name_dict[row.id] = row.name
    
    return item_name_dict

ITEM_NAME_DICT = get_item_name_dict()