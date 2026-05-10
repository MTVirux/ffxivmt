#!/bin/bash

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.sales_sweep_jobs (
    bucket        int,
    enqueued_at   timeuuid,
    from_ts       timestamp,
    to_ts         timestamp,
    regions       set<text>,
    triggered_by  text,
    status        text,
    started_at    timestamp,
    finished_at   timestamp,
    sales_written int,
    error         text,
    PRIMARY KEY ((bucket), enqueued_at)
) WITH CLUSTERING ORDER BY (enqueued_at ASC);"
