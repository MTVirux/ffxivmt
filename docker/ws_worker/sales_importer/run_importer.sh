#!/bin/bash
# Run the bulk sales importer. Pass --detached to run in background.
export PYTHONPATH=/ws_worker:${PYTHONPATH}

cd /sales_importer/python

mkdir -p /sales_importer/logs/action
mkdir -p /sales_importer/logs/error
mkdir -p /sales_importer/logs/request
mkdir -p /sales_importer/logs/debug
mkdir -p /sales_importer/logs/panic
chmod -R 777 /sales_importer/logs

if [ "$1" = "--detached" ]; then
    echo "Running in detached mode"
    /Python-3.10.5/python sales_importer.py > /dev/null 2>&1 &
    exit 0
else
    echo "Running in foreground mode"
    /Python-3.10.5/python sales_importer.py
fi
