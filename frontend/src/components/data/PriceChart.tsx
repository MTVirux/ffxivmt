import { useMemo } from 'react';
import {
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import type { Sale } from '../../api/types';
import { formatGil, formatGilExact } from '../../lib/format';
import { shortDateTime } from '../../lib/time';

type Props = {
  sales: Sale[];
  height?: number;
};

type Point = {
  t: number;
  unitPrice: number;
  hq: boolean;
  buyer: string;
  qty: number;
};

export default function PriceChart({ sales, height = 220 }: Props) {
  const data = useMemo<Point[]>(
    () =>
      sales
        .map<Point>((s) => ({
          t: Date.parse(s.sale_time),
          unitPrice: s.unit_price,
          hq: s.hq,
          buyer: s.buyer_name,
          qty: s.quantity,
        }))
        .filter((p) => Number.isFinite(p.t))
        .sort((a, b) => a.t - b.t),
    [sales],
  );

  if (data.length === 0) {
    return (
      <div
        className="flex items-center justify-center rounded-lg border border-dashed border-border/60 bg-card/40 text-sm text-muted-foreground"
        style={{ height }}
      >
        No recent sales
      </div>
    );
  }

  return (
    <div style={{ height }}>
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={data} margin={{ top: 12, right: 16, bottom: 0, left: 0 }}>
          <CartesianGrid stroke="var(--color-border)" strokeOpacity={0.4} vertical={false} />
          <XAxis
            dataKey="t"
            type="number"
            domain={['dataMin', 'dataMax']}
            scale="time"
            tickFormatter={(t) => shortDateTime(t)}
            stroke="var(--color-muted-foreground)"
            fontSize={11}
            minTickGap={32}
          />
          <YAxis
            dataKey="unitPrice"
            stroke="var(--color-muted-foreground)"
            fontSize={11}
            tickFormatter={(v: number) => formatGil(v)}
            width={72}
          />
          <Tooltip
            contentStyle={{
              background: 'var(--color-card)',
              border: '1px solid var(--color-border)',
              borderRadius: 8,
              fontSize: 12,
            }}
            labelFormatter={(t: number) => shortDateTime(t)}
            formatter={(value: number, _name, ctx) => {
              const p = ctx.payload as Point;
              return [
                formatGilExact(value),
                `${p.hq ? 'HQ' : 'NQ'} · ×${p.qty} · ${p.buyer}`,
              ];
            }}
          />
          <Line
            type="monotone"
            dataKey="unitPrice"
            stroke="var(--color-accent)"
            strokeWidth={2}
            dot={{ r: 2.5, fill: 'var(--color-accent)' }}
            activeDot={{ r: 4 }}
            isAnimationActive={false}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}
