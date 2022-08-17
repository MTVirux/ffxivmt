#!/bin/bash
THRESHOLD=100          # Minimum amount of memory left when you should start killing, in MB
while true; do
    AVALIABLE=$(free -m | head -2 | tail -1 | awk '{print $4}')
    DATE=$(echo `date +"%Y%m%d%H%M%S"`)
    if [ "$THRESHOLD" -ge "$AVALIABLE" ]; then
        redis-cli config set dbfilename $DATE.rdb
        redis-cli save
        redis-cli flushall
        echo "Server memory is low, cleaning up..."
    fi
    sleep 20
done