#!/bin/bash

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.gilflux_rankings (
    world_id       int,
    item_id        int,
    rankings       map<text, bigint>,
    last_sale_time timestamp,
    updated_at     timestamp,
    PRIMARY KEY ((world_id), item_id)
);"
