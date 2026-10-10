import type { StatusState } from '../../api/types';

const VARIANTS: Record<StatusState, { title: string; className: string }> = {
  operational: {
    title: 'All systems operational',
    className: 'border-emerald-500/50 text-emerald-400',
  },
  degraded: { title: 'Degraded performance', className: 'border-amber-500/50 text-amber-400' },
  down: { title: 'Ingestion down', className: 'border-destructive/50 text-destructive' },
  unknown: { title: 'Metrics unavailable', className: 'border-border text-muted-foreground' },
};

type Props = { state: StatusState; reasons: string[] };

export default function StatusBanner({ state, reasons }: Props) {
  const variant = VARIANTS[state];
  return (
    <div className={`rounded-xl border bg-card p-5 ${variant.className}`}>
      <p className="text-lg font-semibold">{variant.title}</p>
      {reasons.length > 0 && (
        <ul className="mt-2 space-y-1 text-sm text-muted-foreground">
          {reasons.map((r) => (
            <li key={r}>{r}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
