import type { WorldStructure } from '../api/types';

export function buildWorldNameMap(tree: WorldStructure | undefined): Map<number, string> {
  const map = new Map<number, string>();
  if (!tree) return map;
  for (const dcs of Object.values(tree)) {
    for (const worlds of Object.values(dcs)) {
      for (const [id, name] of Object.entries(worlds)) {
        map.set(Number(id), name);
      }
    }
  }
  return map;
}
