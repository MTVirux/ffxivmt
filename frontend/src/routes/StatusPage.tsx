import type { StatusMetric, StatusMetricKey } from '../api/types';
import BackfillProgress from '../components/status/BackfillProgress';
import MetricCard from '../components/status/MetricCard';
import StatusBanner from '../components/status/StatusBanner';
import QueryBoundary from '../components/layout/QueryBoundary';
import { useStatusMetrics } from '../hooks/useStatusMetrics';
import { formatConnected, formatCount, formatPercent, formatRate } from '../lib/statusFormat';

type Card = {
  key: StatusMetricKey;
  label: string;
  format: (m: StatusMetric) => string;
  formatPoint: (v: number | null) => string;
};

const CARDS: Card[] = [
  {
    key: 'requests_per_second',
    label: 'API requests',
    format: (m) => formatRate(m.value),
    formatPoint: formatRate,
  },
  {
    key: 'error_rate',
    label: 'Server error rate',
    format: (m) => formatPercent(m.value),
    formatPoint: formatPercent,
  },
  {
    key: 'sales_per_second',
    label: 'Sales received',
    format: (m) => formatRate(m.value),
    formatPoint: formatRate,
  },
  {
    key: 'worlds_connected',
    label: 'Worlds connected',
    format: (m) => formatConnected(m.value, m.total),
    formatPoint: formatCount,
  },
  {
    key: 'backfill_rows_per_second',
    label: 'Backfill rows imported',
    format: (m) => formatRate(m.value),
    formatPoint: formatRate,
  },
];

export default function StatusPage() {
  const query = useStatusMetrics();

  return (
    <div className="space-y-8">
      <header>
        <h1 className="text-3xl font-semibold tracking-tight">Status</h1>
        <p className="mt-2 text-sm text-muted-foreground">
          Live API traffic, errors and market data ingestion over the last 24 hours. Refreshes every
          minute.
        </p>
      </header>
      <QueryBoundary query={query} errorText="Status API unreachable - the site may be down.">
        {(data) => (
          <div className="space-y-6">
            <StatusBanner state={data.state} reasons={data.reasons} />
            {data.available && (
              <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                {CARDS.map((card) => {
                  const metric = data.metrics[card.key];
                  return metric ? (
                    <MetricCard
                      key={card.key}
                      label={card.label}
                      value={card.format(metric)}
                      series={metric.series}
                      formatPoint={card.formatPoint}
                    />
                  ) : null;
                })}
              </div>
            )}
          </div>
        )}
      </QueryBoundary>
      <BackfillProgress />
    </div>
  );
}
