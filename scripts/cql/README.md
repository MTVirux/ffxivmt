# Manual CQL

Schema that is **not** created by the application and **not** applied by any
deploy script. The Scylla VM has no day-2 automation - `redeploy.sh` is app-VM
only - and `docker/scylla/startup_scripts/` runs only on a fresh container, so an
existing cluster never picks these up on its own.

Apply by hand on the Scylla VM:

```bash
docker exec -i ffmt_scylla_node cqlsh -f - < scripts/cql/<file>.cql
```

| File | When |
|---|---|
| `create_archive_export_state.cql` | Before the first `ffmt archive` run. |
| `alter_sales_add_total_price_gil.cql` | **Before** deploying the code that dual-writes `total_price_gil`. |
| `create_sales_quarantine_table.cql` | Before deploying the sale anomaly quarantine. |
| `create_item_price_baseline_table.cql` | Before the first `ffmt update-baselines` run. |

## Ordering hazards

- **`alter_sales_add_total_price_gil.cql` must land before the deploy that
  writes the column.** `ScyllaSaleStore.AddBatchAsync` dual-writes
  `total_price_gil` on every insert; without the column, every
  `INSERT INTO sales` fails on an unknown column and both the websocket
  consumer and the backfill service stop writing sales. This is an ingestion
  outage, not a degraded mode.
- **`create_item_price_baseline_table.cql` must land before
  `ffmt update-baselines`**, and that job must run before the anomaly filter is
  armed. Until baselines exist the filter fails open on every sale, which is
  safe but means no protection - watch `ffmt_sales_no_baseline_total`.
- **Retiring `total_price`** (`ALTER TABLE ffmt.sales DROP total_price;`) is a
  separate, later step. It is only safe once the deploy that stopped writing it
  has been live longer than the sales retention window - roughly 8 days, the
  widest Gilflux timeframe plus a day - so that no surviving row still carries
  only the old column.

The corresponding `CREATE TABLE` statements are mirrored in
`docker/scylla/startup_scripts/` so a rebuilt Scylla VM gets them at bootstrap.
Keep the two copies in sync.
