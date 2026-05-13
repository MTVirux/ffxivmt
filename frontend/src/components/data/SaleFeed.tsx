import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { useUniversalisStream } from '../../hooks/useUniversalisStream';
import type { EnrichedSale, StreamStatus } from '../../hooks/useUniversalisStream';
import { formatGilExact } from '../../lib/format';
import { relativeTime } from '../../lib/time';

const SKELETON_COUNT = 5;
const COL_WIDTHS = '2fr 0.9fr 1fr 1fr 0.55fr';
const ROW_HEIGHT_PX = 41;

export default function SaleFeed() {
  const { sales, status } = useUniversalisStream();
  const [, setTick] = useState(0);
  const [displayCount, setDisplayCount] = useState(8);
  const listRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const id = setInterval(() => setTick((n) => n + 1), 10_000);
    return () => clearInterval(id);
  }, []);

  useEffect(() => {
    const el = listRef.current;
    if (!el) return;
    const obs = new ResizeObserver(([entry]) => {
      setDisplayCount(Math.max(1, Math.floor(entry.contentRect.height / ROW_HEIGHT_PX)));
    });
    obs.observe(el);
    return () => obs.disconnect();
  }, []);

  return (
    <div className="flex h-full flex-col">
      <div className="mb-3 flex items-center gap-2 font-mono text-xs uppercase tracking-widest text-muted-foreground">
        <StatusDot status={status} />
        Live Sales
      </div>

      <div ref={listRef} className="flex-1 overflow-hidden rounded-xl border border-border/60">
        {status === 'connecting' && sales.length === 0 ? (
          <div className="divide-y divide-border/40">
            {Array.from({ length: SKELETON_COUNT }).map((_, i) => (
              <SkeletonRow key={i} />
            ))}
          </div>
        ) : sales.length === 0 ? (
          <div className="flex h-full items-center justify-center font-mono text-xs text-muted-foreground/40">
            no recent activity
          </div>
        ) : (
          <div className="divide-y divide-border/40">
            {(() => {
              const now = Date.now() / 1000;
              return sales.slice(0, displayCount).map((sale, i) => (
                <SaleRow key={sale.key} sale={sale} isNewest={i === 0 && now - sale.saleTime < 30} />
              ));
            })()}
          </div>
        )}
      </div>
    </div>
  );
}

function StatusDot({ status }: { status: StreamStatus }) {
  return (
    <span
      className={`inline-block h-1.5 w-1.5 rounded-full ${
        status === 'connected' ? 'animate-pulse bg-green-500' : 'bg-muted-foreground/40'
      }`}
    />
  );
}

function SaleRow({ sale, isNewest }: { sale: EnrichedSale; isNewest: boolean }) {
  const nameIsId = sale.itemName === String(sale.itemId);
  return (
    <div
      className={`grid items-center gap-4 px-4 py-2.5 text-sm ${
        isNewest ? 'bg-accent/5' : 'hover:bg-card/40'
      }`}
      style={{ gridTemplateColumns: COL_WIDTHS }}
    >
      <div className="flex min-w-0 items-center gap-1.5">
        <Link
          to={`/item/${sale.itemId}`}
          className="truncate font-medium text-foreground hover:text-accent"
        >
          {nameIsId ? sale.itemId : sale.itemName}
        </Link>
        {sale.hq && (
          <span className="shrink-0 rounded border border-accent/30 bg-accent/10 px-1 py-px font-mono text-[10px] font-semibold text-accent">
            HQ
          </span>
        )}
      </div>
      <div className="truncate text-muted-foreground">{sale.worldName}</div>
      <div className="truncate text-muted-foreground">{sale.buyerName}</div>
      <div className="truncate text-right font-mono tabular-nums">
        {formatGilExact(sale.unitPrice)}
        {sale.quantity > 1 && (
          <span className="ml-1 text-xs text-muted-foreground/60">×{sale.quantity}</span>
        )}
      </div>
      <div className="text-right font-mono text-xs text-muted-foreground/40">
        {relativeTime(sale.saleTime * 1_000)}
      </div>
    </div>
  );
}

function SkeletonRow() {
  return (
    <div
      className="grid items-center gap-4 px-4 py-2.5"
      style={{ gridTemplateColumns: COL_WIDTHS }}
    >
      <div className="h-3 w-2/3 animate-pulse rounded bg-muted/30" />
      <div className="h-3 w-3/4 animate-pulse rounded bg-muted/30" />
      <div className="h-3 w-1/2 animate-pulse rounded bg-muted/30" />
      <div className="h-3 w-4/5 animate-pulse rounded bg-muted/30 ml-auto" />
      <div className="h-3 w-full animate-pulse rounded bg-muted/30" />
    </div>
  );
}
