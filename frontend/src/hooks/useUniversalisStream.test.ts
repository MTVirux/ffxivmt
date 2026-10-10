import { QueryClient } from '@tanstack/react-query';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { cachedItemName, dropExpired, resolveItemName } from './useUniversalisStream';
import type { EnrichedSale } from './useUniversalisStream';

const VERSION = '0123456789abcdef';
const CONFIG = { gilflux_timeframes: ['1h'], item_names_version: VERSION };
const NAMES_URL = `/api/v1/item/names?v=${VERSION}`;

function item(id: number, name: string) {
  return { id, name, marketable: true, craftable: false, icon_image: 0 };
}

function stubApi(routes: Record<string, unknown>) {
  const fetchMock = vi.fn(async (url: string) =>
    url in routes
      ? Response.json({ status: true, message: '', data: routes[url] })
      : new Response(null, { status: 500 }),
  );
  vi.stubGlobal('fetch', fetchMock);
  return () => fetchMock.mock.calls.map(([url]) => url);
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('resolveItemName', () => {
  it('reads the name from the versioned list', async () => {
    const requested = stubApi({ '/api/v1/config': CONFIG, [NAMES_URL]: { '1': 'Gil' } });
    expect(await resolveItemName(new QueryClient(), 1)).toBe('Gil');
    expect(requested()).toEqual(['/api/v1/config', NAMES_URL]);
  });

  it('falls back to the item endpoint for ids missing from the list', async () => {
    stubApi({
      '/api/v1/config': CONFIG,
      [NAMES_URL]: { '1': 'Gil' },
      '/api/v1/item/2': item(2, 'Fire Shard'),
    });
    expect(await resolveItemName(new QueryClient(), 2)).toBe('Fire Shard');
  });

  it('falls back to the item endpoint when config has no version', async () => {
    const requested = stubApi({
      '/api/v1/config': { gilflux_timeframes: ['1h'] },
      '/api/v1/item/2': item(2, 'Fire Shard'),
    });
    expect(await resolveItemName(new QueryClient(), 2)).toBe('Fire Shard');
    expect(requested()).toEqual(['/api/v1/config', '/api/v1/item/2']);
  });

  it('falls back to the item endpoint when the list request fails', async () => {
    stubApi({ '/api/v1/config': CONFIG, '/api/v1/item/2': item(2, 'Fire Shard') });
    expect(await resolveItemName(new QueryClient(), 2)).toBe('Fire Shard');
  });

  it('returns the id when nothing resolves', async () => {
    stubApi({ '/api/v1/config': CONFIG });
    expect(await resolveItemName(new QueryClient(), 3)).toBe('3');
  });
});

describe('cachedItemName', () => {
  it('serves names from the loaded list without a request', async () => {
    stubApi({ '/api/v1/config': CONFIG, [NAMES_URL]: { '1': 'Gil', '2': 'Fire Shard' } });
    const queryClient = new QueryClient();
    expect(cachedItemName(queryClient, 2)).toBeUndefined();
    await resolveItemName(queryClient, 1);
    expect(cachedItemName(queryClient, 2)).toBe('Fire Shard');
  });
});

describe('dropExpired', () => {
  const NOW = 1_760_000_000;

  function sale(key: string, saleTime: number, receivedAt: number): EnrichedSale {
    return {
      key,
      itemId: 1,
      itemName: 'Gil',
      worldName: 'Cerberus',
      buyerName: 'Buyer',
      hq: false,
      quantity: 1,
      unitPrice: 1,
      saleTime,
      receivedAt,
    };
  }

  it('keeps hours-old sales that arrived recently', () => {
    const late = sale('late', NOW - 14 * 3600, NOW - 5);
    expect(dropExpired([late], NOW)).toEqual([late]);
  });

  it('drops sales that arrived over ten minutes ago', () => {
    const fresh = sale('fresh', NOW - 60, NOW - 60);
    const stale = sale('stale', NOW - 700, NOW - 700);
    expect(dropExpired([fresh, stale], NOW)).toEqual([fresh]);
  });
});
