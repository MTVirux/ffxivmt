-- APPLY THIS BEFORE DEPLOYING the code that writes total_price_gil.
-- ScyllaSaleStore.AddBatchAsync dual-writes this column on every insert. If the
-- column does not exist yet, every INSERT INTO sales fails on an unknown column
-- and both ingest paths stop writing sales entirely.
--
-- Not idempotent, so it lives outside scripts/cql/schema/ and a fresh container
-- never runs it - schema/02_sales.cql already declares the column.

ALTER TABLE ffmt.sales ADD total_price_gil bigint;
