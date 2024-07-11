#!/bin/bash

echo "Enabling DB update script..."

echo "Generating temp key..."
TEMP_KEY=$(head -c 512K /dev/urandom | tr -dc 'a-zA-Z0-9' | head -c 524288)

# Echo the key to the config PHP file
echo "<?php defined('BASEPATH') OR exit('No direct script access allowed');
\$config[\"temp_key\"] = \"$TEMP_KEY\"; ?>" > ./backend/application/config/secrets.php

echo "Updating Item DB (Log Channel SCYLLADB)..."

# Generate timestamp for the filename
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")

# Make the POST request to update the DB and pass the key
curl -d "key=$TEMP_KEY" localhost/updatedb/ > /dev/null

echo "Cleanup: Removing temp key..."
echo "" > ./backend/application/config/secrets.php