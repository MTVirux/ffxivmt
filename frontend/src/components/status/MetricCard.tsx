import { useMemo } from 'react';
import { Area, AreaChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import type { SeriesPoint } from '../../api/types';
import { shortDateTime } from '../../lib/time';

type Props = {
  label: string;
  value: string;
  series: SeriesPoint[];
  formatPoint: (v: number | null) => string;
};

export default function MetricCard({ label, value, series, formatPoint }: Props) {
  const data = useMemo(() => series.map((p) => ({ t: p.t * 1000, v: p.v })), [series]);

  return (
    <div className="rounded-xl border border-border/60 bg-card p-4">
      <p className="text-sm text-muted-foreground">{label}</p>
      <p className="mt-1 font-mono text-2xl font-semibold">{value}</p>
      <div className="mt-3 h-12">
        {data.length > 0 && (
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={data} margin={{ top: 2, right: 0, bottom: 0, left: 0 }}>
              <XAxis dataKey="t" type="number" domain={['dataMin', 'dataMax']} hide />
              <YAxis domain={[0, 'auto']} hide />
              <Tooltip
                contentStyle={{
                  background: 'var(--color-card)',
                  border: '1px solid var(--color-border)',
                  fontSize: 12,
                }}
                labelFormatter={(t) => shortDateTime(Number(t))}
                formatter={(v) => [formatPoint(typeof v === 'number' ? v : null), label]}
              />
              <Area
                type="monotone"
                dataKey="v"
                stroke="var(--color-primary)"
                fill="var(--color-primary)"
                fillOpacity={0.15}
                strokeWidth={1.5}
                isAnimationActive={false}
                connectNulls
              />
            </AreaChart>
          </ResponsiveContainer>
        )}
      </div>
    </div>
  );
}
