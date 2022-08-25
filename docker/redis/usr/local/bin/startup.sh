#!/bin/bash

/entrypoint.sh &

#Prep redis DB
/server/redis-config/prep.sh

# Start server process
/server/workers/run_server.sh &
  
# Start memory cleaner process
/server/workers/mem_monitor.sh &

# Wait for any process to exit
while true;do
    sleep 60
done