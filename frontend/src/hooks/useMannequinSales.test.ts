import { describe, it, expect } from 'vitest';
import type { MannequinFilters } from '../lib/mannequin';
import { buildMannequinPath } from './useMannequinSales';

const ANY: MannequinFilters = {
  datacenter: '',
  world: '',
  minUnitPrice: 0,
  quality: 'all',
  buyer: '',
};

describe('buildMannequinPath', () => {
  it('sends no params for unfiltered filters', () => {
    expect(buildMannequinPath(ANY, null)).toBe('/mannequin_sales?');
  });

  it('sends the trimmed buyer name', () => {
    expect(buildMannequinPath({ ...ANY, buyer: '  Some One ' }, null)).toBe(
      '/mannequin_sales?buyer_name=Some+One',
    );
  });

  it('leaves out a blank buyer', () => {
    expect(buildMannequinPath({ ...ANY, buyer: '   ' }, null)).not.toContain('buyer_name');
  });

  it('combines the buyer with the other filters', () => {
    const filters: MannequinFilters = {
      datacenter: 'Light',
      world: 'Odin',
      minUnitPrice: 5000,
      quality: 'hq',
      buyer: 'Some',
    };
    expect(buildMannequinPath(filters, 123)).toBe(
      '/mannequin_sales?target_location=Odin&before=123&min_unit_price=5000&hq=true&buyer_name=Some',
    );
  });
});
