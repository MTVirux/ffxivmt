-- Per-bucket backfill progress. Each bucket is roughly one Universalis request, so a request
-- that keeps failing stalls only its own slice of the catalogue instead of pinning the whole
-- region's pointer. Partitioned by (region, loop) so a pass reads every bucket in one read.
--
-- Apply by hand on the Scylla VM before deploying the worker that uses it:
--   docker exec ffmt-ffmt_scylla_node-1 cqlsh -f /path/to/create_backfill_bucket_state.cql
CREATE TABLE IF NOT EXISTS ffmt.backfill_bucket_state (
    region              text,
    loop                text,
    bucket              int,
    last_import_at      timestamp,
    earliest_import_at  timestamp,
    crawl_complete      boolean,
    PRIMARY KEY ((region, loop), bucket)
);
