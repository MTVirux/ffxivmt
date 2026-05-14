const UNIVERSALIS_ASSETS_BASE = 'https://universalis-ffxiv.github.io/universalis-assets/icon2x';

export function itemIconUrl(itemId: number): string | null {
  if (!Number.isFinite(itemId) || itemId <= 0) return null;
  return `${UNIVERSALIS_ASSETS_BASE}/${itemId}.png`;
}
