export type Listing = { price: number; quantity: number; worldId: number };
/** Listings per item id, cheapest first. */
export type Board = ReadonlyMap<number, readonly Listing[]>;

export type GcSealsIngredient = { id: number; amount: number };
export type GcSealsRecipe = { item_id: number; yield: number; ingredients: GcSealsIngredient[] };
export type GcSealsItem = { id: number; name: string; item_level: number; seals: number };
/** GET /api/v1/tools/gc_seals/catalogue */
export type GcSealsCatalogue = {
  items: GcSealsItem[];
  recipes: GcSealsRecipe[];
  names: Record<string, string>;
};

export type GcSealsMode = { kind: 'quantity'; quantity: number } | { kind: 'seals'; sealTarget: number };

export const MAX_GC_QUANTITY = 999;
/** A Captain's seal cap; seals turned in past it are lost. */
export const MAX_SEAL_TARGET = 90_000;
export const DEFAULT_SEAL_TARGET = 10_000;

const MAX_DEPTH = 8;

export type WorldPurchase = { worldId: number; quantity: number; cost: number };
export type Fill = { cost: number; byWorld: WorldPurchase[] };

/**
 * Walks listings cheapest first and charges the last one only for the units needed - whole-stack
 * purchases are not modelled. Null when the board can't cover `qty`.
 */
export function fill(listings: readonly Listing[] | undefined, qty: number): Fill | null {
  if (qty <= 0) return { cost: 0, byWorld: [] };
  if (!listings) return null;

  let remaining = qty;
  let cost = 0;
  const byWorld = new Map<number, WorldPurchase>();
  for (const listing of listings) {
    if (remaining <= 0) break;
    const take = Math.min(remaining, listing.quantity);
    if (take <= 0) continue;
    remaining -= take;
    cost += take * listing.price;
    const world = byWorld.get(listing.worldId) ?? { worldId: listing.worldId, quantity: 0, cost: 0 };
    world.quantity += take;
    world.cost += take * listing.price;
    byWorld.set(listing.worldId, world);
  }
  return remaining > 0 ? null : { cost, byWorld: [...byWorld.values()] };
}

export type PlanNode = {
  id: number;
  quantity: number;
  method: 'buy' | 'craft';
  /** This node priced on its own; pricePlan re-prices the tree's purchases together. */
  cost: number;
  /** Set when method is 'craft'. */
  crafts?: number;
  children: PlanNode[];
};

export type Planner = {
  buy(id: number, qty: number): PlanNode | null;
  craft(id: number, qty: number): PlanNode | null;
  best(id: number, qty: number): PlanNode | null;
};

export function createPlanner(recipes: readonly GcSealsRecipe[], board: Board): Planner {
  const byItem = new Map<number, GcSealsRecipe[]>();
  for (const recipe of recipes) {
    const list = byItem.get(recipe.item_id) ?? [];
    list.push(recipe);
    byItem.set(recipe.item_id, list);
  }
  const memo = new Map<string, PlanNode | null>();

  const buy = (id: number, qty: number): PlanNode | null => {
    const filled = fill(board.get(id), qty);
    return filled ? { id, quantity: qty, method: 'buy', cost: filled.cost, children: [] } : null;
  };

  const craft = (id: number, qty: number, path: Set<number>): PlanNode | null => {
    const options = byItem.get(id);
    if (!options || path.size >= MAX_DEPTH) return null;

    path.add(id);
    let cheapest: PlanNode | null = null;
    for (const recipe of options) {
      const crafts = Math.ceil(qty / recipe.yield);
      const children: PlanNode[] = [];
      let cost = 0;
      for (const ingredient of recipe.ingredients) {
        const child = path.has(ingredient.id) ? null : best(ingredient.id, ingredient.amount * crafts, path);
        if (!child) {
          cost = Number.POSITIVE_INFINITY;
          break;
        }
        children.push(child);
        cost += child.cost;
      }
      if (Number.isFinite(cost) && (!cheapest || cost < cheapest.cost)) {
        cheapest = { id, quantity: qty, method: 'craft', cost, crafts, children };
      }
    }
    path.delete(id);
    return cheapest;
  };

  const best = (id: number, qty: number, path: Set<number>): PlanNode | null => {
    const key = `${id}:${qty}`;
    const cached = memo.get(key);
    if (cached !== undefined) return cached;

    const bought = buy(id, qty);
    const crafted = craft(id, qty, path);
    const result = !bought ? crafted : !crafted ? bought : crafted.cost < bought.cost ? crafted : bought;
    memo.set(key, result);
    return result;
  };

  return {
    buy,
    craft: (id, qty) => craft(id, qty, new Set()),
    best: (id, qty) => best(id, qty, new Set()),
  };
}

export type PricedPurchase = { id: number; quantity: number; cost: number; byWorld: WorldPurchase[] };
export type PricedPlan = { total: number; purchases: PricedPurchase[] };

function collectPurchases(node: PlanNode, into: Map<number, number>): Map<number, number> {
  if (node.method === 'buy') {
    into.set(node.id, (into.get(node.id) ?? 0) + node.quantity);
  } else {
    for (const child of node.children) collectPurchases(child, into);
  }
  return into;
}

/** Re-prices the plan's purchases summed per item, so one cheap listing is never counted twice. */
export function pricePlan(node: PlanNode, board: Board): PricedPlan | null {
  const purchases: PricedPurchase[] = [];
  let total = 0;
  for (const [id, quantity] of collectPurchases(node, new Map())) {
    const filled = fill(board.get(id), quantity);
    if (!filled) return null;
    purchases.push({ id, quantity, cost: filled.cost, byWorld: filled.byWorld });
    total += filled.cost;
  }
  return { total, purchases };
}

export type GcSealsRow = {
  id: number;
  name: string;
  item_level: number;
  /** Seals for one turn-in. */
  seals: number;
  count: number;
  total_seals: number;
  buy_cost: number | null;
  craft_cost: number | null;
  best_cost: number | null;
  method: 'buy' | 'craft' | null;
  gil_per_seal: number | null;
};

export function itemCount(mode: GcSealsMode, seals: number): number {
  return mode.kind === 'quantity' ? mode.quantity : Math.ceil(mode.sealTarget / seals);
}

export function computeRows(
  catalogue: GcSealsCatalogue,
  board: Board,
  mode: GcSealsMode,
  planner: Planner = createPlanner(catalogue.recipes, board),
): GcSealsRow[] {
  return catalogue.items.map((item) => {
    const count = itemCount(mode, item.seals);
    const totalSeals = count * item.seals;
    const buyCost = fill(board.get(item.id), count)?.cost ?? null;
    const craftNode = planner.craft(item.id, count);
    const craftCost = craftNode ? (pricePlan(craftNode, board)?.total ?? null) : null;
    const bestCost = buyCost === null ? craftCost : craftCost === null ? buyCost : Math.min(buyCost, craftCost);
    return {
      id: item.id,
      name: item.name,
      item_level: item.item_level,
      seals: item.seals,
      count,
      total_seals: totalSeals,
      buy_cost: buyCost,
      craft_cost: craftCost,
      best_cost: bestCost,
      method: bestCost === null ? null : bestCost === buyCost ? 'buy' : 'craft',
      gil_per_seal: bestCost === null ? null : bestCost / totalSeals,
    };
  });
}

export type GcSealsBreakdown = {
  method: 'buy' | 'craft';
  tree: PlanNode;
  total: number;
  purchases: PricedPurchase[];
};

export function buildBreakdown(row: GcSealsRow, planner: Planner, board: Board): GcSealsBreakdown | null {
  if (row.method === null) return null;
  const tree = row.method === 'buy' ? planner.buy(row.id, row.count) : planner.craft(row.id, row.count);
  if (!tree) return null;
  const priced = pricePlan(tree, board);
  return priced ? { method: row.method, tree, total: priced.total, purchases: priced.purchases } : null;
}

/** Every id the market board needs to be asked about. */
export function boardItemIds(catalogue: GcSealsCatalogue): number[] {
  const ids = new Set<number>(catalogue.items.map((i) => i.id));
  for (const recipe of catalogue.recipes) {
    ids.add(recipe.item_id);
    for (const ingredient of recipe.ingredients) ids.add(ingredient.id);
  }
  return [...ids].sort((a, b) => a - b);
}

export function parseBoundedInt(raw: string, max: number): number | null {
  const trimmed = raw.trim();
  if (trimmed === '') return null;
  const n = Number(trimmed);
  if (!Number.isInteger(n) || n < 1) return null;
  return Math.min(n, max);
}
