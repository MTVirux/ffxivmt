#!/bin/bash

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.item_price_baseline (
    region            text,
    item_id           int,
    hq                boolean,
    median_unit_price bigint,
    sample_count      int,
    computed_at       timestamp,
    PRIMARY KEY ((region), item_id, hq)
) WITH CLUSTERING ORDER BY (item_id ASC, hq ASC)
  AND compression = {'sstable_compression': 'ZstdCompressor'};"
