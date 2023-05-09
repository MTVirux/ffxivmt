#!/bin/bash

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.gilflux_ranking (
    item_id int,
    item_name text,
    world_id int,
    world_name text,
    gilflux_ranking int,
    24h_gilflux_ranking int,
    updated_at timestamp,
    PRIMARY KEY ((item_id, world_id))
);"

cqlsh -e "CREATE INDEX gilflux_ranking_item_id            ON ffmt.gilflux_ranking (item_id);"
cqlsh -e "CREATE INDEX gilflux_ranking_item_name          ON ffmt.gilflux_ranking (item_name);"
cqlsh -e "CREATE INDEX gilflux_ranking_world_id           ON ffmt.gilflux_ranking (world_id);"
cqlsh -e "CREATE INDEX gilflux_ranking_world_name         ON ffmt.gilflux_ranking (world_name);"
