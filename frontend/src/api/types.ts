// Hand-written until `pnpm openapi:gen` populates src/api/generated/schema.ts.
// The backend serializes snake_case, so these field names must match that casing.

export type ApiEnvelope<T> =
  | { status: true; message: string; data: T }
  | { status: false; message: string };

/** GET /api/v1/item/:id */
export type Item = {
  id: number;
  name: string;
  marketable: boolean;
  craftable: boolean;
  icon_image: number;
};

/**
 * GET /api/v1/worlds - region → datacenter → worldId(string) → worldName.
 * Region/DC keys keep their original casing; only property names are snake_cased.
 */
export type WorldStructure = Record<string, Record<string, Record<string, string>>>;

/** GET /api/v1/gilflux - one row per (item, world); aggregations are client-side. */
export type GilfluxRanking = {
  item_id: number;
  item_name: string;
  world_id: number | null;
  world_name: string | null;
  datacenter: string;
  region: string;
  /** Keyed by timeframe label (e.g. "1h", "7d"). */
  rankings: Record<string, number>;
  /** Epoch millis; null if never refreshed. */
  updated_at: number | null;
  /** Epoch millis of the most recent sale; null if none. */
  last_sale_time: number | null;
};

export type LocationKind = 'world' | 'datacenter' | 'region';

export type Location = {
  kind: LocationKind;
  /** Sent as `target_location`; must match the name as it appears in `worlds`. */
  name: string;
  /** Set only when kind === 'world'. */
  worldId?: number;
};

/** Fields the /api/v1/tools/* calculators return beside `data`. */
export type ToolMeta = {
  item_name: string;
  item_id: number;
  location: string;
  request_id: string;
};

export type ToolResponse<T> =
  | ({ status: true; message?: string; data: T[] } & ToolMeta)
  | { status: false; message: string };

/** Row for /api/v1/tools/item_product_profit_calculator. */
export type ProfitRow = {
  id: number;
  name: string;
  min_price: number;
  regular_sale_velocity: number;
  ffmt_score: number;
};

export type ItemProductProfitResponse = ToolResponse<ProfitRow>;

/** Row for /api/v1/tools/currency_efficiency_calculator. */
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

export type CurrencyEfficiencyResponse = ToolResponse<CurrencyEfficiencyRow>;

/** GET /api/v1/item/:id/sales - one Scylla `sales` row. */
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
  /** ISO 8601 with timezone offset. */
  sale_time: string;
};

/** GET /api/v1/config */
export type AppConfig = {
  /** Gilflux timeframe keys in ascending duration order (e.g. ["1h","3h","7d"]). */
  gilflux_timeframes: string[];
};

/** GET /api/v1/search_buyer - one row per purchase. */
export type BuyerSearchRow = {
  item_id: number;
  world_id: number;
  buyer_name: string;
  /** ISO 8601 with timezone offset. */
  sale_time: string;
  /** Null for rows written before sales_by_buyer gained the column. */
  quantity: number | null;
  /** quantity * unit_price. Null whenever quantity is. */
  total_price: number | null;
};
