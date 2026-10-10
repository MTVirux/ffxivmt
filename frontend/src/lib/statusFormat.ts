const compact = new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 });
const whole = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const day = new Intl.DateTimeFormat('en-GB', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  timeZone: 'UTC',
});

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
  return isMissing(v) ? '-' : whole.format(v);
}

export function formatConnected(
  value: number | null | undefined,
  total: number | null | undefined,
): string {
  if (isMissing(value)) return '-';
  return isMissing(total) ? formatCount(value) : `${formatCount(value)} / ${formatCount(total)}`;
}

export function formatDay(unixSeconds: number | null | undefined): string {
  return isMissing(unixSeconds) ? '-' : day.format(unixSeconds * 1000);
}

export function formatAgo(unixSeconds: number | null | undefined, nowMs = Date.now()): string {
  if (isMissing(unixSeconds)) return 'never';
  const minutes = Math.floor((nowMs / 1000 - unixSeconds) / 60);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes} min ago`;
  if (minutes < 24 * 60) return `${Math.floor(minutes / 60)} h ago`;
  return `${Math.floor(minutes / (24 * 60))} d ago`;
}
