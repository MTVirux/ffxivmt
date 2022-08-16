#!/bin/bash

/custom_entrypoint.sh &

redis-cli config set dir /server/persistent_data
redis-cli config set dbfilename $(echo `date +"%Y%m%d%H%M%S"`).rdb

# Start the first process
/Python-3.10.5/python /server/server.py &
  
# Start the second process
/usr/local/bin/redis_memory_cleaner.sh &
  
# Wait for any process to exit
while true;do
    sleep 60
done
  
# Exit with status of process that exited first
exit $?