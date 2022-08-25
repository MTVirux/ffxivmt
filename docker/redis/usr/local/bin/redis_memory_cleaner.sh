#!/bin/bash
THRESHOLD=50

# Get the total memory size
MEMORY_LIMIT_IN_BYTES=$(cat /sys/fs/cgroup/memory/memory.limit_in_bytes)
MEMORY_LIMIT_IN_MEGABYTES=$(($MEMORY_LIMIT_IN_BYTES / 1024 / 1024))

if [ ${REDIS_MEMORY_CLEANER_TRIGGER_TYPE} == "percentage" ];then
    THRESHOLD=${MEMORY_LIMIT_IN_MEGABYTES}/100*${REDIS_MEMORY_CLEANER_THRESHOLD_PERCENTAGE}
else
    THRESHOLD=${REDIS_MEMORY_CLEANER_THRESHOLD_SIZE}
fi


rm -rf /server/logs/redis_memory_cleaner.log
echo "---Memory cleaner started---" >> /server/logs/redis_memory_cleaner.log
while true; do

    # Get the used memory size
    MEMORY_USAGE_IN_BYTES=$(cat /sys/fs/cgroup/memory/memory.usage_in_bytes)
    MEMORY_USAGE_IN_MEGABYTES=$(($MEMORY_USAGE_IN_BYTES / 1024 / 1024))

    # Get the percentage of memory used
    MEMORY_LEFT_IN_MEGABYTES=$(($MEMORY_LIMIT_IN_MEGABYTES - $MEMORY_USAGE_IN_MEGABYTES))
    MEMORY_LEFT_TO_HIT_THRESHOLD=$(($MEMORY_LEFT_IN_MEGABYTES - $THRESHOLD))

    if [ "$THRESHOLD" -ge "$MEMORY_LEFT_IN_MEGABYTES" ]; then
        DATE=$(echo `date +"%Y%m%d%H%M%S"`)
        redis-cli config set dbfilename $DATE.rdb
        redis-cli save
        redis-cli flushall
        redis-cli set sales ${REDIS_SALES_DB}
        redis-cli set listings ${REDIS_LISTINGS_DB}
        redis-cli set recent ${REDIS_RECENT_DB}
        MESSAGE="["+$date"]"+" Server memory is low, cleaning up..."
        echo $MESSAGE >> /server/logs/redis_memory_cleaner.log
        echo $MESSAGE > /server/logs/status/redis_memory_cleaner.status
    fi
    echo "Current memory usage is: $MEMORY_USAGE_IN_MEGABYTES MB/ $MEMORY_LIMIT_IN_MEGABYTES MB ( $MEMORY_LEFT_TO_HIT_THRESHOLD MB left to hit threshold of $THRESHOLD MB)" > ${REDIS_SERVER_LOGS_DIR}/system/redis_memory_cleaner.log
    sleep ${REDIS_MEMORY_CLEANER_INTERVAL}
done