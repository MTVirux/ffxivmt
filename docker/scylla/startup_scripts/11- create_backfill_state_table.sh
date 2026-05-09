#!/bin/bash

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.backfill_state (
    region              text,
    last_import_at      timestamp,
    earliest_import_at  timestamp,
    PRIMARY KEY (region));"
