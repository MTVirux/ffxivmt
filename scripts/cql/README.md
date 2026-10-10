# CQL

Single source of truth for the Scylla schema. Nothing here is created by the
application.

| Dir | Contents | Applied automatically? |
|---|---|---|
| `schema/` | Idempotent `CREATE KEYSPACE` / `CREATE TABLE IF NOT EXISTS`, numbered in dependency order. | Yes, but only on a **fresh** container: compose bind-mounts this dir at `/startup_scripts` and `run_entrypoints.sh` runs `cqlsh -f` over every `*.cql` once Scylla answers CQL. |
| `migrations/` | Non-idempotent one-offs (`ALTER TABLE`, drops). | No - never runs on its own. |

The Scylla VM has no day-2 automation - `redeploy.sh` is app-VM only - so an
existing cluster never picks up a new file on its own. Apply by hand on the
Scylla VM:

```bash
docker exec -i ffmt_scylla_node cqlsh -f - < scripts/cql/schema/<file>.cql
docker exec -i ffmt_scylla_node cqlsh -f - < scripts/cql/migrations/<file>.cql
```

Adding a table means adding one file under `schema/` with the next number.
There is no second copy to keep in sync.

## Ordering hazards

- **`migrations/alter_sales_add_total_price_gil.cql` must land before the deploy
  that writes the column.** `ScyllaSaleStore.AddBatchAsync` dual-writes
  `total_price_gil` on every insert; without the column, every
  `INSERT INTO sales` fails on an unknown column and both the websocket
  consumer and the backfill service stop writing sales. This is an ingestion
  outage, not a degraded mode. `schema/02_sales.cql` already declares the
  column, so only pre-existing clusters need the migration.
- **`migrations/alter_sales_by_buyer_add_quantity_unit_price.cql` must land
  before the deploy that writes those columns.** Same failure mode as above:
  `ScyllaSaleStore.AddBatchAsync` writes `quantity` and `unit_price` into
  `sales_by_buyer` on every insert, so a missing column is an ingestion outage.
  `schema/02_sales.cql` already declares them, so only pre-existing clusters
  need the migration. Rows written before it lands read back null and the buyer
  search renders them blank; they age out with the archive prune (~8 days).
- **`schema/12_item_price_baseline.cql` must land before
  `ffmt update-baselines`**, and that job must run before the anomaly filter is
  armed. Until baselines exist the filter fails open on every sale, which is
  safe but means no protection - watch `ffmt_sales_no_baseline_total`.
- **`schema/13_archive_export_state.cql` must land before the first
  `ffmt archive` run.**
- **`schema/11_sales_quarantine.cql` must land before deploying the sale
  anomaly quarantine.**
- **`schema/10_backfill_bucket_state.cql` must land before deploying the worker
  that uses it.**
- **Retiring `total_price`** (`ALTER TABLE ffmt.sales DROP total_price;`) is a
  separate, later step. It is only safe once the deploy that stopped writing it
  has been live longer than the sales retention window - roughly 8 days, the
  widest Gilflux timeframe plus a day - so that no surviving row still carries
  only the old column.
- **`schema/14_mannequin_sales.cql` should land before deploying the mannequin
  feed.** Unlike the sales migrations above this is not an ingestion outage:
  `ScyllaSaleStore` skips the mannequin rows (warning at most every 5 minutes)
  while the tables are missing. But those sales never reach the feed, the
  `/api/v1/mannequin_sales` endpoint errors and the cap worker logs failed
  passes. Run `ffmt backfill-mannequin` after applying it to recover the gap.
- **`schema/15_mannequin_sales_by_day.cql` must land before the deploy that
  reads the view.** `MannequinSalesReader` reads one `mannequin_sales_by_day`
  partition per day instead of one `mannequin_sales` partition per world, so
  without the view every `/api/v1/mannequin_sales` request fails. Ingest is
  unaffected - Scylla keeps the view in step with base-table writes and the cap
  worker's partition deletes. Existing rows are built into the view in the
  background, so the feed can look short until that finishes.
