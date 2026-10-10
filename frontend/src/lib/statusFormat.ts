const compact = new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 });

function isMissing(v: number | null | undefined): v is null | undefined {
  return v == null || !Number.isFinite(v);
}

export function formatRate(v: number | null | undefined): string {
  return isMissing(v) ? '-' : `${compact.format(v)}/s`;
}

export function formatPercent(v: number | null | undefined): string {
  return isMissing(v) ? '-' : `${(v * 100).toFixed(1)}%`;
}

export function formatCount(v: number | null | undefined): string {
  return isMissing(v) ? '-' : String(Math.round(v));
}

export function formatConnected(
  value: number | null | undefined,
  total: number | null | undefined,
): string {
  if (isMissing(value)) return '-';
  return isMissing(total) ? formatCount(value) : `${formatCount(value)} / ${formatCount(total)}`;
}
