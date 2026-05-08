const compact = new Intl.NumberFormat('en-US', {
  notation: 'compact',
  maximumFractionDigits: 1,
});

const exact = new Intl.NumberFormat('en-US');

export function formatGil(n: number): string {
  if (!Number.isFinite(n)) return '—';
  return `${compact.format(n)} gil`;
}

export function formatGilExact(n: number): string {
  if (!Number.isFinite(n)) return '—';
  return `${exact.format(n)} gil`;
}

export function formatNumber(n: number): string {
  if (!Number.isFinite(n)) return '—';
  return exact.format(n);
}
