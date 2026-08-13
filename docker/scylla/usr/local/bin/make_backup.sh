#!/bin/bash

# Bind-mounted to /mnt/scylla-data/backup on the Scylla VM.
backup_dir="/backup"

keyspaces=`cqlsh -e "DESCRIBE KEYSPACES;" | awk '{if (NR!=1) print $1}'`

for keyspace in $keyspaces
do
    tables=`cqlsh -e "DESCRIBE TABLES; USE $keyspace;" | awk '{if (NR!=1) print $1}'`

    for table in $tables
    do
        cqlsh -e "COPY $keyspace.$table TO '$backup_dir/$keyspace-$table.csv';"
    done
done