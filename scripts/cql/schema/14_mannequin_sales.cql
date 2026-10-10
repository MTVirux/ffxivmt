CREATE TABLE IF NOT EXISTS ffmt.mannequin_sales (
    world_id   int,
    day        date,
    sale_time  timestamp,
    item_id    int,
    buyer_name text,
    hq         boolean,
    quantity   int,
    unit_price int,
    PRIMARY KEY ((world_id, day), sale_time, item_id, buyer_name)
) WITH CLUSTERING ORDER BY (sale_time DESC, item_id ASC, buyer_name ASC)
  AND compression = {'sstable_compression': 'ZstdCompressor'};

CREATE TABLE IF NOT EXISTS ffmt.mannequin_sales_days (
    bucket int,
    day    date,
    PRIMARY KEY ((bucket), day)
) WITH CLUSTERING ORDER BY (day ASC);
