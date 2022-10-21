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


SALES_ADD_LOG_FILE=${REDIS_SERVER_LOGS_DIR}/action/$(ls ${REDIS_SERVER_LOGS_DIR}/action -1|grep sales_add|tail -n 1)
SALES_ADD_ERROR_LOG_FILE=${REDIS_SERVER_LOGS_DIR}/error/$(ls ${REDIS_SERVER_LOGS_DIR}/error -1|grep sales_add|tail -n 1)
LISTINGS_ADD_LOG_FILE=${REDIS_SERVER_LOGS_DIR}/action/$(ls ${REDIS_SERVER_LOGS_DIR}/action -1|grep listings_add|tail -n 1)
LISTINGS_ADD_ERROR_LOG_FILE=${REDIS_SERVER_LOGS_DIR}/error/$(ls ${REDIS_SERVER_LOGS_DIR}/error -1|grep listings_add|tail -n 1)
LISTINGS_REMOVE_LOG_FILE=${REDIS_SERVER_LOGS_DIR}/action/$(ls ${REDIS_SERVER_LOGS_DIR}/action -1|grep listings_remove|tail -n 1)
LISTINGS_REMOVE_ERROR_LOG_FILE=${REDIS_SERVER_LOGS_DIR}/error/$(ls ${REDIS_SERVER_LOGS_DIR}/error -1|grep listings_remove|tail -n 1)
SERVER_DEBUG_LOG_FILE=${REDIS_SERVER_LOGS_DIR}/$(ls ${REDIS_SERVER_LOGS_DIR} -1|grep debug|tail -n 1)
MEMORY_CLEANER_LOG_FILE=${REDIS_SERVER_LOGS_DIR}/system/$(ls ${REDIS_SERVER_LOGS_DIR}/system -1|grep redis_memory_cleaner|tail -n 1)
CPU_LOG_FILE=${REDIS_SERVER_LOGS_DIR}/system/$(ls ${REDIS_SERVER_LOGS_DIR}/system -1|grep cpu|tail -n 1)

touch ${REDIS_SERVER_LOGS_DIR}/status/action.status
touch ${REDIS_SERVER_LOGS_DIR}/status/error.status
touch ${REDIS_SERVER_LOGS_DIR}/status/debug.status
touch ${REDIS_SERVER_LOGS_DIR}/status/other.status


ACTION_FILES_TO_TRACK=($SALES_ADD_LOG_FILE $LISTINGS_ADD_LOG_FILE)
ERROR_FILES_TO_TRACK=($SALES_ADD_ERROR_LOG_FILE $LISTINGS_ADD_ERROR_LOG_FILE)
DEBUG_FILES_TO_TRACK=$SERVER_DEBUG_LOG_FILE
OTHER_FILES_TO_TRACK=($MEMORY_CLEANER_LOG_FILE $CPU_LOG_FILE $REDIS_STATS_LOG_FILE)

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

    ITERATOR=0
    for file in ${ACTION_FILES_TO_TRACK[@]}; do
        if [ -f $file ]; then
            #get just the name of the file
            FILE_NAME=$(basename $file)
            #file name without extension
            FILE_NAME_NO_EXTENSION=${FILE_NAME%.*}
            FILE_NAME_NO_EXTENSION=$(echo $FILE_NAME_NO_EXTENSION | sed 's/[^a-zA-Z0-9]//g')
            #replace .log extension in file name with .status extension
            STATUS_FILE_NAME=${FILE_NAME%.log}.status
            STATUS_FILE=${REDIS_SERVER_LOGS_DIR}/status/$STATUS_FILE_NAME
            if [ ! -f $STATUS_FILE ]; then
                touch $STATUS_FILE
                STATUS_FILE=${REDIS_SERVER_LOGS_DIR}/status/$STATUS_FILE_NAME
            fi
            LAST_ACTION_LINE=$(tail -n 1 $file)
            LAST_STATUS_LINE=$(tail -n 1 ${REDIS_SERVER_LOGS_DIR}/status/$STATUS_FILE_NAME)
            if [ "$LAST_ACTION_LINE" != "$LAST_STATUS_LINE" ]; then
                tail -n $NUMBER_OF_ACTION_LINES ${REDIS_SERVER_LOGS_DIR}/action/$FILE_NAME > $STATUS_FILE
                ACTION_OUTPUT_ARRAY[$ITERATOR]=$(tail -n $NUMBER_OF_ACTION_LINES "$file")
            fi
            ITERATOR=$((ITERATOR+1))
        fi
    done

    ITERATOR=0
    for file in ${DEBUG_FILES_TO_TRACK[@]}; do
        if [ -f $file ]; then
            #get just the name of the file
            FILE_NAME=$(basename $file)
            #file name without extension
            FILE_NAME_NO_EXTENSION=${FILE_NAME%.*}
            FILE_NAME_NO_EXTENSION=$(echo $FILE_NAME_NO_EXTENSION | sed 's/[^a-zA-Z0-9]//g')
            #replace .log extension in file name with .status extension
            STATUS_FILE_NAME=${FILE_NAME%.log}.status
            STATUS_FILE=${REDIS_SERVER_LOGS_DIR}/status/$STATUS_FILE_NAME
            if [ ! -f $STATUS_FILE ]; then
                touch $STATUS_FILE
                STATUS_FILE=${REDIS_SERVER_LOGS_DIR}/status/$STATUS_FILE_NAME
            fi
            LAST_DEBUG_LINE=$(tail -n 1 $file)
            LAST_STATUS_LINE=$(tail -n 1 ${REDIS_SERVER_LOGS_DIR}/status/$STATUS_FILE_NAME)
            if [ ["$STATUS_FILE_NAME"]"$LAST_DEBUG_LINE" != "$LAST_STATUS_LINE" ]; then
                tail -n $NUMBER_OF_DEBUG_LINES ${REDIS_SERVER_LOGS_DIR}/$FILE_NAME > $STATUS_FILE
                DEBUG_OUTPUT_ARRAY["$ITERATOR"]=$(tail -n $NUMBER_OF_DEBUG_LINES "$file")
            fi
            ITERATOR=$((ITERATOR+1))
        fi
    done

    ITERATOR=0
    for file in ${ERROR_FILES_TO_TRACK[@]}; do
        if [ -f $file ]; then
            #get just the name of the file
            FILE_NAME=$(basename $file)
            #file name without extension
            FILE_NAME_NO_EXTENSION=${FILE_NAME%.*}
            FILE_NAME_NO_EXTENSION=$(echo $FILE_NAME_NO_EXTENSION | sed 's/[^a-zA-Z0-9]//g')
            #replace .log extension in file name with .status extension
            STATUS_FILE_NAME=${FILE_NAME%.log}.status
            STATUS_FILE=${REDIS_SERVER_LOGS_DIR}/status/$STATUS_FILE_NAME
            if [ ! -f $STATUS_FILE ]; then
                touch $STATUS_FILE
                STATUS_FILE=${REDIS_SERVER_LOGS_DIR}/status/$STATUS_FILE_NAME
            fi
            LAST_ERROR_LINE=$(tail -n 1 $file)
            LAST_STATUS_LINE=$(tail -n 1 ${REDIS_SERVER_LOGS_DIR}/status/$STATUS_FILE_NAME)
            if [ "$LAST_ERROR_LINE" != "$LAST_STATUS_LINE" ]; then
                tail -n $NUMBER_OF_ERROR_LINES ${REDIS_SERVER_LOGS_DIR}/error/$FILE_NAME > $STATUS_FILE
                ERROR_OUTPUT_ARRAY["$ITERATOR"]=$(tail -n $NUMBER_OF_ERROR_LINES "$file")
            fi
            ITERATOR=$((ITERATOR+1))
        fi
    done

    ITERATOR=0
    for file in ${OTHER_FILES_TO_TRACK[@]}; do
        if [ -f $file ]; then
            #get just the name of the file
            FILE_NAME=$(basename $file)
            #file name without extension
            FILE_NAME_NO_EXTENSION=${FILE_NAME%.*}
            FILE_NAME_NO_EXTENSION=$(echo $FILE_NAME_NO_EXTENSION | sed 's/[^a-zA-Z0-9]//g')
            #replace .log extension in file name with .status extension
            STATUS_FILE_NAME=${FILE_NAME%.log}.status
            STATUS_FILE=${REDIS_SERVER_LOGS_DIR}/status/$STATUS_FILE_NAME
            if [ ! -f $STATUS_FILE ]; then
                $STATUS_FILE_NAME = $FILE_NAME
                STATUS_FILE=${REDIS_SERVER_LOGS_DIR}/status/$STATUS_FILE_NAME
            fi
            LAST_OTHER_LINE=$(tail -n 1 $file)
            LAST_STATUS_LINE=$(tail -n 1 ${REDIS_SERVER_LOGS_DIR}/status/$STATUS_FILE_NAME)
                tail -n $NUMBER_OF_OTHER_LINES ${REDIS_SERVER_LOGS_DIR}/system/$FILE_NAME > $STATUS_FILE
                OTHER_OUTPUT_ARRAY[$ITERATOR]=$(tail -n $NUMBER_OF_OTHER_LINES "$file")
            ITERATOR=$((ITERATOR+1))
        fi
    done

    #for each entry in ACTION_OUTPUT_ARRAY, write the entry to the ACTION_STATUS_FILE
    > $ACTION_STATUS_FILE
    for key in "${!ACTION_OUTPUT_ARRAY[@]}"; do
        echo "${ACTION_OUTPUT_ARRAY[$key]}" >> $ACTION_STATUS_FILE
    done

    #for each entry in ERROR_OUTPUT_ARRAY, write the entry to the ERROR_STATUS_FILE
    > $ERROR_STATUS_FILE
    for key in "${!ERROR_OUTPUT_ARRAY[@]}"; do
        echo "${ERROR_OUTPUT_ARRAY[$key]}" >> $ERROR_STATUS_FILE
    done

    #for each entry in DEBUG_OUTPUT_ARRAY, write the entry to the DEBUG_STATUS_FILE
    > $DEBUG_STATUS_FILE
    for key in "${!DEBUG_OUTPUT_ARRAY[@]}"; do
        echo "${DEBUG_OUTPUT_ARRAY[$key]}" >> $DEBUG_STATUS_FILE
    done

    #for each entry in OTHER_OUTPUT_ARRAY, write the entry to the OTHER_STATUS_FILE
    > $OTHER_STATUS_FILE
    for key in "${!OTHER_OUTPUT_ARRAY[@]}"; do
        echo "${OTHER_OUTPUT_ARRAY[$key]}" >> $OTHER_STATUS_FILE
    done



    sleep ${REDIS_STATUS_UPDATER_INTERVAL}

done

pkill -P $$

