#!/bin/bash

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.shops (
    item_id int,
    item_name text,
    currency_id text,
    currency_name text,
    price int,
    amount int,
    npc_id int,
    npc_name text,
    shop_id text,
    shop_name text,
    PRIMARY KEY ((item_id, currency_id, npc_id, shop_id))
);"

cqlsh -e "CREATE INDEX shops_item_id            ON ffmt.shops (item_id);"
cqlsh -e "CREATE INDEX shops_item_name          ON ffmt.shops (item_name);"
cqlsh -e "CREATE INDEX shops_currency_id        ON ffmt.shops (currency_id);"
cqlsh -e "CREATE INDEX shops_currency_name      ON ffmt.shops (currency_name);"
cqlsh -e "CREATE INDEX shops_npc_id             ON ffmt.shops (npc_id);"
cqlsh -e "CREATE INDEX shops_npc_name           ON ffmt.shops (npc_name);"
cqlsh -e "CREATE INDEX shops_shop_id            ON ffmt.shops (shop_id);"
cqlsh -e "CREATE INDEX shops_shop_name          ON ffmt.shops (shop_name);"