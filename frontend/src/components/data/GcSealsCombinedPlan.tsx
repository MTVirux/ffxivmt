import { Link } from 'react-router';
import type { CombinedEntry, CombinedPlan } from '../../lib/gcSeals';
import { formatGilCompact, formatNumber } from '../../lib/format';

type Props = {
  plan: CombinedPlan;
  names: Record<string, string>;
  worldNames: Map<number, string>;
};

const CARD_CLASS = 'space-y-4 rounded-xl border border-border/60 bg-card/40 p-4';
const HEADING_CLASS = 'text-xs uppercase tracking-widest text-muted-foreground';

function methodOf(entry: CombinedEntry): string {
  if (entry.bought > 0 && entry.crafted > 0) return 'buy + craft';
  return entry.crafted > 0 ? 'craft' : 'buy';
}

export default function GcSealsCombinedPlan({ plan, names, worldNames }: Props) {
  if (plan.entries.length === 0) {
    return (
      <section className={CARD_CLASS}>
        <h2 className={HEADING_CLASS}>Combined plan</h2>
        <p className="text-sm text-muted-foreground">
          The market board can't price any item right now.
        </p>
      </section>
    );
  }

  const nameOf = (id: number) => names[String(id)] || `Item ${id}`;
  const itemCount = plan.entries.reduce((sum, e) => sum + e.quantity, 0);

  return (
    <section className={CARD_CLASS}>
      <header className="flex flex-wrap items-baseline gap-x-6 gap-y-1 text-sm text-muted-foreground">
        <h2 className={HEADING_CLASS}>Combined plan</h2>
        <span>
          <span className="font-mono tabular-nums text-foreground">
            {formatGilCompact(plan.total)}
          </span>{' '}
          gil
        </span>
        <span>
          <span className="font-mono tabular-nums text-foreground">{formatNumber(plan.seals)}</span>{' '}
          / {formatNumber(plan.target)} seals
        </span>
        <span>
          <span className="font-mono tabular-nums text-foreground">{formatNumber(itemCount)}</span>{' '}
          {itemCount === 1 ? 'item' : 'items'} to turn in
        </span>
        {plan.seals > 0 && (
          <span>
            <span className="font-mono font-medium tabular-nums text-accent">
              {formatNumber(Math.round((plan.total / plan.seals) * 100) / 100)}
            </span>{' '}
            gil/seal
          </span>
        )}
      </header>

      {plan.short && (
        <p className="rounded-md bg-destructive/15 px-3 py-2 text-sm text-destructive">
          The market board can only reach {formatNumber(plan.seals)} of {formatNumber(plan.target)}{' '}
          seals - this is the best partial plan.
        </p>
      )}

      <div className="grid gap-6 lg:grid-cols-2">
        <section>
          <h3 className={`mb-2 ${HEADING_CLASS}`}>Turn in</h3>
          <table className="w-full text-sm">
            <tbody>
              {plan.entries.map((e) => (
                <tr key={e.id} className="border-t border-border/30">
                  <td className="py-1 pr-3">
                    <Link
                      to={`/item/${e.id}`}
                      className="font-medium text-foreground hover:text-accent"
                    >
                      {nameOf(e.id)}
                    </Link>
                  </td>
                  <td className="py-1 pr-3 text-right font-mono tabular-nums">
                    ×{formatNumber(e.quantity)}
                  </td>
                  <td className="py-1 pr-3 text-right font-mono tabular-nums text-muted-foreground">
                    {formatNumber(e.seals)} seals
                  </td>
                  <td className="py-1 pr-3 text-right font-mono tabular-nums">
                    {formatGilCompact(e.cost)}
                  </td>
                  <td className="py-1">
                    <span className="rounded bg-card px-1.5 py-0.5 text-xs text-muted-foreground">
                      {methodOf(e)}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
        <section>
          <h3 className={`mb-2 ${HEADING_CLASS}`}>
            Shopping list - {formatGilCompact(plan.total)} gil
          </h3>
          <table className="w-full text-sm">
            <tbody>
              {plan.purchases.map((p) => (
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
    </section>
  );
}
