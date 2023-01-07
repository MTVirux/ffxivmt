import threading
import external
import time
import log
import pprint
import math
import sales_add
import threading
from ratelimit import limits, sleep_and_retry
import time
import queue
import requests
import os


@sleep_and_retry
@limits(calls=25, period=1)
def send_request(url):
    log.request(f"Sending request --- {url}")
    response = requests.get(url)
    return response
    

def request_handler(url):
    global db_threads
    global external_item_name_dict
    response = send_request(url);
    if(response.status_code == 200):
        #spawn thread to add sales
        thread = threading.Thread(target=sales_add.parse, daemon=True, args=(response.json(), url, external_item_name_dict))
        db_threads.append(thread)
        thread.start()
    else:
        #Retry if error status code
        log.error("Response status code: " + str(response.status_code) + " [Retrying] --- " + str(url))
        request_handler(url)


def watcher():

    print("entered watcher thread")

    global req_threads
    global db_threads
    global url_queue

    while len(req_threads) > 0 or len(db_threads) > 0 or url_queue.qsize() > 0:
        #write status to file
        file = open("../IMPORT.status", "w")
        file.write("Req threads: " + str(len(req_threads)))
        file.write('\n')
        file.write("DB threads: " + str(len(db_threads)))
        file.write('\n')
        file.write("Queue size: " + str(url_queue.qsize()))
        file.write('\n')
        file.close()
        time.sleep(0.1)
    
    print("IMPORT FINISHED - WATCHER EXITING")
    file.write
    


#   
#   IMPORT SALES
#   

query_list = {}
url_list = []
req_threads = []
db_threads = []
url_queue = queue.Queue()
max_request_threads = 25
max_db_threads = 50 #Each thread should consume about 50 ~ 100 MB of RAM

#Create a IMPORT.status file if it doesn't exist
if not os.path.exists("../IMPORT.status"):
    open("../IMPORT.status", "w").close()

os.chmod('../IMPORT.status', 0o666)

watcher_thread = threading.Thread(target=watcher, daemon=True)




#Make combos of region and item id

external_region_list = external.get_region_list()
external_item_id_list = external.get_item_id_list()
external_item_name_dict = external.get_item_name_dict()


for region in external_region_list:
    query_list[region] = []
    chunk_size = 10
    # Split the sorted item id list into sublists of {chunk_size} item ids
    for i in range(0, len(external_item_id_list), chunk_size):
        sublist = external_item_id_list[i:i+chunk_size]
        query_list[region].append(sublist)

#Make url list
for region in query_list:
    for item_id_list in query_list[region]:
        item_id_str = ",".join(str(i) for i in item_id_list)
        url = f"https://universalis.app/api/v2/history/{region}/{item_id_str}?entriesToReturn=999999&statsWithin=7776000000&entriesWithin=7776000000"
        url_list.append(url)

# Add the urls to the queue
for url in url_list:
    url_queue.put(url)

# start watcher thread after url queue is populated 
# as to have a condition to keep it running
watcher_thread.start()

# While url queue is not empty or there are active threads
while not url_queue.empty() or len(req_threads) > 0:

    # Start a new thread if there are fewer than `max_request_threads` active threads
    if (len(req_threads) < max_request_threads) and ((len(db_threads) + len(req_threads)) < max_db_threads):
      url = url_queue.get()
      thread = threading.Thread(target=request_handler, args=(url,))
      thread.start()
      req_threads.append(thread)
    else:
        # Wait for a thread to finish before starting a new one
        for thread in req_threads:
          if not thread.is_alive():
            req_threads.remove(thread)

        for thread in db_threads:
            if not thread.is_alive():
              db_threads.remove(thread)


# Wait for all request threads to finish
for thread in req_threads:
    thread.join()

# Wait for all db threads to finish
for thread in db_threads:
    thread.join()
