import { describe, expect, it } from 'vitest';
import { formatConnected, formatCount, formatPercent, formatRate } from './statusFormat';

describe('formatRate', () => {
  it('formats per-second rates compactly', () => {
    expect(formatRate(12.34)).toBe('12.3/s');
    expect(formatRate(1234)).toBe('1.2K/s');
    expect(formatRate(0)).toBe('0/s');
  });

  it('renders missing values as a hyphen', () => {
    expect(formatRate(null)).toBe('-');
    expect(formatRate(undefined)).toBe('-');
    expect(formatRate(Number.NaN)).toBe('-');
  });
});

describe('formatPercent', () => {
  it('formats a ratio as a percentage', () => {
    expect(formatPercent(0.004)).toBe('0.4%');
    expect(formatPercent(0.072)).toBe('7.2%');
    expect(formatPercent(0)).toBe('0.0%');
  });

  it('renders missing values as a hyphen', () => {
    expect(formatPercent(null)).toBe('-');
  });
});

describe('formatCount', () => {
  it('rounds to a whole number', () => {
    expect(formatCount(78)).toBe('78');
    expect(formatCount(77.6)).toBe('78');
    expect(formatCount(null)).toBe('-');
  });
});

describe('formatConnected', () => {
  it('shows connected out of total', () => {
    expect(formatConnected(78, 80)).toBe('78 / 80');
  });

  it('falls back to the count alone without a total', () => {
    expect(formatConnected(78, null)).toBe('78');
    expect(formatConnected(78, undefined)).toBe('78');
  });

  it('renders a missing count as a hyphen', () => {
    expect(formatConnected(null, 80)).toBe('-');
  });
});
