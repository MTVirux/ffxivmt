import { describe, expect, it, vi } from 'vitest';
import { fetchListings, listingsUrl, parseListings, type FetchLike } from './universalisListings';

const ok = (body: unknown) => new Response(JSON.stringify(body), { status: 200 });

const idsInUrl = (url: string) => url.split('/')[6].split('?')[0].split(',');

describe('listingsUrl', () => {
  it('asks for listings only, without sale history', () => {
    expect(listingsUrl('Light', [5057, 5058])).toBe(
      'https://universalis.app/api/v2/Light/5057,5058?entries=0&fields=items.listings.pricePerUnit,items.listings.quantity,items.listings.worldID',
    );
  });
  it('uses the single-item field paths for one id', () => {
    expect(listingsUrl('North-America', [5057])).toBe(
      'https://universalis.app/api/v2/North-America/5057?entries=0&fields=listings.pricePerUnit,listings.quantity,listings.worldID',
    );
  });
});

describe('parseListings', () => {
  it('sorts cheapest first and fills in the world for world-scope answers', () => {
    const board = parseListings(
      { items: { '5057': { listings: [{ pricePerUnit: 9, quantity: 1 }, { pricePerUnit: 3, quantity: 2 }] } } },
      [5057, 5058],
      33,
    );
    expect(board.get(5057)).toEqual([
      { price: 3, quantity: 2, worldId: 33 },
      { price: 9, quantity: 1, worldId: 33 },
    ]);
    expect(board.get(5058)).toEqual([]);
  });
  it('keeps per-listing worlds on DC answers', () => {
    const board = parseListings(
      { items: { '5057': { listings: [{ pricePerUnit: 3, quantity: 2, worldID: 40 }] } } },
      [5057],
      0,
    );
    expect(board.get(5057)).toEqual([{ price: 3, quantity: 2, worldId: 40 }]);
  });
  it('reads the flat single-item shape', () => {
    const board = parseListings({ listings: [{ pricePerUnit: 4, quantity: 1, worldID: 40 }] }, [5057], 0);
    expect(board.get(5057)).toEqual([{ price: 4, quantity: 1, worldId: 40 }]);
  });
});

describe('fetchListings', () => {
  const ids = Array.from({ length: 250 }, (_, i) => i + 1);

  it('fetches in chunks of 100 and merges the board', async () => {
    const fetchImpl = vi.fn<FetchLike>((url) =>
      Promise.resolve(
        ok({
          items: Object.fromEntries(
            idsInUrl(url).map((id) => [id, { listings: [{ pricePerUnit: Number(id), quantity: 1, worldID: 40 }] }]),
          ),
        }),
      ),
    );
    const progress: number[] = [];

    const result = await fetchListings('Light', ids, { fetchImpl, onProgress: (p) => progress.push(p.done) });

    expect(fetchImpl).toHaveBeenCalledTimes(3);
    expect(result.board.size).toBe(250);
    expect(result.board.get(250)).toEqual([{ price: 250, quantity: 1, worldId: 40 }]);
    expect(result).toMatchObject({ failedChunks: 0, totalChunks: 3 });
    expect(progress).toEqual([1, 2, 3]);
  });

  it('sends no headers so the browser skips the CORS preflight', async () => {
    const fetchImpl = vi.fn<FetchLike>(() => Promise.resolve(ok({ items: {} })));
    await fetchListings('Light', [1, 2], { fetchImpl });
    expect(Object.keys(fetchImpl.mock.calls[0][1])).toEqual(['signal']);
  });

  it('fills in the world id for a world-scope fetch', async () => {
    const fetchImpl = vi.fn<FetchLike>(() =>
      Promise.resolve(ok({ items: { '1': { listings: [{ pricePerUnit: 5, quantity: 1 }] } } })),
    );
    const result = await fetchListings('Odin', [1, 2], { fetchImpl, worldId: 33 });
    expect(result.board.get(1)).toEqual([{ price: 5, quantity: 1, worldId: 33 }]);
  });

  it('retries a chunk once after a 503', async () => {
    const fetchImpl = vi
      .fn<FetchLike>()
      .mockResolvedValueOnce(new Response('', { status: 503 }))
      .mockResolvedValueOnce(ok({ items: { '1': { listings: [{ pricePerUnit: 5, quantity: 1, worldID: 40 }] } } }));

    const result = await fetchListings('Light', [1, 2], { fetchImpl, retryDelayMs: 0 });

    expect(fetchImpl).toHaveBeenCalledTimes(2);
    expect(result.failedChunks).toBe(0);
    expect(result.board.get(1)).toHaveLength(1);
  });

  it('counts chunks that keep failing as partial', async () => {
    const fetchImpl = vi.fn<FetchLike>((url) =>
      Promise.resolve(url.includes('/1,2,') ? new Response('', { status: 500 }) : ok({ items: {} })),
    );

    const result = await fetchListings('Light', ids, { fetchImpl, retryDelayMs: 0 });

    expect(result).toMatchObject({ failedChunks: 1, totalChunks: 3 });
    expect(fetchImpl).toHaveBeenCalledTimes(4);
    expect(result.board.has(1)).toBe(false);
  });

  it('does not retry a 4xx', async () => {
    const fetchImpl = vi.fn<FetchLike>(() => Promise.resolve(new Response('', { status: 404 })));
    await expect(fetchListings('Light', [1, 2], { fetchImpl, retryDelayMs: 0 })).rejects.toThrow('Universalis');
    expect(fetchImpl).toHaveBeenCalledTimes(1);
  });

  it('throws when every chunk fails', async () => {
    const fetchImpl = vi.fn<FetchLike>(() => Promise.reject(new TypeError('network')));
    await expect(fetchListings('Light', [1, 2], { fetchImpl, retryDelayMs: 0 })).rejects.toThrow('Universalis');
  });
});
