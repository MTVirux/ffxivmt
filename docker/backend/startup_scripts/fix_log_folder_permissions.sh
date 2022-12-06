#! /bin/bash

# This script fixes the permissions of the log folder so that the
# backend can write to it.

chmod -R 777 /var/www/html/application/logs

