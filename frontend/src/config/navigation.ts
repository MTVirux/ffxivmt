export type NavItem = {
  name: string;
  /** Short label for the top nav (≤ ~16 chars). */
  navLabel: string;
  to: string;
  description: string;
};

export const navItems: NavItem[] = [
  {
    name: 'Gilflux rankings',
    navLabel: 'Gilflux',
    to: '/gilflux',
    description: 'The items moving the most gil right now — by World, DC, or Region.',
  },
  {
    name: 'Currency efficiency',
    navLabel: 'Currency',
    to: '/tools/currency-efficiency-calculator',
    description: 'The most profitable way to spend your tokens, scrips, and seals.',
  },
  {
    name: 'Item product profit',
    navLabel: 'Profit solver',
    to: '/tools/item-product-profit-calculator',
    description: 'The most profitable craft for any material on the market board.',
  },
  {
    name: 'Buyer search',
    navLabel: 'Buyer',
    to: '/tools/buyer-search',
    description: "Look up a character's market-board purchase history.",
  },
];
