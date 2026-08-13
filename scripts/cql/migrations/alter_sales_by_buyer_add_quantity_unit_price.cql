-- APPLY THIS BEFORE DEPLOYING the code that writes these columns.
-- ScyllaSaleStore.AddBatchAsync writes quantity and unit_price into
-- sales_by_buyer on every insert; until they exist, every
-- INSERT INTO sales_by_buyer fails on an unknown column and both ingest paths
-- stop writing sales entirely.
--
-- Not idempotent, so it lives outside scripts/cql/schema/ and a fresh container
-- never runs it - schema/02_sales.cql already declares the columns.
--
-- Rows written before this lands come back null and render as blank in the
-- buyer search. They age out with the archive prune (widest Gilflux timeframe
-- + 1 day, ~8 days).

ALTER TABLE ffmt.sales_by_buyer ADD quantity int;
ALTER TABLE ffmt.sales_by_buyer ADD unit_price int;
