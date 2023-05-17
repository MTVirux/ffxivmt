#!/bin/bash

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.gilflux_ranking (
item_id int,
item_name text,
world_id int,
world_name text,
datacenter text,
region text,
ranking_alltime bigint,
ranking_1h bigint,
ranking_3h bigint,
ranking_6h bigint,
ranking_12h bigint,
ranking_1d bigint,
ranking_3d bigint,
ranking_7d bigint,
updated_at timestamp,
PRIMARY KEY ((item_id, world_id, datacenter, region))
);"

cqlsh -e "CREATE INDEX gilflux_ranking_item_name    ON ffmt.gilflux_ranking (item_name);"
cqlsh -e "CREATE INDEX gilflux_ranking_world_id     ON ffmt.gilflux_ranking (world_id);"
cqlsh -e "CREATE INDEX gilflux_ranking_world_name   ON ffmt.gilflux_ranking (world_name);"
cqlsh -e "CREATE INDEX gilflux_ranking_datacenter   ON ffmt.gilflux_ranking (datacenter);"
cqlsh -e "CREATE INDEX gilflux_ranking_region       ON ffmt.gilflux_ranking (region);"
cqlsh -e "CREATE INDEX gilflux_ranking_updated_at   ON ffmt.gilflux_ranking (updated_at);"
