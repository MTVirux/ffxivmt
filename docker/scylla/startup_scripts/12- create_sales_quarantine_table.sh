#!/bin/bash

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.sales_quarantine (
    item_id         int,
    world_id        int,
    sale_time       timestamp,
    buyer_name      text,
    hq              boolean,
    on_mannequin    boolean,
    quantity        int,
    unit_price      int,
    total_price     bigint,
    reason          text,
    baseline_median bigint,
    quarantined_at  timestamp,
    PRIMARY KEY ((item_id, world_id), sale_time, buyer_name)
) WITH CLUSTERING ORDER BY (sale_time DESC, buyer_name ASC)
  AND default_time_to_live = 7776000
  AND compaction = {
    'class': 'TimeWindowCompactionStrategy',
    'compaction_window_unit': 'DAYS',
    'compaction_window_size': 7
  }
  AND compression = {'sstable_compression': 'ZstdCompressor'};"
