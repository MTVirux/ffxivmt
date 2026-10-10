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
 * purchases are not modelled. The cheapest `skip` units are taken as already bought elsewhere in
 * the plan. Null when the board can't cover `skip + qty`.
 */
export function fill(listings: readonly Listing[] | undefined, qty: number, skip = 0): Fill | null {
  if (qty <= 0) return { cost: 0, byWorld: [] };
  if (!listings) return null;

  let toSkip = skip;
  let remaining = qty;
  let cost = 0;
  const byWorld = new Map<number, WorldPurchase>();
  for (const listing of listings) {
    if (remaining <= 0) break;
    if (listing.quantity <= 0) continue;
    const skipped = Math.min(toSkip, listing.quantity);
    toSkip -= skipped;
    const take = Math.min(remaining, listing.quantity - skipped);
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
  /** Priced after the listings earlier parts of the plan bought, so a tree's costs add up to its joint price. */
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

/** Units of each item id the plan has bought so far. */
type Used = ReadonlyMap<number, number>;
type Choice = { node: PlanNode; used: Used };

export function createPlanner(recipes: readonly GcSealsRecipe[], board: Board): Planner {
  const byItem = new Map<number, GcSealsRecipe[]>();
  for (const recipe of recipes) {
    const list = byItem.get(recipe.item_id) ?? [];
    list.push(recipe);
    byItem.set(recipe.item_id, list);
  }
  const offset = (used: Used, id: number) => used.get(id) ?? 0;

  const buy = (id: number, qty: number, used: Used): Choice | null => {
    const filled = fill(board.get(id), qty, offset(used, id));
    if (!filled) return null;
    return {
      node: { id, quantity: qty, method: 'buy', cost: filled.cost, children: [] },
      used: new Map(used).set(id, offset(used, id) + qty),
    };
  };

  const craftWith = (recipe: GcSealsRecipe, qty: number, used: Used, path: Set<number>): Choice | null => {
    const crafts = Math.ceil(qty / recipe.yield);
    const wants = recipe.ingredients.map((ingredient) => ({ id: ingredient.id, qty: ingredient.amount * crafts }));
    const buyable = wants.map((want) => fill(board.get(want.id), want.qty, offset(used, want.id)) !== null);
    // Ingredients that can only be crafted get first claim on shared materials.
    const order = [...wants.keys()].sort((a, b) => Number(buyable[a]) - Number(buyable[b]));

    const children: PlanNode[] = [];
    let current = used;
    let cost = 0;
    for (const i of order) {
      const child = path.has(wants[i].id) ? null : best(wants[i].id, wants[i].qty, current, path);
      if (!child) return null;
      children[i] = child.node;
      cost += child.node.cost;
      current = child.used;
    }
    return { node: { id: recipe.item_id, quantity: qty, method: 'craft', cost, crafts, children }, used: current };
  };

  const craft = (id: number, qty: number, used: Used, path: Set<number>): Choice | null => {
    const options = byItem.get(id);
    if (!options || path.size >= MAX_DEPTH) return null;

    path.add(id);
    let cheapest: Choice | null = null;
    for (const recipe of options) {
      const option = craftWith(recipe, qty, used, path);
      if (option && (!cheapest || option.node.cost < cheapest.node.cost)) cheapest = option;
    }
    path.delete(id);
    return cheapest;
  };

  const best = (id: number, qty: number, used: Used, path: Set<number>): Choice | null => {
    const bought = buy(id, qty, used);
    const crafted = craft(id, qty, used, path);
    if (!bought) return crafted;
    if (!crafted) return bought;
    return crafted.node.cost < bought.node.cost ? crafted : bought;
  };

  return {
    buy: (id, qty) => buy(id, qty, new Map())?.node ?? null,
    craft: (id, qty) => craft(id, qty, new Map(), new Set())?.node ?? null,
    best: (id, qty) => best(id, qty, new Map(), new Set())?.node ?? null,
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
    const craftCost = planner.craft(item.id, count)?.cost ?? null;
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
