from base64 import decode
from posixpath import split
import threading
import listings_add
import listings_remove
import sales_add
import log
import database
import time


#/Item_world/-----//
#                  /listingID : {[JSON_DATA]}
#                  /
#                  /listingID : {[JSON_DATA]}
#                  /
#                  /listingID : {[JSON_DATA]}
#                  /
#                  /listingID : {[JSON_DATA]}
#
#
#/Item_world/-----//
#                  /listingID : {[JSON_DATA]}
#                  /
#                  /listingID : {[JSON_DATA]}
# 


threads = []

sales_add_thread                = threading.Thread(target=sales_add.start_sales_add);
threads.append(sales_add_thread)


def start_threads(threads):
    for x in threads:
        try:
            x.start()
        except Exception as thread_error:
            log.error("THREAD FAILED TO START" + str(thread_error))

def join_threads(threads):
    for x in threads:
        try:
            x.join()
        except Exception as thread_error:
            log.error("THREAD FAILED TO JOIN" + str(thread_error))


start_threads(threads)
log.debug("ALL THREADS STARTED")

#keep_alive(threads);

join_threads(threads)
log.debug("ALL THREADS JOINED")