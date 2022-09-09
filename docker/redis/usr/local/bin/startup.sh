#!/bin/bash

/entrypoint.sh &

#Wait until redis is up and done loading
while ! redis-cli ping | grep -q 'PONG'; do
  sleep 1
done
/server/redis-config/prep.sh

# Start server process
/server/workers/run_server.sh &
  
# Start memory cleaner process
/server/workers/mem_monitor.sh &

# Wait for any process to exit
while true;do
    sleep 60
done