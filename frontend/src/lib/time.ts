const rtf = new Intl.RelativeTimeFormat('en-US', { numeric: 'auto' });

const MIN = 60_000;
const HOUR = 60 * MIN;
const DAY = 24 * HOUR;

export function relativeTime(input: string | number | Date): string {
  const ts = typeof input === 'number' ? input : new Date(input).getTime();
  if (!Number.isFinite(ts)) return '—';
  const diff = ts - Date.now();
  const abs = Math.abs(diff);
  if (abs < MIN) return rtf.format(Math.round(diff / 1000), 'second');
  if (abs < HOUR) return rtf.format(Math.round(diff / MIN), 'minute');
  if (abs < DAY) return rtf.format(Math.round(diff / HOUR), 'hour');
  return rtf.format(Math.round(diff / DAY), 'day');
}

const dtf = new Intl.DateTimeFormat('en-US', {
  month: 'short',
  day: 'numeric',
  hour: 'numeric',
  minute: '2-digit',
});

export function shortDateTime(input: string | number | Date): string {
  const ts = typeof input === 'number' ? input : new Date(input).getTime();
  if (!Number.isFinite(ts)) return '—';
  return dtf.format(ts);
}
