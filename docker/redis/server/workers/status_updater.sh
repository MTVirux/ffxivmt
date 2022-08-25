#!/bin/bash

if [ -z "$1" ]; then
    NUMBER_OF_ACTION_LINES=10
else
    NUMBER_OF_ACTION_LINES=$1
fi

if [ -z "$2" ]; then
    NUMBER_OF_DEBUG_LINES=10
else
    NUMBER_OF_DEBUG_LINES=$2
fi

if [ -z "$3" ]; then
    NUMBER_OF_ERROR_LINES=10
else
    NUMBER_OF_ERROR_LINES=$3
fi

if [ -z "$4" ]; then
    NUMBER_OF_OTHER_LINES=1
else
    NUMBER_OF_OTHER_LINES=$4
fi


SALES_ADD_LOG_FILE=/server/logs/action/$(ls /server/logs/action -1|grep sales_add|tail -n 1)
SALES_ADD_ERROR_LOG_FILE=/server/logs/error/$(ls /server/logs/error -1|grep sales_add|tail -n 1)
SERVER_DEBUG_LOG_FILE=/server/logs/$(ls /server/logs -1|grep debug|tail -n 1)
MEMORY_CLEANER_LOG_FILE=/server/logs/$(ls /server/logs -1|grep redis_memory_cleaner|tail -n 1)

touch ${REDIS_SERVER_LOGS_DIR}/status/action.status
touch ${REDIS_SERVER_LOGS_DIR}/status/error.status
touch ${REDIS_SERVER_LOGS_DIR}/status/debug.status
touch ${REDIS_SERVER_LOGS_DIR}/status/other.status


ACTION_FILES_TO_TRACK=$SALES_ADD_LOG_FILE
ERROR_FILES_TO_TRACK=$SALES_ADD_ERROR_LOG_FILE
DEBUG_FILES_TO_TRACK=$SERVER_DEBUG_LOG_FILE
OTHER_FILES_TO_TRACK=$MEMORY_CLEANER_LOG_FILE

mkdir -p ${REDIS_SERVER_LOGS_DIR}

#for each of the files in ACTION_FILES_TO_TRACK
#check if the file exists
#if it exists grab the name of the file and the last line of the file



ACTION_STATUS_FILE="${REDIS_SERVER_LOGS_DIR}/status/action.status"
ERROR_STATUS_FILE="${REDIS_SERVER_LOGS_DIR}/status/error.status"
DEBUG_STATUS_FILE="${REDIS_SERVER_LOGS_DIR}/status/debug.status"
OTHER_STATUS_FILE="${REDIS_SERVER_LOGS_DIR}/status/other.status"


echo "----- ACTIONS -----"  > $ACTION_STATUS_FILE
echo "----- ERRORS  -----"  > $ERROR_STATUS_FILE
echo "-----  DEBUG  -----"   > $DEBUG_STATUS_FILE
echo "-----  OTHER  -----"   > $OTHER_STATUS_FILE




while true; do

    for file in ${ACTION_FILES_TO_TRACK[@]}; do
        if [ -f $file ]; then
            #get just the name of the file
            file_name=$(basename $file)
            #replace .log extension in file name with .status extension
            status_file_name=${file_name%.log}.status
            LAST_ACTION_LINE=$(tail -n 1 ${REDIS_SERVER_LOGS_DIR}/action/$file_name)
            LAST_STATUS_LINE=$(tail -n 1 ${REDIS_SERVER_LOGS_DIR}/status/$status_file_name)
            if [ "$LAST_ACTION_LINE" != "$LAST_STATUS_LINE" ]; then
                echo "$LAST_ACTION_LINE" >> ${REDIS_SERVER_LOGS_DIR}/status/$status_file_name
                echo ["$status_file_name"]"$LAST_ACTION_LINE" >> $ACTION_STATUS_FILE
            fi            
        fi
    done

    for file in ${ERROR_FILES_TO_TRACK[@]}; do
        if [ -f $file ]; then
            #get just the name of the file
            file_name=$(basename $file)
            #replace .log extension in file name with .status extension
            status_file_name=${file_name%.log}.status
            LAST_ERROR_LINE=$(tail -n 1 ${REDIS_SERVER_LOGS_DIR}/error/$file_name)
            LAST_STATUS_LINE=$(tail -n 1 ${REDIS_SERVER_LOGS_DIR}/status/$status_file_name)
            if [ "$LAST_ERROR_LINE" != "$LAST_STATUS_LINE" ]; then
                echo "$LAST_ERROR_LINE" >> ${REDIS_SERVER_LOGS_DIR}/status/$status_file_name
                echo ["$status_file_name"]"$LAST_ERROR_LINE" >> $ERROR_STATUS_FILE
            fi
        fi
    done

    for file in ${DEBUG_FILES_TO_TRACK[@]}; do
        if [ -f $file ]; then
            #get just the name of the file
            file_name=$(basename $file)
            #replace .log extension in file name with .status extension
            status_file_name=${file_name%.log}.status
            LAST_DEBUG_LINE=$(tail -n 1 ${REDIS_SERVER_LOGS_DIR}/$file_name)
            LAST_STATUS_LINE=$(tail -n 1 ${REDIS_SERVER_LOGS_DIR}/status/$status_file_name)
            if [ ["$status_file_name"]"$LAST_DEBUG_LINE" != "$LAST_STATUS_LINE" ]; then
                echo ["$status_file_name"]"$LAST_DEBUG_LINE" >> ${REDIS_SERVER_LOGS_DIR}/status/$status_file_name
            fi
        fi
    done

    for file in ${OTHER_FILES_TO_TRACK[@]}; do
        if [ -f $file ]; then
            #get just the name of the file
            file_name=$(basename $file)
            #replace .log extension in file name with .status extension
            status_file_name=${file_name%.log}.status
            LAST_OTHER_LINE=$(tail -n 1 ${REDIS_SERVER_LOGS_DIR}/$file_name)
            LAST_STATUS_LINE=$(tail -n 1 ${REDIS_SERVER_LOGS_DIR}/status/$status_file_name)
            if [ "$LAST_OTHER_LINE" != "$LAST_STATUS_LINE" ]; then
                echo "$LAST_OTHER_LINE" >> ${REDIS_SERVER_LOGS_DIR}/status/$status_file_name
                echo ["$status_file_name"]"$LAST_OTHER_LINE" >> $OTHER_STATUS_FILE
            fi
        fi
    done

    sleep ${REDIS_STATUS_UPDATER_INTERVAL}

done



