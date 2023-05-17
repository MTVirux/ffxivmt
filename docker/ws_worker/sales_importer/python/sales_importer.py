import external
import log
import metrics
import config

import queue
import requests
import time
import datetime
import threading
from ratelimit import limits, sleep_and_retry
import os
import math
import json

@sleep_and_retry
@limits(calls=25, period=1)
def send_request(url):
    global response_queue
    log.request(f"Sending request --- {url}")
    response = requests.get(url)
    if(response.status_code == 200 and response.text != ""):
        log.action(f"Request success --- {url}")
        response_queue.put({"url": url, "json":response.text})
        return True
    else:
        #log.error(f"Request failed --- {response.status_code} --- RETRYING: {url}")
        external.FAILED_REQUEST_URLS.put(url)

def watcher():

    #Create a IMPORT.status file if it doesn't exist
    if not os.path.exists("../IMPORT.status"):
        open("../IMPORT.status", "w").close()

    os.chmod('../IMPORT.status', 0o666)


    global query_list
    global req_threads
    global php_request_threads
    global url_queue
    global response_queue
    global php_concurrent_request_limit
    global items_per_request

    while (url_queue.qsize() + response_queue.qsize() + len(req_threads) + len(php_request_threads) + external.FAILED_REQUEST_URLS.qsize()) > 0:
        #write status to file
        file = open("../IMPORT.status", "w")
        file.write("Queue size: " + str(url_queue.qsize()) + " ("+str(items_per_request)+" items per request)")
        file.write('\n')
        file.write("Universalis threads: " + str(len(req_threads)))
        file.write('\n')
        file.write("PHP request queue size: " + str(response_queue.qsize()))
        file.write('\n')
        file.write("PHP request threads: " + str(len(php_request_threads)))
        file.write('\n')
        file.write("PHP requests completed: " + str(metrics.PHP_REQUESTS_COMPLETED) + " / " + str(metrics.TOTAL_REQUESTS))
        file.write('\n')
        file.write("Sales parsed: " + str(metrics.TOTAL_SALES_PARSED))
        file.write('\n')
        file.write("Parity check: " + str((int(metrics.PHP_REQUESTS_COMPLETED) + int(len(req_threads)) + int(len(php_request_threads)) + int(response_queue.qsize()) + int(url_queue.qsize())) == int(metrics.TOTAL_REQUESTS)))
        file.write('\n')
        file.write("Requests retried: " + str(metrics.RETRIED_REQUESTS))
        file.write('\n')

        try:
            current_time = datetime.datetime.now().timestamp()
            if(metrics.TOTAL_SALES_PARSED > 0 and metrics.PHP_REQUESTS_COMPLETED > 0):
                time_per_sale = (time.time() - metrics.START_TIME) / metrics.TOTAL_SALES_PARSED
                average_sale_per_request = metrics.TOTAL_SALES_PARSED / metrics.PHP_REQUESTS_COMPLETED
                expected_completion_time = time_per_sale * average_sale_per_request * (metrics.TOTAL_REQUESTS - metrics.PHP_REQUESTS_COMPLETED)
                time_remaining = datetime.timedelta(seconds=expected_completion_time)
                file.write("ETA: " + str(time_remaining))
        except Exception as e:
            file.write("ETA: N/A")

        file.write('\n')
        file.write('\n')
        file.write("------ QUEUE MONITOR ------")
        file.write('\n')
        file.write('\n')
        if(url_queue.qsize() > 0):
            file.write("Next in URL queue: ")
            file.write('\n')
            file.write(str(url_queue.queue[0]))
            file.write('\n')
            file.write('\n')
        file.write('Last response sent to PHP:')
        file.write('\n')
        file.write(str(metrics.LAST_RESPONSE))
        file.write('\n')
        file.write('\n')
        if(response_queue.qsize() > 0):
            file.write("Next in PHP request queue: ")
            file.write('\n')
            file.write(str(response_queue.queue[0]["json"]))
            file.write('\n')
            file.write('\n')
        file.close()
        time.sleep(1)
    
    print("IMPORT FINISHED - WATCHER EXITING")
    file.write



#   
#   IMPORT SALES
#   

url_list = []
query_list = {}
req_threads = []
php_request_threads = []

url_queue = queue.Queue()
response_queue = queue.Queue()

items_per_request = 2

response_queue_limit = 10
max_request_threads = 25
php_concurrent_request_limit = 10

#Make combos of region and item id
external_world_list = external.get_world_list()
external_item_id_list = external.get_item_id_list()
external_item_name_dict = external.get_item_name_dict()


for world in external_world_list:
    query_list[world] = []
    chunk_size = items_per_request
    # Split the sorted item id list into sublists of {chunk_size} item ids
    for i in range(0, len(external_item_id_list), chunk_size):
        sublist = external_item_id_list[i:i+chunk_size]
        query_list[world].append(sublist)

#Make url list
for region in query_list:
    for item_id_list in query_list[region]:
        item_id_str = ",".join(str(i) for i in item_id_list)
        entries_to_return = str(config.ENTRIES_TO_RETURN) #(max 999999)
                
        if(config.IMPORT_ALL_TIME == True):
            current_timestamp_ms = str(math.floor(time.time()*1000))
        else:
            current_timestamp_ms = str(config.TIME_AGO_TO_IMPORT_SALES)
        
        
        url = f"{config.UNIVERSALIS_URL}{config.UNIVERSALIS_SALES_ENDPOINT}{region}/{item_id_str}?entriesToReturn={entries_to_return}&statsWithin={current_timestamp_ms}&entriesWithin={current_timestamp_ms}"
        url_list.append(url)

# Add the urls to the queue
for url in url_list:
    url_queue.put(url)

metrics.TOTAL_REQUESTS = url_queue.qsize()

# Start the watcher thread
t = threading.Thread(target=watcher)
t.start()

while((url_queue.qsize() + response_queue.qsize() + len(req_threads) + len(php_request_threads) + external.FAILED_REQUEST_URLS.qsize()) > 0):

    if(external.FAILED_REQUEST_URLS.qsize() > 0):
        for i in range(external.FAILED_REQUEST_URLS.qsize()):
            url_queue.put(external.FAILED_REQUEST_URLS.get())
            metrics.RETRIED_REQUESTS += 1

    if(len(req_threads) < max_request_threads and ((response_queue.qsize() + len(req_threads)) < response_queue_limit)):
        t = threading.Thread(target=send_request, args=(url_queue.get(),))
        req_threads.append(t)
        t.start()
    
    elif(response_queue.qsize() > 0 and len(php_request_threads) < php_concurrent_request_limit):

        t = threading.Thread(target=external.send_sales_to_php, args=(response_queue.get(),))
        php_request_threads.append(t)
        t.start()
        pass
    
    else:
        # Wait for a thread to finish before starting a new one
        for thread in req_threads:
            if not thread.is_alive():
                req_threads.remove(thread)

        for thread in php_request_threads:
            if not thread.is_alive():
                php_request_threads.remove(thread)

while len(req_threads) > 0 or len(php_request_threads) > 0:
    for thread in req_threads:
        if not thread.is_alive():
            req_threads.remove(thread)

    for thread in php_request_threads:
        if not thread.is_alive():
            php_request_threads.remove(thread)