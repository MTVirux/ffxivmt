#!/bin/bash


cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.worlds (
id int,
name text,
datacenter text,
region text,
PRIMARY KEY (id));"


cqlsh -e "CREATE INDEX IF NOT EXISTS world_names ON ffmt.worlds (name)"
cqlsh -e "CREATE INDEX IF NOT EXISTS datacenters ON ffmt.worlds (datacenter)"
cqlsh -e "CREATE INDEX IF NOT EXISTS regions ON ffmt.worlds (region)"
