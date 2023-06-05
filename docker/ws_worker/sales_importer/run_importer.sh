#!/bin/bash


#navigate to dir
cd /sales_importer/python

# set permissions on log folders
chmod -R 777 /sales_importer/logs

mkdir -p /sales_importer/logs/action
mkdir -p /sales_importer/logs/error
mkdir -p /sales_importer/logs/request
mkdir -p /sales_importer/logs/debug

# if --detached was passed run detached else run in foreground
if [ "$1" = "--detached" ]; then
    echo "Running in detached mode"
    python sales_importer.py > /dev/null 2>&1 &
    exit 0
else
    echo "Running in foreground mode"
    python sales_importer.py
    
fi
