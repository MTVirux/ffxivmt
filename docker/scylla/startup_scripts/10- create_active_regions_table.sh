#!/bin/bash

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.active_regions (
    region        text PRIMARY KEY,
    activated_at  timestamp,
    activated_by  text);"
