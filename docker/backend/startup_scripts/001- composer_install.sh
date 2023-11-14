#! /bin/bash

# This script updates the composer dependencies of the backend.

cd /var/www/html/application
rm -rf /var/www/html/application/vendor
composer install