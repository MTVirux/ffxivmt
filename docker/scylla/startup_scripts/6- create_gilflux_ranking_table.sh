#!/bin/bash

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.gilflux_ranking (
    item_id        int,
    world_id       int,
    ranking_1h     bigint,
    ranking_3h     bigint,
    ranking_6h     bigint,
    ranking_12h    bigint,
    ranking_1d     bigint,
    ranking_3d     bigint,
    ranking_7d     bigint,
    last_sale_time timestamp,
    updated_at     timestamp,
    PRIMARY KEY ((item_id, world_id))
);"

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.gilflux_by_world (
    world_id       int,
    item_id        int,
    ranking_1h     bigint,
    ranking_3h     bigint,
    ranking_6h     bigint,
    ranking_12h    bigint,
    ranking_1d     bigint,
    ranking_3d     bigint,
    ranking_7d     bigint,
    last_sale_time timestamp,
    updated_at     timestamp,
    PRIMARY KEY ((world_id), item_id)
);"
