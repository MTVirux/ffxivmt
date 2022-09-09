#! /bin/bash

# watch files in /server/logs/status

if [ -z "$1" ]; then
    NUMBER_OF_ACTION_LINES=3
else
    NUMBER_OF_ACTION_LINES=$1
fi

if [ -z "$2" ]; then
    NUMBER_OF_DEBUG_LINES=5
else
    NUMBER_OF_DEBUG_LINES=$2
fi

if [ -z "$3" ]; then
    NUMBER_OF_ERROR_LINES=5
else
    NUMBER_OF_ERROR_LINES=$3
fi

if [ -z "$4" ]; then
    NUMBER_OF_OTHER_LINES=1
else
    NUMBER_OF_OTHER_LINES=$4
fi

/server/workers/cpu_monitor.sh &

/server/workers/status_updater.sh $NUMBER_OF_ACTION_LINES $NUMBER_OF_DEBUG_LINES $NUMBER_OF_ERROR_LINES $NUMBER_OF_OTHER_LINES &

#Get highest of all params
MAX_NUMBER_OF_LINES=$NUMBER_OF_ACTION_LINES
if [ $NUMBER_OF_DEBUG_LINES -gt $MAX_NUMBER_OF_LINES ]; then
    MAX_NUMBER_OF_LINES=$NUMBER_OF_DEBUG_LINES
fi
if [ $NUMBER_OF_ERROR_LINES -gt $MAX_NUMBER_OF_LINES ]; then
    MAX_NUMBER_OF_LINES=$NUMBER_OF_ERROR_LINES
fi
if [ $NUMBER_OF_OTHER_LINES -gt $MAX_NUMBER_OF_LINES ]; then
    MAX_NUMBER_OF_LINES=$NUMBER_OF_OTHER_LINES
fi

watch -t -c -n $(bc -l <<< "${REDIS_STATUS_UPDATER_INTERVAL}") \
"tail -n $MAX_NUMBER_OF_LINES" \
"/server/logs/status/action.status" \
"/server/logs/status/debug.status" \
"/server/logs/status/error.status" \
"/server/logs/status/other.status"

kill $! 2>/dev/null
