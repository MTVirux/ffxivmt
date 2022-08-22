#!/bin/bash
THRESHOLD=100          # Minimum amount of memory left when you should start killing, in MB
rm -rf /server/logs/redis_memory_cleaner.log
echo "---Memory cleaner started---" >> /server/logs/redis_memory_cleaner.log
while true; do
    MEMORY_LIMIT_IN_BYTES=$(cat /sys/fs/cgroup/memory/memory.limit_in_bytes)
    MEMORY_LIMIT_IN_MEGABYTES=$(($MEMORY_LIMIT_IN_BYTES / 1024 / 1024))
    MEMORY_USAGE_IN_BYTES=$(cat /sys/fs/cgroup/memory/memory.usage_in_bytes)
    MEMORY_USAGE_IN_MEGABYTES=$(($MEMORY_USAGE_IN_BYTES / 1024 / 1024))
    MEMORY_LEFT_IN_MEGABYTES=$(($MEMORY_LIMIT_IN_MEGABYTES - $MEMORY_USAGE_IN_MEGABYTES))
    MEMORY_LEFT_TO_HIT_THRESHOLD=$(($MEMORY_LEFT_IN_MEGABYTES - $THRESHOLD))
    DATE=$(echo `date +"%Y%m%d%H%M%S"`)
    if [ "$THRESHOLD" -ge "$MEMORY_LEFT_IN_MEGABYTES" ]; then
        redis-cli config set dbfilename $DATE.rdb
        redis-cli save
        redis-cli flushall
        redis-cli set sales ${REDIS_SALES_DB}
        redis-cli set listings ${REDIS_LISTINGS_DB}
        redis-cli set recent ${REDIS_RECENT_DB}
        MESSAGE="["+$date"]"+" Server memory is low, cleaning up..."
        echo $MESSAGE >> /server/logs/redis_memory_cleaner.log
    fi
    echo "[" $(date) "]" "Current memory usage is: $MEMORY_USAGE_IN_MEGABYTES MB/ $MEMORY_LIMIT_IN_MEGABYTES MB ( $MEMORY_LEFT_TO_HIT_THRESHOLD MB left to hit threshold of $THRESHOLD MB)" >> /server/logs/redis_memory_cleaner.log
    sleep 0.1
done