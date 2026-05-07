#!/bin/bash

# Start server process. In dev (WS_WORKER_DEV_WATCH=true, set by docker-compose.dev.yml),
# use the watchfiles-wrapped runner so .py edits on the host restart server.py in-place.
if [ "${WS_WORKER_DEV_WATCH}" = "true" ]; then
    chmod +x /server/workers/run_server_watch.sh
    /server/workers/run_server_watch.sh &
else
    /server/workers/run_server.sh &
fi

# Wait for any process to exit
while true;do
    sleep 60
done