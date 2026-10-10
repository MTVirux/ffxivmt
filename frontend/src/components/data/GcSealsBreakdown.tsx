import type { GcSealsBreakdown, PlanNode } from '../../lib/gcSeals';
import { formatGilCompact, formatNumber } from '../../lib/format';

type Props = {
  breakdown: GcSealsBreakdown | null;
  names: Record<string, string>;
  worldNames: Map<number, string>;
};

export default function GcSealsBreakdownView({ breakdown, names, worldNames }: Props) {
  if (!breakdown) {
    return (
      <p className="text-sm text-muted-foreground">
        The market board can't cover this item right now.
      </p>
    );
  }

  const nameOf = (id: number) => names[String(id)] || `Item ${id}`;

  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <section>
        <h3 className="mb-2 text-xs uppercase tracking-widest text-muted-foreground">Plan</h3>
        <ul className="space-y-1 text-sm">
          <PlanNodeView node={breakdown.tree} nameOf={nameOf} />
        </ul>
      </section>
      <section>
        <h3 className="mb-2 text-xs uppercase tracking-widest text-muted-foreground">
          Shopping list - {formatGilCompact(breakdown.total)} gil
        </h3>
        <table className="w-full text-sm">
          <tbody>
            {breakdown.purchases.map((p) => (
              <tr key={p.id} className="border-t border-border/30">
                <td className="py-1 pr-3">{nameOf(p.id)}</td>
                <td className="py-1 pr-3 text-right font-mono tabular-nums">
                  ×{formatNumber(p.quantity)}
                </td>
                <td className="py-1 pr-3 text-right font-mono tabular-nums">
                  {formatGilCompact(p.cost)}
                </td>
                <td className="py-1 text-xs text-muted-foreground">
                  {p.byWorld
                    .map((w) => `${worldNames.get(w.worldId) ?? '?'} ${formatNumber(w.quantity)}`)
                    .join(', ')}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </div>
  );
}

function PlanNodeView({ node, nameOf }: { node: PlanNode; nameOf: (id: number) => string }) {
  return (
    <li>
      <span className={node.method === 'craft' ? 'text-accent' : 'text-muted-foreground'}>
        {node.method === 'craft' ? `Craft ×${node.crafts}` : 'Buy'}
      </span>{' '}
      <span className="font-mono tabular-nums">{formatNumber(node.quantity)}</span>{' '}
      {nameOf(node.id)}{' '}
      <span className="font-mono text-xs tabular-nums text-muted-foreground">
        {formatGilCompact(node.cost)}
      </span>
      {node.children.length > 0 && (
        <ul className="ml-4 mt-1 space-y-1 border-l border-border/40 pl-3">
          {node.children.map((child) => (
            <PlanNodeView key={child.id} node={child} nameOf={nameOf} />
          ))}
        </ul>
      )}
    </li>
  );
}
