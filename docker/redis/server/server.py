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

listing_add_thread              = threading.Thread(target=listings_add.start_listing_add);
threads.append(listing_add_thread)

#listing_remove_thread           = threading.Thread(target=listings_remove.start_listings_remove);
#threads.append(listing_remove_thread)

sales_add_thread                = threading.Thread(target=sales_add.start_sales_add);
threads.append(sales_add_thread)

#sales_remove_thread            = threading.Thread(target=sales_remove.start_sales_remove);
#threads.append(sales_remove_thread)

#db_cleaning_thread              = threading.Thread(target=db_cleaning.start_cleaning);
#threads.append(db_cleaning_thread)

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

def check_thread_alive(thr):
    try:
        thr.join(timeout=0.0)
        return thr.is_alive()
    except Exception as thread_error:
        return False

def keep_alive(threads):
    while True:
        for x in threads:
            if(check_thread_alive(x) == False):
                log.debug("THREAD_DIED " + str(x) + " HAS DIED")
                threads.remove(x)
                threads.append(threading.Thread(target=x))
                log.debug("THREAD_RESTART " + str(x) + " HAS BEEN RESTARTED")
        time.sleep(1)

start_threads(threads)
log.debug("ALL THREADS STARTED")

#keep_alive(threads);

join_threads(threads)
log.debug("ALL THREADS JOINED")