// Until `pnpm openapi:gen` populates src/api/generated/schema.ts from a live
// /openapi/v1.json, this file is the import boundary call sites use.

export type ApiEnvelope<T> =
  | { status: true; message: string; data: T }
  | { status: false; message: string };

/** GET /api/v1/item/:id — minimal Item shape returned today. */
export type Item = {
  id: number;
  name: string;
  marketable: boolean;
  craftable: boolean;
  icon_image: number;
};

/**
 * GET /api/v1/worlds — region → datacenter → worldId(string) → worldName.
 * Region/DC keys keep their original casing; only property names are snake_cased.
 */
export type WorldStructure = Record<string, Record<string, Record<string, string>>>;

/** GET /api/v1/gilflux — one row per (item, world); aggregations are client-side. */
export type GilfluxRanking = {
  item_id: number;
  item_name: string;
  world_id: number | null;
  world_name: string | null;
  datacenter: string;
  region: string;
  /** Keyed by timeframe label (e.g. "1h", "7d"). */
  rankings: Record<string, number>;
  /** Epoch millis — null if the ranking has never been refreshed. */
  updated_at: number | null;
  /** Epoch millis of the most recent sale used for the ranking; null if no sales yet. */
  last_sale_time: number | null;
};

export type LocationKind = 'world' | 'datacenter' | 'region';

export type Location = {
  kind: LocationKind;
  /** Sent as `target_location` — must match the name as it appears in `worlds`. */
  name: string;
  /** Set only when kind === 'world'. */
  worldId?: number;
};

/** Result row for /api/v1/tools/item_product_profit_calculator. */
export type ProfitRow = {
  id: number;
  name: string;
  min_price: number;
  regular_sale_velocity: number;
  ffmt_score: number;
};

export type ItemProductProfitResponse =
  | {
      status: true;
      message?: string;
      data: ProfitRow[];
      item_name: string;
      item_id: number;
      location: string;
      request_id: string;
    }
  | { status: false; message: string };

/** Result row for /api/v1/tools/currency_efficiency_calculator. */
export type CurrencyEfficiencyRow = {
  id: number;
  name: string;
  /** Currency cost per unit (e.g. allagan tomestones to buy 1). */
  price: number;
  currency_id: number;
  currency_name: string;
  min_price: number;
  regular_sale_velocity: number;
  median_stack_size: number;
  daily_market_cap: number;
  daily_market_cap_percent: number;
  ffmt_score: number;
};

export type CurrencyEfficiencyResponse =
  | {
      status: true;
      message?: string;
      data: CurrencyEfficiencyRow[];
      item_name: string;
      item_id: number;
      location: string;
      request_id: string;
    }
  | { status: false; message: string };

/** GET /api/v1/item/:id/sales — single Scylla `sales` row. */
export type Sale = {
  item_id: number;
  world_id: number;
  item_name: string;
  world_name: string;
  datacenter: string;
  region: string;
  buyer_name: string;
  hq: boolean;
  on_mannequin: boolean;
  quantity: number;
  unit_price: number;
  total: number;
  /** ISO 8601 with timezone offset (e.g. "2026-05-08T12:34:56+00:00"). */
  sale_time: string;
};

/** GET /api/v1/config — server-side configuration for the frontend. */
export type AppConfig = {
  /** Gilflux timeframe keys in ascending duration order (e.g. ["1h","3h","7d"]). */
  gilflux_timeframes: string[];
};

/** GET /api/v1/search_buyer — one row per purchase found for the buyer. */
export type BuyerSearchRow = {
  item_id: number;
  world_id: number;
  buyer_name: string;
  /** ISO 8601 with timezone offset (e.g. "2026-05-08T12:34:56+00:00"). */
  sale_time: string;
};
