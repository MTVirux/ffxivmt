#!/bin/bash

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.worlds (
    id int,
    name text,
    datacenter text,
    region text,
    PRIMARY KEY (id));"
