#! /bin/bash

# This script fixes the permissions of folders so the backend can write to them

#Logs
mkdir -p /var/www/html/application/logs
chmod -R 777 /var/www/html/application/logs

#Cache
mkdir -p /var/www/html/application/cache
chmod -R 777 /var/www/html/application/cache

