from base64 import decode
from posixpath import split
import threading
import listings_add
import listings_remove
import sales_add
import db_cleaning
import log
import database


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

listing_remove_thread           = threading.Thread(target=listings_remove.start_listing_remove);
threads.append(listing_remove_thread)

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

start_threads(threads)
log.debug("ALL THREADS STARTED")