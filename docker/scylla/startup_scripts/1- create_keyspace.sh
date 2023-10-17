#!/bin/bash

cqlsh -e "CREATE KEYSPACE IF NOT EXISTS ffmt WITH REPLICATION = {'class': 'SimpleStrategy', 'replication_factor': 1};"
