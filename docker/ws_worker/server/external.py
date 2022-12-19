from pprint import pprint
import requests
import config
import log
import pprint

def warn_backend_to_update_item(world_name, item_id):
    #Split hash into item_id and world_name

    req = requests.post("http://" + config.BACKEND_HOST_CONTAINER + "/test/python_update", data={"item_id": item_id, "world_name": world_name})
    if(req.status_code == 200):
        log.action("Backend updated item " + hash)
    else:
        log.error("Backend could not update item " + hash)

    pass
