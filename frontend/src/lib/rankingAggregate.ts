import type { GilfluxRanking } from '../api/types';

export type TimeframeKey =
  | 'ranking_1h'
  | 'ranking_3h'
  | 'ranking_6h'
  | 'ranking_12h'
  | 'ranking_1d'
  | 'ranking_3d'
  | 'ranking_7d';

export const TIMEFRAMES: readonly { key: TimeframeKey; label: string }[] = [
  { key: 'ranking_1h', label: '1h' },
  { key: 'ranking_3h', label: '3h' },
  { key: 'ranking_6h', label: '6h' },
  { key: 'ranking_12h', label: '12h' },
  { key: 'ranking_1d', label: '1d' },
  { key: 'ranking_3d', label: '3d' },
  { key: 'ranking_7d', label: '7d' },
] as const;

/** Used at both the aggregate (per-item) and per-world levels of the ranking table. */
export type RankingRow = {
  kind: 'aggregate' | 'world';
  item_id: number;
  item_name: string;
  /** null on aggregate rows. */
  world_name: string | null;
  ranking_1h: number;
  ranking_3h: number;
  ranking_6h: number;
  ranking_12h: number;
  ranking_1d: number;
  ranking_3d: number;
  ranking_7d: number;
  last_sale_time: number | null;
  subRows?: RankingRow[];
};

// Single-world queries leave subRows undefined (one row per item, nothing to expand).
export function aggregateRankings(rankings: GilfluxRanking[]): RankingRow[] {
  const map = new Map<number, RankingRow>();

  for (const r of rankings) {
    let agg = map.get(r.item_id);
    if (!agg) {
      agg = {
        kind: 'aggregate',
        item_id: r.item_id,
        item_name: r.item_name,
        world_name: null,
        ranking_1h: 0,
        ranking_3h: 0,
        ranking_6h: 0,
        ranking_12h: 0,
        ranking_1d: 0,
        ranking_3d: 0,
        ranking_7d: 0,
        last_sale_time: null,
        subRows: [],
      };
      map.set(r.item_id, agg);
    }

    for (const { key } of TIMEFRAMES) agg[key] += r[key];

    if (r.last_sale_time !== null && (agg.last_sale_time === null || r.last_sale_time > agg.last_sale_time)) {
      agg.last_sale_time = r.last_sale_time;
    }

    agg.subRows!.push({
      kind: 'world',
      item_id: r.item_id,
      item_name: r.item_name,
      world_name: r.world_name,
      ranking_1h: r.ranking_1h,
      ranking_3h: r.ranking_3h,
      ranking_6h: r.ranking_6h,
      ranking_12h: r.ranking_12h,
      ranking_1d: r.ranking_1d,
      ranking_3d: r.ranking_3d,
      ranking_7d: r.ranking_7d,
      last_sale_time: r.last_sale_time,
    });
  }

  const out: RankingRow[] = [];
  for (const row of map.values()) {
    if ((row.subRows?.length ?? 0) <= 1) {
      row.subRows = undefined;
    }
    out.push(row);
  }
  return out;
}
