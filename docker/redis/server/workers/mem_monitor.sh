
#! /bin/bash

while true
do
    until /usr/local/bin/redis_memory_cleaner.sh;do
	    sleep ${MEMORY_CLEANER_RESTART_DELAY}
	done
done
