export type Currency = { id: number; name: string };
export type CurrencyGroup = { category: string; currencies: Currency[] };

// Names are the exact in-game strings the API resolves against; ids are carried
// only as stable keys. Every entry was checked against the calculator - anything
// that returns no marketable rows (e.g. Allagan Tomestone of Mnemonics, whose
// current-tier gear is untradable) is deliberately absent.
export const CURRENCY_GROUPS: CurrencyGroup[] = [
  {
    category: 'Tomestones',
    currencies: [
      { id: 28, name: 'Allagan Tomestone of Poetics' },
      { id: 48, name: 'Allagan Tomestone of Mathematics' },
    ],
  },
  {
    category: 'Scrips',
    currencies: [
      { id: 33913, name: "Purple Crafters' Scrip" },
      { id: 33914, name: "Purple Gatherers' Scrip" },
      { id: 41784, name: "Orange Crafters' Scrip" },
      { id: 41785, name: "Orange Gatherers' Scrip" },
    ],
  },
  {
    category: 'Grand Company',
    currencies: [
      { id: 20, name: 'Storm Seal' },
      { id: 21, name: 'Serpent Seal' },
      { id: 22, name: 'Flame Seal' },
    ],
  },
  {
    category: 'PvP',
    currencies: [
      { id: 25, name: 'Wolf Mark' },
      { id: 36656, name: 'Trophy Crystal' },
    ],
  },
  {
    category: 'Hunts',
    currencies: [
      { id: 27, name: 'Allied Seal' },
      { id: 10307, name: 'Centurio Seal' },
      { id: 26533, name: 'Sack of Nuts' },
    ],
  },
  {
    category: 'Gold Saucer',
    currencies: [
      { id: 29, name: 'MGP' },
      { id: 41668, name: 'Felicitous Token' },
    ],
  },
  {
    category: 'FATEs',
    currencies: [
      { id: 26807, name: 'Bicolor Gemstone' },
      { id: 41805, name: 'Twilight Gemstone' },
    ],
  },
  {
    category: 'Variant & Criterion',
    currencies: [
      { id: 38533, name: "Sil'dihn Potsherd" },
      { id: 38534, name: "Sil'dihn Silver" },
      { id: 38535, name: "Sil'dihn Manuscript" },
      { id: 39884, name: 'Rokkon Potsherd' },
      { id: 39885, name: 'Shishu Coin' },
      { id: 39886, name: 'Rokkon Manuscript' },
      { id: 41078, name: 'Aloalo Potsherd' },
      { id: 41079, name: 'Aloalo Coin' },
      { id: 41080, name: 'Aloalo Manuscript' },
      { id: 50434, name: 'Corvosi Potsherd' },
      { id: 50847, name: 'Corvosi Brass' },
      { id: 49125, name: 'Corvosi Manuscript' },
    ],
  },
  {
    category: 'Island Sanctuary',
    currencies: [
      { id: 37550, name: "Islander's Cowrie" },
      { id: 37549, name: "Seafarer's Cowrie" },
    ],
  },
  {
    category: 'Cosmic Exploration',
    currencies: [
      { id: 47594, name: 'Phaenna Exploration Token' },
      { id: 49802, name: 'Oizys Exploration Token' },
    ],
  },
  {
    category: 'Occult Crescent',
    currencies: [
      { id: 45043, name: 'Enlightenment Silver Piece' },
      { id: 45044, name: 'Enlightenment Gold Piece' },
      { id: 51975, name: 'Enlightenment Silver Obol' },
      { id: 51976, name: 'Enlightenment Gold Obol' },
    ],
  },
  {
    category: 'Ishgardian Restoration',
    currencies: [{ id: 28063, name: "Skybuilders' Scrip" }],
  },
  {
    category: 'Faux Hollows',
    currencies: [{ id: 30341, name: 'Faux Leaf' }],
  },
];

export function isKnownCurrency(name: string): boolean {
  return CURRENCY_GROUPS.some((g) => g.currencies.some((c) => c.name === name));
}

// Matches on the category too, so "pvp" surfaces Wolf Mark and Trophy Crystal.
export function filterCurrencyGroups(
  groups: CurrencyGroup[],
  query: string,
): CurrencyGroup[] {
  const q = query.trim().toLowerCase();
  if (q === '') return groups;

  const out: CurrencyGroup[] = [];
  for (const group of groups) {
    if (group.category.toLowerCase().includes(q)) {
      out.push(group);
      continue;
    }
    const currencies = group.currencies.filter((c) =>
      c.name.toLowerCase().includes(q),
    );
    if (currencies.length > 0) out.push({ ...group, currencies });
  }
  return out;
}
