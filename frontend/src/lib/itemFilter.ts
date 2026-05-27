export function matchesItemName(name: string, query: string): boolean {
  const q = query.trim().toLowerCase();
  if (q === '') return true;
  return name.toLowerCase().includes(q);
}
