#!/bin/bash

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.sales (buyer_name text,
 hq boolean,
 on_mannequin  boolean,
 unit_price int,
 quantity int,
 sale_time timestamp,
 world_id int,
 item_id int,
 world_name text,
 item_name text,
 total int,
 PRIMARY KEY ((item_id,world_id),sale_time))"
#Secondary index the buyer_name column
cqlsh -e "CREATE INDEX IF NOT EXISTS buyers ON ffmt.sales (buyer_name)"