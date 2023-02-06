#!/bin/bash

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.gilflux (
item_id int,
item_name text,
world_id int,
world_name text,
datacenter text,
region text,
total int,
sale_time timestamp,
PRIMARY KEY ((item_id, world_id, datacenter, region), sale_time)
);"

cqlsh -e "CREATE INDEX gilflux_item_id      ON ffmt.gilflux (item_id);"
cqlsh -e "CREATE INDEX gilflux_item_name    ON ffmt.gilflux (item_name);"
cqlsh -e "CREATE INDEX gilflux_world_id     ON ffmt.gilflux (world_id);"
cqlsh -e "CREATE INDEX gilflux_world_name   ON ffmt.gilflux (world_name);"
cqlsh -e "CREATE INDEX gilflux_datacenter   ON ffmt.gilflux (datacenter);"
cqlsh -e "CREATE INDEX gilflux_region       ON ffmt.gilflux (region);"
cqlsh -e "CREATE INDEX gilflux_sale_time    ON ffmt.gilflux (sale_time);"