#!/bin/bash

# Bind-mounted to /mnt/scylla-data/backup on the Scylla VM.
backup_dir="/backup"

# Get a list of all keyspaces
keyspaces=`cqlsh -e "DESCRIBE KEYSPACES;" | awk '{if (NR!=1) print $1}'`

# Loop through the keyspaces
for keyspace in $keyspaces
do
    # Get a list of all tables in the keyspace
    tables=`cqlsh -e "DESCRIBE TABLES; USE $keyspace;" | awk '{if (NR!=1) print $1}'`

    # Loop through the tables
    for table in $tables
    do
        # Create a backup of the table
        cqlsh -e "COPY $keyspace.$table TO '$backup_dir/$keyspace-$table.csv';"
    done
done