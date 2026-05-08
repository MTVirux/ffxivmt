#!/bin/bash
# Restart the websocket consumer if it ever exits.
export PYTHONPATH=/ws_worker:${PYTHONPATH}

while true; do
    /Python-3.10.5/python /server/server.py
    sleep 1
done
