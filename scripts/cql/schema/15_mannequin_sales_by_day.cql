CREATE MATERIALIZED VIEW IF NOT EXISTS ffmt.mannequin_sales_by_day AS
    SELECT world_id, day, sale_time, item_id, buyer_name, hq, quantity, unit_price
    FROM ffmt.mannequin_sales
    WHERE day IS NOT NULL AND sale_time IS NOT NULL AND world_id IS NOT NULL
      AND item_id IS NOT NULL AND buyer_name IS NOT NULL
    PRIMARY KEY ((day), sale_time, world_id, item_id, buyer_name)
    WITH CLUSTERING ORDER BY (sale_time DESC, world_id ASC, item_id ASC, buyer_name ASC)
    AND compression = {'sstable_compression': 'ZstdCompressor'};
