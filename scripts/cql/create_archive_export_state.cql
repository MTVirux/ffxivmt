CREATE TABLE IF NOT EXISTS ffmt.archive_export_state (
    world_id    int,
    export_date date,
    exported_at timestamp,
    PRIMARY KEY (world_id, export_date)
) WITH CLUSTERING ORDER BY (export_date ASC);
