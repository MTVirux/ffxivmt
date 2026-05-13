import { Link } from 'react-router-dom';
import { navItems } from '../config/navigation';
import { useWorlds } from '../hooks/useWorlds';
import SaleFeed from '../components/data/SaleFeed';

export default function HomePage() {
  const worlds = useWorlds();
  const worldCount = worlds.data
    ? Object.values(worlds.data).reduce(
        (a, dcs) => a + Object.values(dcs).reduce((b, ws) => b + Object.keys(ws).length, 0),
        0,
      )
    : null;
  const regionCount = worlds.data ? Object.keys(worlds.data).length : null;

  return (
    <div className="space-y-16">
      <section className="relative">
        <div
          aria-hidden="true"
          className="absolute -left-24 -top-24 -z-10 h-64 w-[36rem] rounded-full opacity-30 blur-3xl"
          style={{
            background:
              'radial-gradient(closest-side, var(--color-accent), transparent)',
          }}
        />
        <p className="font-mono text-xs uppercase tracking-[0.2em] text-accent">
          ffxiv market tools
        </p>
        <h1 className="mt-3 max-w-3xl text-5xl font-semibold leading-tight tracking-tight sm:text-6xl">
          Watch the gil flow.
        </h1>
        <p className="mt-5 max-w-2xl text-lg text-muted-foreground">
          Live Final Fantasy XIV market data.<br />
          Gilflux rankings, crafting profit math, and currency efficiency.<br />
          Powered by Universalis and You ❤︎
        </p>

        <div className="mt-8 flex flex-wrap items-center gap-x-8 gap-y-3 font-mono text-xs uppercase tracking-widest text-muted-foreground">
          <Stat label="Regions" value={regionCount} />
          <Stat label="Worlds" value={worldCount} />
          <Stat label="Latency" value="real-time" />
        </div>
      </section>

      <section>
        <h2 className="text-sm font-medium uppercase tracking-widest text-muted-foreground">
          Tools
        </h2>
        <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {navItems.map((item) => (
            <Link
              key={item.to}
              to={item.to}
              className="group relative overflow-hidden rounded-xl border border-border/60 bg-card p-6 transition-all hover:-translate-y-0.5 hover:border-accent/60 hover:shadow-lg hover:shadow-accent/5"
            >
              <h3 className="text-lg font-medium tracking-tight group-hover:text-accent">
                {item.name}
              </h3>
              <p className="mt-2 text-sm text-muted-foreground">{item.description}</p>
              <span className="mt-6 inline-flex items-center gap-1 text-xs font-medium text-accent opacity-0 transition-opacity group-hover:opacity-100">
                Open →
              </span>
            </Link>
          ))}
        </div>
      </section>

      <section className="h-96">
        <SaleFeed />
      </section>
    </div>
  );
}

function Stat({ label, value }: { label: string; value: number | string | null }) {
  return (
    <div className="flex items-baseline gap-2">
      <span className="text-foreground tabular-nums">
        {value === null ? '—' : value}
      </span>
      <span>{label}</span>
    </div>
  );
}
