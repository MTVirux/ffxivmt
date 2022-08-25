#! /bin/bash

kill $(ps aux | grep "/Python-3.10.5/python /server/server.py" | head -1 |awk '{print $2}')
