#!/bin/bash

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.sales (
    item_id      int,
    world_id     int,
    sale_time    timestamp,
    buyer_name   text,
    hq           boolean,
    on_mannequin boolean,
    quantity     int,
    unit_price   int,
    total_price  int,
    PRIMARY KEY ((item_id, world_id), sale_time, buyer_name)
) WITH CLUSTERING ORDER BY (sale_time DESC, buyer_name ASC)
  AND compaction = {
    'class': 'TimeWindowCompactionStrategy',
    'compaction_window_unit': 'DAYS',
    'compaction_window_size': 7
  }
  AND compression = {'sstable_compression': 'ZstdCompressor'};"

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.sales_by_buyer (
    buyer_name text,
    sale_time  timestamp,
    item_id    int,
    world_id   int,
    PRIMARY KEY ((buyer_name), sale_time, item_id, world_id)
) WITH CLUSTERING ORDER BY (sale_time DESC, item_id ASC, world_id ASC);"
