-- APPLY THIS BEFORE DEPLOYING the code that writes total_price_gil.
-- ScyllaSaleStore.AddBatchAsync dual-writes this column on every insert. If the
-- column does not exist yet, every INSERT INTO sales fails on an unknown column
-- and both ingest paths stop writing sales entirely.
--
-- The Scylla VM has no day-2 automation and docker/scylla/startup_scripts/ only
-- runs on a fresh container, so an existing cluster never picks this up on its
-- own. See scripts/cql/README.md.

ALTER TABLE ffmt.sales ADD total_price_gil bigint;
