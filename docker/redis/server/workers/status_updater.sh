#!/bin/bash

if [ -z "$1" ]; then
  echo "Must specify number of lines to show"
  exit 1
fi

NUMBER_OF_LINES=$1
LAST_LISTINGS_ADD_FILE=$(ls /server/logs/action -1|grep listings_add|tail -n 1)
LAST_LISTINGS_REMOVE_FILE=$(ls /server/logs/action -1|grep listings_remove|tail -n 1)
LAST_SALES_ADD_FILE=$(ls /server/logs/action -1|grep sales_add|tail -n 1)

mkdir -p /server/logs/status

while true; do

if [ -f "/server/logs/action/$LAST_LISTINGS_ADD_FILE" ]; then
    tail -n $NUMBER_OF_LINES "/server/logs/action/$LAST_LISTINGS_ADD_FILE" > /server/logs/status/listings_add.status
fi
if [ -f "/server/logs/action/$LAST_LISTINGS_REMOVE_FILE" ]; then
    tail -n $NUMBER_OF_LINES "/server/logs/action/$LAST_LISTINGS_REMOVE_FILE" > /server/logs/status/listings_remove.status
fi
if [ -f "/server/logs/action/$LAST_SALES_ADD_FILE" ]; then
    tail -n $NUMBER_OF_LINES "/server/logs/action/$LAST_SALES_ADD_FILE" > /server/logs/status/sales_add.status
fi
if [ -f "/server/logs/redis_memory_cleaner.log" ]; then
    tail -n $NUMBER_OF_LINES "/server/logs/redis_memory_cleaner.log" > /server/logs/status/redis_memory_cleaner.status
fi

sleep ${REDIS_STATUS_UPDATER_INTERVAL}
    
done
