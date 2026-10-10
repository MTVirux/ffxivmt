import { describe, expect, it } from 'vitest';
import {
  formatAgo,
  formatConnected,
  formatCount,
  formatDay,
  formatPercent,
  formatRate,
} from './statusFormat';

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

  it('groups thousands', () => {
    expect(formatCount(12345)).toBe('12,345');
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

describe('formatDay', () => {
  it('formats unix seconds as a UTC calendar day', () => {
    expect(formatDay(Date.UTC(2023, 2, 22, 12) / 1000)).toBe('22 Mar 2023');
    expect(formatDay(Date.UTC(2026, 0, 1, 0, 0, 30) / 1000)).toBe('1 Jan 2026');
  });

  it('renders missing values as a hyphen', () => {
    expect(formatDay(null)).toBe('-');
    expect(formatDay(undefined)).toBe('-');
  });
});

describe('formatAgo', () => {
  const now = Date.UTC(2026, 9, 10, 12);
  const secondsAgo = (s: number) => now / 1000 - s;

  it('says just now under a minute, including slightly future times', () => {
    expect(formatAgo(secondsAgo(0), now)).toBe('just now');
    expect(formatAgo(secondsAgo(59), now)).toBe('just now');
    expect(formatAgo(secondsAgo(-30), now)).toBe('just now');
  });

  it('counts whole minutes, hours and days', () => {
    expect(formatAgo(secondsAgo(2 * 60 + 30), now)).toBe('2 min ago');
    expect(formatAgo(secondsAgo(59 * 60), now)).toBe('59 min ago');
    expect(formatAgo(secondsAgo(3 * 3600 + 59 * 60), now)).toBe('3 h ago');
    expect(formatAgo(secondsAgo(5 * 86400 + 3600), now)).toBe('5 d ago');
  });

  it('says never when there is no timestamp', () => {
    expect(formatAgo(null, now)).toBe('never');
    expect(formatAgo(undefined, now)).toBe('never');
  });
});
