#!/bin/bash

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.gilflux_dirty_pairs (
    bucket       int,
    enqueued_at  timeuuid,
    item_id      int,
    world_id     int,
    PRIMARY KEY ((bucket), enqueued_at, item_id, world_id)
);"
