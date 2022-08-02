import redis
import config
import threading
import database
import datetime
import time




def clean_listings():
    keys = database.DB_LISTINGS_CLEAN.keys();
    ttl_seconds = config.HASH_FIELD_TTL
    ttl_days = config.HASH_FIELD_TTL/86400

    for i in keys:
        key = str(i).strip("b").strip("\'")
        clean_key = int(str(i).strip("b").strip("\'").split(".")[0])
        key_expire_time_date = datetime.datetime.fromtimestamp(clean_key)+ datetime.timedelta(days=ttl_days)
        key_expire_time_timestamp = key_expire_time_date.timestamp()
        if(key_expire_time_timestamp < time.time()):
            fields = database.DB_LISTINGS_CLEAN.hgetall(key)
            for k in fields:
                clean_field = str(k).strip("b").strip("\'")
                print("Removing: " + key + clean_field)
                if(database.DB_LISTINGS_CLEAN.hdel(key, clean_field) == 1):
                    continue
    return
    
    return



def clean_sales():
    keys = database.DB_SALES_CLEAN.keys();
    ttl_seconds = config.HASH_FIELD_TTL
    ttl_days = config.HASH_FIELD_TTL/86400

    for i in keys:
        key = str(i).strip("b").strip("\'")
        clean_key = int(str(i).strip("b").strip("\'").split(".")[0])
        key_expire_time_date = datetime.datetime.fromtimestamp(clean_key)+ datetime.timedelta(days=ttl_days)
        key_expire_time_timestamp = key_expire_time_date.timestamp()
        if(key_expire_time_timestamp < time.time()):
            fields = database.DB_SALES_CLEAN.hgetall(key)
            for k in fields:
                clean_field = str(k).strip("b").strip("\'")
                print("Removing: " + key + clean_field)
                if(database.DB_SALES_CLEAN.hdel(key, clean_field) == 1):
                    continue
    return



def start_cleaning():
    while(True):

        cleaning_threads = []
        clean_listings_thread = threading.Thread(target = clean_listings)
        cleaning_threads.append(clean_listings_thread)
        clean_sales_thread = threading.Thread(target = clean_sales)
        cleaning_threads.append(clean_sales_thread)

        for x in cleaning_threads:
            x.start();
        
        for x in cleaning_threads:
            x.join();

        time.sleep(config.HASH_FIELD_TTL)