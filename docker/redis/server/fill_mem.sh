#! /bin/bash

while true;do
	DATE=$(echo `date +"%Y%m%d%H%M%S%N"`)
	chmod +r /server/logs/action/sales_add_2022-08-17.log
	file=`cat /server/logs/action/sales_add_2022-08-17.log`
	redis-cli set $DATE "$file"
done


