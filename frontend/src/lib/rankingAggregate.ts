import type { GilfluxRanking } from '../api/types';

/** Fallback timeframe keys for useAppConfig until /config answers. */
export const TIMEFRAMES: readonly string[] = ['1h', '3h', '6h', '12h', '1d', '3d', '7d'] as const;

export type RankingRow = {
  kind: 'aggregate' | 'world';
  item_id: number;
  item_name: string;
  /** null on aggregate rows. */
  world_name: string | null;
  rankings: Record<string, number>;
  last_sale_time: number | null;
  subRows?: RankingRow[];
};

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
        rankings: {},
        last_sale_time: null,
        subRows: [],
      };
      map.set(r.item_id, agg);
    }

    for (const [key, val] of Object.entries(r.rankings)) {
      agg.rankings[key] = (agg.rankings[key] ?? 0) + val;
    }

    if (
      r.last_sale_time !== null &&
      (agg.last_sale_time === null || r.last_sale_time > agg.last_sale_time)
    ) {
      agg.last_sale_time = r.last_sale_time;
    }

    agg.subRows!.push({
      kind: 'world',
      item_id: r.item_id,
      item_name: r.item_name,
      world_name: r.world_name,
      rankings: { ...r.rankings },
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
