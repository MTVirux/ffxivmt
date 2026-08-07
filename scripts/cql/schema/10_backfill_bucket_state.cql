-- Per-bucket backfill progress. Each bucket is roughly one Universalis request, so a request
-- that keeps failing stalls only its own slice of the catalogue instead of pinning the whole
-- region's pointer. Partitioned by (region, loop) so a pass reads every bucket in one read.
CREATE TABLE IF NOT EXISTS ffmt.backfill_bucket_state (
    region              text,
    loop                text,
    bucket              int,
    last_import_at      timestamp,
    earliest_import_at  timestamp,
    crawl_complete      boolean,
    PRIMARY KEY ((region, loop), bucket)
);
