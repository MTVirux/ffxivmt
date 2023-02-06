from pprint import pprint
import requests
import config
import log
import metrics
import database
from cassandra.query import dict_factory
import json
import queue
from math import floor
from time import time

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


def send_sales_to_php(response_item):
    try:
        global FAILED_REQUEST_URLS
        metrics.LAST_RESPONSE = response_item["json"]
        headers = {'Content-type': 'application/json'}
        response = requests.post("http://" + config.BACKEND_HOST_CONTAINER + "/api/v1/updatedb/python_request", json=json.loads(response_item["json"]), headers=headers)
        url = response_item["url"];
        try:
            json.loads(response.text)["data"]["parsed_sales"]
            json.loads(response.text)["data"]["time"]
        except Exception as e:
            log.error(e)
            log.error(f"PHP response error --- {response.text}")
            FAILED_REQUEST_URLS.put(url)

    except e as Exception:
        log.error(e)
        log.error(f"Error sending sales to PHP --- {response_item['json']}")
        FAILED_REQUEST_URLS.put(url)
        return
        
    if(response.status_code == 200):
        try:
            metrics.PHP_REQUESTS_COMPLETED += 1
            log.action(response.text)
            metrics.TOTAL_SALES_PARSED += int(json.loads(response.text)["data"]["parsed_sales"])
        except Exception as e:
            log.error(e)
            log.error(f"Error parsing response --- {response.text}")
            FAILED_REQUEST_URLS.put(url)
    else:
        print("REQUEST FAILED")
        metrics.PHP_REQUESTS_FAILED += 1
        FAILED_REQUEST_URLS.put(url)
        log.error(f"Request failed --- {response.status_code} --- {response.text}")

FAILED_REQUEST_URLS = queue.Queue()

def get_current_timestamp_ms():
    return int(floor(time() * 1000))