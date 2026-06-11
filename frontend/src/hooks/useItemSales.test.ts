import { describe, it, expect } from 'vitest';
import { buildSalesPath } from './useItemSales';

describe('buildSalesPath', () => {
  it('builds a world-scoped path', () => {
    expect(buildSalesPath(42, { kind: 'world', name: 'Gilgamesh', worldId: 1 }, 100)).toBe(
      '/item/42/sales?target_location=Gilgamesh&limit=100',
    );
  });

  it('builds a datacenter-scoped path', () => {
    expect(buildSalesPath(42, { kind: 'datacenter', name: 'Aether' }, 50)).toBe(
      '/item/42/sales?target_location=Aether&limit=50',
    );
  });

  it('url-encodes names containing spaces', () => {
    expect(buildSalesPath(7, { kind: 'datacenter', name: 'Test DC' }, 25)).toBe(
      '/item/7/sales?target_location=Test%20DC&limit=25',
    );
  });

  it('builds a region-scoped path and never leaks worldId into the url', () => {
    const path = buildSalesPath(3, { kind: 'region', name: 'North-America', worldId: 1 }, 100);
    expect(path).toBe('/item/3/sales?target_location=North-America&limit=100');
    expect(path).not.toContain('world');
  });
});
