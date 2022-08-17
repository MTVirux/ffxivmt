#!/bin/bash

/entrypoint.sh &

redis-cli config set save "" #Disable automatic saving
redis-cli select 0
redis-cli set sales ${REDIS_SALES_DB}
redis-cli set listings ${REDIS_LISTINGS_DB}
redis-cli set recent ${REDIS_RECENT_DB}

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