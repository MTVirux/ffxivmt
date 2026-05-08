// XIVAPI's icon CDN packs files into 1000-numbered subdirectories. Item.icon_image
// is the raw `Icon` column from the FFXIV datamining CSV (an int).
//
//   29426 → https://xivapi.com/i/29000/029426.png
//      26 → https://xivapi.com/i/0/000026.png
//
// If XIVAPI ever flakes, swap this for a self-hosted mirror without touching
// callers — the function is the only place that knows the URL scheme.

const XIVAPI_BASE = 'https://xivapi.com/i';

export function xivApiIconUrl(icon: number): string | null {
  if (!Number.isFinite(icon) || icon <= 0) return null;
  const folder = Math.floor(icon / 1000) * 1000;
  return `${XIVAPI_BASE}/${folder}/${String(icon).padStart(6, '0')}.png`;
}
