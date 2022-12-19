#!/bin/bash

# Start server process
/server/workers/run_server.sh &

# Wait for any process to exit
while true;do
    sleep 60
done