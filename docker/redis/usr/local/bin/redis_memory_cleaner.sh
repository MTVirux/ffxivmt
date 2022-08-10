#!/bin/bash
threshold=100          # Minimum amount of memory left when you should start killing, in MB
while true; do
    available=$(free -m | head -2 | tail -1 | awk '{print $4}')
    date = $(echo `date +"%Y%m%d%H%M%S"`)
    if [ "$threshold" -ge "$available" ]; then
        redis-cli save
        redis-cli flushall
        mv /rdump/dump.rdb /server/presistent_dumps/$date.rdb
        echo "Server memory is low, cleaning up..."
    fi
    sleep 20
done