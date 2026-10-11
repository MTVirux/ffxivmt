import { describe, expect, it } from 'vitest';
import {
  boardItemIds,
  buildBreakdown,
  combinedPlan,
  computeRows,
  createPlanner,
  fill,
  parsePositiveInt,
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
  it('skips units already bought', () => {
    expect(fill([L(1, 1, 33), L(50, 10, 34)], 2, 1)).toEqual({
      cost: 100,
      byWorld: [{ worldId: 34, quantity: 2, cost: 100 }],
    });
    expect(fill([L(5, 2)], 1, 2)).toBeNull();
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
    expect(node.cost).toBe(1 + 50);
    const priced = pricePlan(node, b);
    expect(priced?.total).toBe(node.cost);
    expect(priced?.purchases).toEqual([
      { id: SHARD, quantity: 2, cost: 51, byWorld: [{ worldId: 1, quantity: 2, cost: 51 }] },
    ]);
  });
  it('buys what it can so a scarce ingredient goes where it must', () => {
    const b = board({ [SHARD]: [L(1, 1)], [INGOT]: [L(100, 5)] });
    const node = createPlanner(recipes, b).craft(SWORD, 1);
    expect(node?.cost).toBe(101);
    expect(node?.children.map((c) => c.method)).toEqual(['buy', 'craft']);
    expect(pricePlan(node!, b)?.total).toBe(101);
  });
  it('switches a later sibling to buy once a shared ingredient runs out', () => {
    const b = board({ [SHARD]: [L(1, 1), L(500, 10)], [PLATE]: [L(20, 5)] });
    const node = createPlanner(recipes, b).craft(SWORD, 1);
    expect(node?.cost).toBe(21);
    expect(node?.children.map((c) => c.method)).toEqual(['craft', 'buy']);
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
  it('skips crafting for direct purchases only', () => {
    const [sword] = computeRows(catalogue, b, { kind: 'quantity', quantity: 2 }, undefined, true);
    expect(sword).toMatchObject({ buy_cost: 1800, craft_cost: null, best_cost: 1800, method: 'buy' });
  });
  it('prefers buying on a tie', () => {
    const [sword] = computeRows(catalogue, board({ [SWORD]: [L(200, 1)], [INGOT]: [L(100, 10)] }), {
      kind: 'quantity',
      quantity: 1,
    });
    expect(sword.method).toBe('buy');
  });
  it('prices a craft whose ingredients compete for a scarce material', () => {
    const [sword] = computeRows(
      {
        items: [{ id: SWORD, name: 'Sword', item_level: 55, seals: 300 }],
        recipes: [
          { item_id: SWORD, yield: 1, ingredients: [{ id: INGOT, amount: 1 }, { id: PLATE, amount: 1 }] },
          { item_id: INGOT, yield: 1, ingredients: [{ id: SHARD, amount: 1 }] },
          { item_id: PLATE, yield: 1, ingredients: [{ id: SHARD, amount: 1 }] },
        ],
        names: {},
      },
      board({ [SHARD]: [L(1, 1)], [INGOT]: [L(100, 5)] }),
      { kind: 'quantity', quantity: 1 },
    );
    expect(sword).toMatchObject({ buy_cost: null, craft_cost: 101, best_cost: 101, method: 'craft' });
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
  it('returns a single buy node for a bought row', () => {
    const b = board({ [SWORD]: [L(150, 5)], [INGOT]: [L(100, 10)] });
    const planner = createPlanner(catalogue.recipes, b);
    const [sword] = computeRows(catalogue, b, { kind: 'quantity', quantity: 1 }, planner);
    expect(buildBreakdown(sword, planner, b)).toEqual({
      method: 'buy',
      tree: { id: SWORD, quantity: 1, method: 'buy', cost: 150, children: [] },
      total: 150,
      purchases: [{ id: SWORD, quantity: 1, cost: 150, byWorld: [{ worldId: 1, quantity: 1, cost: 150 }] }],
    });
  });
  it('is null for an unpriceable row', () => {
    const b = board({});
    const planner = createPlanner(catalogue.recipes, b);
    const [, plate] = computeRows(catalogue, b, { kind: 'quantity', quantity: 1 }, planner);
    expect(buildBreakdown(plate, planner, b)).toBeNull();
  });
});

describe('combinedPlan', () => {
  const sword = catalogue.items[0];
  const plate = catalogue.items[1];
  const plan = (b: Board, target: number, directOnly = true, items = catalogue.items) =>
    combinedPlan(items, target, createPlanner(catalogue.recipes, b), b, directOnly);

  it('mixes the cheapest gil per seal across items', () => {
    const p = plan(board({ [SWORD]: [L(300, 2)], [PLATE]: [L(800, 5)] }), 1000);
    expect(p).toMatchObject({ target: 1000, seals: 1000, total: 1400, short: false });
    expect(p.entries).toEqual([
      { id: PLATE, quantity: 1, seals: 400, cost: 800, bought: 1, crafted: 0 },
      { id: SWORD, quantity: 2, seals: 600, cost: 600, bought: 2, crafted: 0 },
    ]);
    expect(p.purchases.map((x) => [x.id, x.quantity, x.cost])).toEqual([
      [SWORD, 2, 600],
      [PLATE, 1, 800],
    ]);
  });
  it('takes part of a stack and may overshoot the target', () => {
    const p = plan(board({ [SWORD]: [L(300, 10)] }), 700);
    expect(p).toMatchObject({ seals: 900, total: 900, short: false });
    expect(p.entries).toEqual([{ id: SWORD, quantity: 3, seals: 900, cost: 900, bought: 3, crafted: 0 }]);
  });
  it('reprices an item once its cheap listings are gone', () => {
    const p = plan(board({ [SWORD]: [L(300, 1), L(3000, 5)], [PLATE]: [L(800, 5)] }), 1100);
    expect(p.total).toBe(300 + 2 * 800);
    expect(p.entries.map((e) => [e.id, e.quantity])).toEqual([
      [PLATE, 2],
      [SWORD, 1],
    ]);
  });
  it('is short when the whole board cannot reach the target', () => {
    const p = plan(board({ [SWORD]: [L(300, 1)] }), 1000);
    expect(p).toMatchObject({ seals: 300, total: 300, short: true });
  });
  it('is empty when nothing can be priced', () => {
    expect(plan(board({}), 1000)).toEqual({
      target: 1000,
      seals: 0,
      total: 0,
      short: true,
      entries: [],
      purchases: [],
    });
  });
  it('only uses the items it is given', () => {
    const p = plan(board({ [SWORD]: [L(1, 9)], [PLATE]: [L(800, 5)] }), 400, true, [plate]);
    expect(p.entries.map((e) => e.id)).toEqual([PLATE]);
  });
  it('crafts copies when crafting is allowed and cheaper', () => {
    const b = board({ [SWORD]: [L(900, 5)], [INGOT]: [L(100, 10)] });
    const p = plan(b, 600, false, [sword]);
    expect(p.entries).toEqual([{ id: SWORD, quantity: 2, seals: 600, cost: 400, bought: 0, crafted: 2 }]);
    expect(p.purchases.map((x) => [x.id, x.quantity, x.cost])).toEqual([[INGOT, 4, 400]]);
    expect(plan(b, 600, true, [sword]).total).toBe(1800);
  });
  it('buys finished copies once crafting materials run out', () => {
    const p = plan(board({ [SWORD]: [L(900, 5)], [INGOT]: [L(100, 2)] }), 600, false, [sword]);
    expect(p.total).toBe(200 + 900);
    expect(p.entries).toEqual([{ id: SWORD, quantity: 2, seals: 600, cost: 1100, bought: 1, crafted: 1 }]);
  });
});

describe('boardItemIds', () => {
  it('lists every item and ingredient once, sorted', () => {
    expect(boardItemIds(catalogue)).toEqual([SWORD, INGOT, ORE, PLATE]);
  });
});

describe('parsePositiveInt', () => {
  it.each([
    ['5', 5],
    [' 12 ', 12],
    ['500000', 500000],
  ])('%j -> %i', (raw, expected) => {
    expect(parsePositiveInt(raw)).toBe(expected);
  });
  it.each(['', '0', '-3', '2.5', 'abc', '1e20'])('rejects %j', (raw) => {
    expect(parsePositiveInt(raw)).toBeNull();
  });
});
