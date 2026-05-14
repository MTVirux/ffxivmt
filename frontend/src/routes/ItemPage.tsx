import { useId, useMemo, type ReactNode } from 'react';
import { Link, useParams } from 'react-router-dom';
import ItemIcon from '../components/data/ItemIcon';
import PriceChart from '../components/data/PriceChart';
import LocationSelect from '../components/form/LocationSelect';
import { useItem } from '../hooks/useItem';
import { useItemSales } from '../hooks/useItemSales';
import { useUserPrefs } from '../hooks/useUserPrefs';
import { formatGilExact, formatNumber } from '../lib/format';
import { relativeTime } from '../lib/time';
import type { Sale } from '../api/types';

export default function ItemPage() {
  const { id: idParam } = useParams<{ id: string }>();
  const itemId = idParam ? Number(idParam) : undefined;
  const validId = itemId !== undefined && Number.isFinite(itemId) && itemId > 0;

  const item = useItem(validId ? itemId : undefined);
  const [prefs, patchPrefs] = useUserPrefs();
  const worldId = prefs.lastWorldId;
  const setWorldId = (id: number) => patchPrefs({ lastWorldId: id });
  const sales = useItemSales(validId ? itemId : undefined, worldId, 100);

  const summary = useMemo(() => summarize(sales.data ?? []), [sales.data]);

  if (!validId) {
    return (
      <div className="rounded-xl border border-destructive/50 bg-card p-6 text-sm text-destructive">
        Invalid item id: <code className="font-mono">{idParam}</code>
      </div>
    );
  }

  if (item.isLoading) {
    return <ItemHeaderSkeleton />;
  }

  if (item.isError || !item.data) {
    return (
      <div className="space-y-3">
        <div className="rounded-xl border border-destructive/50 bg-card p-6 text-sm text-destructive">
          Item <code className="font-mono">{itemId}</code> not found.
        </div>
        <Link to="/" className="text-sm text-accent hover:underline">
          Back to home
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-10">
      <ItemHeader
        name={item.data.name}
        marketable={item.data.marketable}
        craftable={item.data.craftable}
        itemId={item.data.id}
      />

      <section className="space-y-4">
        <header className="flex flex-wrap items-end justify-between gap-3">
          <div>
            <h2 className="text-sm font-medium uppercase tracking-widest text-muted-foreground">
              Recent prices
            </h2>
            <p className="mt-1 text-xs text-muted-foreground">
              {summary.count > 0
                ? `${formatNumber(summary.count)} sales · last ${relativeTime(summary.lastTs!)}`
                : 'No sales in the last 8 days.'}
            </p>
          </div>
          <WorldPickerInline worldId={worldId} onChange={setWorldId} />
        </header>

        {worldId === undefined ? (
          <EmptyHint>Pick a world to load sales.</EmptyHint>
        ) : sales.isLoading ? (
          <div className="h-[220px] animate-pulse rounded-lg bg-card/40" />
        ) : sales.isError ? (
          <div className="rounded-lg border border-destructive/50 bg-card p-4 text-sm text-destructive">
            Failed to load sales.
          </div>
        ) : (
          <>
            <PriceChart sales={sales.data ?? []} height={240} />
            <RecentSalesTable sales={sales.data ?? []} />
          </>
        )}
      </section>
    </div>
  );
}

function ItemHeader({
  name,
  marketable,
  craftable,
  itemId,
}: {
  name: string;
  marketable: boolean;
  craftable: boolean;
  itemId: number;
}) {
  return (
    <header className="flex flex-wrap items-start gap-6">
      <ItemIcon itemId={itemId} alt={name} size={80} />
      <div className="space-y-2">
        <p className="font-mono text-xs uppercase tracking-widest text-muted-foreground">
          item · {itemId}
        </p>
        <h1 className="text-3xl font-semibold tracking-tight">{name}</h1>
        <div className="flex flex-wrap gap-2 text-xs">
          {marketable ? <Tag>Marketable</Tag> : <Tag muted>Not marketable</Tag>}
          {craftable && <Tag accent>Craftable</Tag>}
        </div>
      </div>
    </header>
  );
}

function Tag({
  children,
  accent,
  muted,
}: {
  children: ReactNode;
  accent?: boolean;
  muted?: boolean;
}) {
  const tone = accent
    ? 'bg-accent/10 text-accent border-accent/30'
    : muted
      ? 'bg-card text-muted-foreground border-border/60'
      : 'bg-card text-foreground border-border/60';
  return (
    <span className={`rounded-full border px-2 py-0.5 font-medium ${tone}`}>{children}</span>
  );
}

function WorldPickerInline({
  worldId,
  onChange,
}: {
  worldId: number | undefined;
  onChange: (n: number) => void;
}) {
  const id = useId();
  return (
    <div className="flex items-center gap-2">
      <label htmlFor={id} className="text-xs uppercase tracking-widest text-muted-foreground">
        World
      </label>
      <LocationSelect id={id} worldId={worldId} onChange={onChange} />
    </div>
  );
}

function RecentSalesTable({ sales }: { sales: Sale[] }) {
  if (sales.length === 0) return null;
  // Newest first for the table; chart already sorts ascending for the line.
  const rows = [...sales]
    .sort((a, b) => Date.parse(b.sale_time) - Date.parse(a.sale_time))
    .slice(0, 25);
  return (
    <div className="overflow-hidden rounded-xl border border-border/60">
      <table className="w-full text-sm">
        <thead className="bg-card/60 text-xs uppercase tracking-widest text-muted-foreground">
          <tr>
            <Th>When</Th>
            <Th>Q</Th>
            <Th align="right">Unit</Th>
            <Th align="right">Total</Th>
            <Th>HQ</Th>
            <Th>Buyer</Th>
          </tr>
        </thead>
        <tbody>
          {rows.map((s, i) => (
            <tr
              key={`${s.sale_time}-${s.buyer_name}-${i}`}
              className="border-t border-border/40 even:bg-card/20"
            >
              <Td>
                <span title={s.sale_time}>{relativeTime(s.sale_time)}</span>
              </Td>
              <Td>×{s.quantity}</Td>
              <Td align="right" mono>
                {formatGilExact(s.unit_price)}
              </Td>
              <Td align="right" mono>
                {formatGilExact(s.total)}
              </Td>
              <Td>
                {s.hq ? (
                  <span className="text-accent" aria-label="High quality">
                    ◆
                  </span>
                ) : (
                  <span className="text-muted-foreground">—</span>
                )}
              </Td>
              <Td>
                <span className="text-muted-foreground">{s.buyer_name}</span>
              </Td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function Th({ children, align }: { children: ReactNode; align?: 'right' }) {
  return (
    <th
      scope="col"
      className={`px-3 py-2 font-medium ${align === 'right' ? 'text-right' : 'text-left'}`}
    >
      {children}
    </th>
  );
}

function Td({
  children,
  align,
  mono,
}: {
  children: ReactNode;
  align?: 'right';
  mono?: boolean;
}) {
  return (
    <td
      className={[
        'px-3 py-2',
        align === 'right' ? 'text-right' : 'text-left',
        mono ? 'font-mono tabular-nums' : '',
      ]
        .filter(Boolean)
        .join(' ')}
    >
      {children}
    </td>
  );
}

function EmptyHint({ children }: { children: ReactNode }) {
  return (
    <div className="rounded-lg border border-dashed border-border/60 bg-card/40 px-4 py-8 text-center text-sm text-muted-foreground">
      {children}
    </div>
  );
}

function ItemHeaderSkeleton() {
  return (
    <div className="flex animate-pulse items-start gap-6">
      <div className="h-20 w-20 rounded-md bg-card/60" />
      <div className="space-y-3">
        <div className="h-3 w-24 rounded bg-card/60" />
        <div className="h-8 w-72 rounded bg-card/60" />
        <div className="h-5 w-48 rounded bg-card/60" />
      </div>
    </div>
  );
}

function summarize(sales: Sale[]): { count: number; lastTs: number | null } {
  if (sales.length === 0) return { count: 0, lastTs: null };
  let last = -Infinity;
  for (const s of sales) {
    const ts = Date.parse(s.sale_time);
    if (Number.isFinite(ts) && ts > last) last = ts;
  }
  return { count: sales.length, lastTs: Number.isFinite(last) ? last : null };
}
