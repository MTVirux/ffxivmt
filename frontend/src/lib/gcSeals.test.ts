import { describe, expect, it } from 'vitest';
import {
  boardItemIds,
  buildBreakdown,
  computeRows,
  createPlanner,
  fill,
  parseBoundedInt,
  pricePlan,
  type Board,
  type GcSealsCatalogue,
  type GcSealsRecipe,
  type Listing,
} from './gcSeals';

const L = (price: number, quantity: number, worldId = 1): Listing => ({ price, quantity, worldId });
const board = (entries: Record<number, Listing[]>): Board =>
  new Map(Object.entries(entries).map(([id, listings]) => [Number(id), listings]));

const SHARD = 2;
const SWORD = 100;
const INGOT = 200;
const ORE = 300;
const PLATE = 400;

describe('fill', () => {
  it('walks listings cheapest first', () => {
    expect(fill([L(10, 2), L(20, 5)], 4)?.cost).toBe(10 * 2 + 20 * 2);
  });
  it('charges only the units needed from the last listing', () => {
    expect(fill([L(10, 99)], 3)?.cost).toBe(30);
  });
  it('returns null when the board cannot cover the quantity', () => {
    expect(fill([L(10, 2)], 3)).toBeNull();
    expect(fill(undefined, 1)).toBeNull();
    expect(fill([], 1)).toBeNull();
  });
  it('costs nothing for zero units', () => {
    expect(fill(undefined, 0)).toEqual({ cost: 0, byWorld: [] });
  });
  it('groups the purchase by world', () => {
    expect(fill([L(5, 1, 33), L(6, 1, 34), L(7, 5, 33)], 4)?.byWorld).toEqual([
      { worldId: 33, quantity: 3, cost: 5 + 7 * 2 },
      { worldId: 34, quantity: 1, cost: 6 },
    ]);
  });
});

describe('createPlanner', () => {
  const recipes: GcSealsRecipe[] = [
    { item_id: SWORD, yield: 1, ingredients: [{ id: INGOT, amount: 2 }, { id: SHARD, amount: 3 }] },
    { item_id: INGOT, yield: 1, ingredients: [{ id: ORE, amount: 3 }] },
  ];

  it('buys when buying is cheaper than crafting', () => {
    const planner = createPlanner(recipes, board({ [INGOT]: [L(10, 10)], [ORE]: [L(5, 30)] }));
    expect(planner.best(INGOT, 2)).toMatchObject({ method: 'buy', cost: 20 });
  });
  it('crafts when the ingredients are cheaper', () => {
    const planner = createPlanner(recipes, board({ [INGOT]: [L(100, 10)], [ORE]: [L(5, 30)] }));
    expect(planner.best(INGOT, 2)).toMatchObject({ method: 'craft', cost: 30, crafts: 2 });
  });
  it('picks buy or craft at every level of the tree', () => {
    const planner = createPlanner(
      recipes,
      board({ [SWORD]: [L(1000, 1)], [INGOT]: [L(100, 10)], [ORE]: [L(5, 30)], [SHARD]: [L(1, 99)] }),
    );
    const node = planner.craft(SWORD, 1);
    expect(node).toMatchObject({ method: 'craft', cost: 2 * 15 + 3 });
    expect(node?.children.map((c) => c.method)).toEqual(['craft', 'buy']);
  });
  it('rounds crafts up for recipes that yield several', () => {
    const planner = createPlanner(
      [{ item_id: PLATE, yield: 3, ingredients: [{ id: ORE, amount: 2 }] }],
      board({ [ORE]: [L(5, 30)] }),
    );
    expect(planner.best(PLATE, 4)).toMatchObject({ method: 'craft', crafts: 2, cost: 2 * 2 * 5 });
  });
  it('uses the cheapest of several recipes', () => {
    const planner = createPlanner(
      [
        { item_id: INGOT, yield: 1, ingredients: [{ id: ORE, amount: 3 }] },
        { item_id: INGOT, yield: 1, ingredients: [{ id: SHARD, amount: 1 }] },
      ],
      board({ [ORE]: [L(5, 30)], [SHARD]: [L(1, 99)] }),
    );
    expect(planner.best(INGOT, 1)?.children.map((c) => c.id)).toEqual([SHARD]);
  });
  it('returns null when neither path can be priced', () => {
    const planner = createPlanner(recipes, board({}));
    expect(planner.best(INGOT, 1)).toBeNull();
    expect(planner.craft(SWORD, 1)).toBeNull();
  });
  it('survives a recipe cycle', () => {
    const planner = createPlanner(
      [
        { item_id: 1, yield: 1, ingredients: [{ id: 2, amount: 1 }] },
        { item_id: 2, yield: 1, ingredients: [{ id: 1, amount: 1 }] },
      ],
      board({ 2: [L(7, 1)] }),
    );
    expect(planner.best(1, 1)).toMatchObject({ method: 'craft', cost: 7 });
  });
});

describe('pricePlan', () => {
  const recipes: GcSealsRecipe[] = [
    { item_id: SWORD, yield: 1, ingredients: [{ id: INGOT, amount: 1 }, { id: PLATE, amount: 1 }] },
    { item_id: INGOT, yield: 1, ingredients: [{ id: SHARD, amount: 1 }] },
    { item_id: PLATE, yield: 1, ingredients: [{ id: SHARD, amount: 1 }] },
  ];

  it('prices a shared ingredient once across the tree', () => {
    const b = board({ [SHARD]: [L(1, 1), L(50, 10)] });
    const node = createPlanner(recipes, b).craft(SWORD, 1)!;
    expect(node.cost).toBe(2);
    const priced = pricePlan(node, b);
    expect(priced?.total).toBe(1 + 50);
    expect(priced?.purchases).toEqual([
      { id: SHARD, quantity: 2, cost: 51, byWorld: [{ worldId: 1, quantity: 2, cost: 51 }] },
    ]);
  });
  it('is null when the summed quantity outruns the board', () => {
    const b = board({ [SHARD]: [L(1, 1)] });
    const node = createPlanner(recipes, b).craft(SWORD, 1)!;
    expect(pricePlan(node, b)).toBeNull();
  });
});

const catalogue: GcSealsCatalogue = {
  items: [
    { id: SWORD, name: 'Sword', item_level: 55, seals: 300 },
    { id: PLATE, name: 'Plate', item_level: 60, seals: 400 },
  ],
  recipes: [
    { item_id: SWORD, yield: 1, ingredients: [{ id: INGOT, amount: 2 }] },
    { item_id: PLATE, yield: 1, ingredients: [{ id: ORE, amount: 1 }] },
  ],
  names: {},
};

describe('computeRows', () => {
  const b = board({ [SWORD]: [L(900, 5)], [INGOT]: [L(100, 10)] });

  it('prices each item for the chosen quantity', () => {
    const [sword] = computeRows(catalogue, b, { kind: 'quantity', quantity: 2 });
    expect(sword).toEqual({
      id: SWORD,
      name: 'Sword',
      item_level: 55,
      seals: 300,
      count: 2,
      total_seals: 600,
      buy_cost: 1800,
      craft_cost: 400,
      best_cost: 400,
      method: 'craft',
      gil_per_seal: 400 / 600,
    });
  });
  it('derives the count from a seal target', () => {
    const [sword] = computeRows(catalogue, b, { kind: 'seals', sealTarget: 1000 });
    expect(sword.count).toBe(4);
    expect(sword.total_seals).toBe(1200);
  });
  it('marks items the board cannot cover', () => {
    const [, plate] = computeRows(catalogue, b, { kind: 'seals', sealTarget: 90_000 });
    expect(plate).toMatchObject({ buy_cost: null, craft_cost: null, best_cost: null, method: null, gil_per_seal: null });
  });
  it('prefers buying on a tie', () => {
    const [sword] = computeRows(catalogue, board({ [SWORD]: [L(200, 1)], [INGOT]: [L(100, 10)] }), {
      kind: 'quantity',
      quantity: 1,
    });
    expect(sword.method).toBe('buy');
  });
});

describe('buildBreakdown', () => {
  it('returns the chosen plan with its shopping list', () => {
    const b = board({ [SWORD]: [L(900, 5)], [INGOT]: [L(100, 10)] });
    const planner = createPlanner(catalogue.recipes, b);
    const [sword] = computeRows(catalogue, b, { kind: 'quantity', quantity: 1 }, planner);
    const breakdown = buildBreakdown(sword, planner, b);
    expect(breakdown?.method).toBe('craft');
    expect(breakdown?.total).toBe(200);
    expect(breakdown?.purchases.map((p) => [p.id, p.quantity])).toEqual([[INGOT, 2]]);
  });
  it('is null for an unpriceable row', () => {
    const b = board({});
    const planner = createPlanner(catalogue.recipes, b);
    const [, plate] = computeRows(catalogue, b, { kind: 'quantity', quantity: 1 }, planner);
    expect(buildBreakdown(plate, planner, b)).toBeNull();
  });
});

describe('boardItemIds', () => {
  it('lists every item and ingredient once, sorted', () => {
    expect(boardItemIds(catalogue)).toEqual([SWORD, INGOT, ORE, PLATE]);
  });
});

describe('parseBoundedInt', () => {
  it.each([
    ['5', 5],
    [' 12 ', 12],
    ['5000', 999],
  ])('%j -> %i', (raw, expected) => {
    expect(parseBoundedInt(raw, 999)).toBe(expected);
  });
  it.each(['', '0', '-3', '2.5', 'abc'])('rejects %j', (raw) => {
    expect(parseBoundedInt(raw, 999)).toBeNull();
  });
});
