import type { Listing } from './gcSeals';

const BASE = 'https://universalis.app/api/v2';
const CHUNK_SIZE = 100;
const CONCURRENCY = 4;

export type FetchLike = (url: string, init: RequestInit) => Promise<Response>;
export type ListingsProgress = { done: number; total: number };
export type ListingsResult = { board: Map<number, Listing[]>; failedChunks: number; totalChunks: number };

type RawListing = { pricePerUnit?: unknown; quantity?: unknown; worldID?: unknown };
type RawResponse = { items?: Record<string, { listings?: RawListing[] }>; listings?: RawListing[] };

/** Universalis answers a single id with a flat object instead of an `items` map, so the field paths differ. */
export function listingsUrl(location: string, ids: readonly number[]): string {
  const prefix = ids.length === 1 ? 'listings' : 'items.listings';
  const fields = ['pricePerUnit', 'quantity', 'worldID'].map((f) => `${prefix}.${f}`).join(',');
  return `${BASE}/${encodeURIComponent(location)}/${ids.join(',')}?entries=0&fields=${fields}`;
}

/** World-scope answers carry no per-listing worldID; `fallbackWorldId` fills it in. */
export function parseListings(
  body: RawResponse,
  ids: readonly number[],
  fallbackWorldId: number,
): Map<number, Listing[]> {
  const items = body.items ?? (ids.length === 1 ? { [ids[0]]: { listings: body.listings } } : {});
  const board = new Map<number, Listing[]>();
  for (const id of ids) {
    const listings: Listing[] = [];
    for (const raw of items[id]?.listings ?? []) {
      if (typeof raw.pricePerUnit !== 'number' || typeof raw.quantity !== 'number' || raw.quantity <= 0) continue;
      listings.push({
        price: raw.pricePerUnit,
        quantity: raw.quantity,
        worldId: typeof raw.worldID === 'number' ? raw.worldID : fallbackWorldId,
      });
    }
    board.set(id, listings.sort((a, b) => a.price - b.price));
  }
  return board;
}

export type FetchListingsOptions = {
  /** The selected world, for world-scope fetches. */
  worldId?: number;
  signal?: AbortSignal;
  onProgress?: (progress: ListingsProgress) => void;
  fetchImpl?: FetchLike;
  retryDelayMs?: number;
};

export async function fetchListings(
  location: string,
  ids: readonly number[],
  options: FetchListingsOptions = {},
): Promise<ListingsResult> {
  const {
    worldId = 0,
    signal,
    onProgress,
    fetchImpl = (url: string, init: RequestInit) => fetch(url, init),
    retryDelayMs = 500,
  } = options;

  const chunks: number[][] = [];
  for (let i = 0; i < ids.length; i += CHUNK_SIZE) chunks.push(ids.slice(i, i + CHUNK_SIZE));

  const board = new Map<number, Listing[]>();
  let next = 0;
  let done = 0;
  let failed = 0;

  const load = async (chunk: number[]): Promise<Map<number, Listing[]> | null> => {
    for (let attempt = 0; attempt < 2; attempt++) {
      if (attempt > 0) await sleep(retryDelayMs);
      try {
        // A custom header would trigger a CORS preflight, which Universalis rejects with 405.
        const res = await fetchImpl(listingsUrl(location, chunk), { signal });
        if (res.ok) return parseListings((await res.json()) as RawResponse, chunk, worldId);
        if (res.status !== 429 && res.status < 500) return null;
      } catch (err) {
        if (signal?.aborted) throw err;
      }
    }
    return null;
  };

  const worker = async () => {
    while (next < chunks.length) {
      const chunk = chunks[next++];
      const result = await load(chunk);
      if (result) {
        for (const [id, listings] of result) board.set(id, listings);
      } else {
        failed++;
      }
      done++;
      onProgress?.({ done, total: chunks.length });
    }
  };

  await Promise.all(Array.from({ length: Math.min(CONCURRENCY, chunks.length) }, worker));

  if (chunks.length > 0 && failed === chunks.length) {
    throw new Error('Universalis did not answer any market board request. Try again in a minute.');
  }
  return { board, failedChunks: failed, totalChunks: chunks.length };
}

function sleep(ms: number) {
  return new Promise<void>((resolve) => setTimeout(resolve, ms));
}
