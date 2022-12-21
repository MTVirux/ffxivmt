
#! /bin/bash

while true
do
    until /Python-3.10.5/python /server/server.py;do
	sleep ${REDIS_SERVER_RESTART_DELAY}
	done
done
