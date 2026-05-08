# ffmt-frontend

React + Vite + TypeScript SPA for [FFXIV Market Tools](https://mtvirux.app). Talks to the
.NET 9 backend (`backend_dotnet/Ffmt.Api`) over `/api/v1/*`.

## Dev workflow

Day-to-day, run Vite on the host — HMR through Docker bind mounts on Windows is
slow (NTFS↔ext4 inotify forwarding):

```sh
pnpm install              # required once; generates pnpm-lock.yaml
pnpm dev                  # http://localhost:5173, proxies /api → :8080
```

The `vite.config.ts` proxy assumes the .NET backend is reachable on
`http://localhost:8080`. Run it locally with:

```sh
cd ../backend_dotnet
dotnet run --project Ffmt.Api
```

Or bring up the full compose stack and use the Caddy proxy on `https://${ZERO_SSL_MAIN_DOMAIN}` instead.

## Compose-integrated dev (rare)

Use the dev override when you specifically want the frontend running inside
Docker (e.g. to reproduce a container-only issue):

```sh
docker compose --env-file env -f docker-compose.yml -f docker-compose.dev.yml up -d ffmt_frontend
```

The bind mount + anonymous `node_modules` volume keeps host installs from
shadowing container deps.

## Build

```sh
pnpm build                # tsc -b && vite build → dist/
pnpm preview              # local static preview of dist/
```

The production Docker image (`docker/dockerfiles/Dockerfile_frontend`) runs
`pnpm install --frozen-lockfile && pnpm build` and serves `dist/` via
`nginx:alpine`.

## API types

OpenAPI-driven. After Phase 2 lands Swashbuckle on the .NET side:

```sh
pnpm openapi:gen          # writes src/api/generated/schema.ts
```

The generated file is committed; regenerate when the backend contract changes.

## Layout

```
src/
├── api/         apiFetch client + generated schema types
├── components/  ui/, layout/, data/, form/
├── hooks/       useWorlds, useItem, useGilfluxRanking, ...
├── lib/         format helpers, time helpers, iconUrl builder
├── routes/      one file per page
└── styles/      Tailwind v4 globals + @theme tokens
```

## Phase status

- ✅ **Phase 1:** skeleton, container, routing, placeholder pages
- ✅ **Phase 2:** `apiFetch`/`apiGet`, `useWorlds`/`useItem`, XIVAPI icon helper,
  real `HomePage`. Backend exposes `/openapi/v1.json`; Item endpoint includes `icon_image`.
- ✅ **Phase 3a:** real `ItemPage` — info card, world picker, Recharts `PriceChart`,
  recent-sales table. Backend gains `GET /api/v1/item/:id/sales?world_id=&limit=`.
- ✅ **Phase 3b:** real `GilfluxPage` — `TieredLocationSelect` (region/DC/world cascading),
  crafted-only toggle, `RankingTable` with sortable timeframes and per-world subrows.
- ✅ **Phase 3c:** calculator pages — RHF + Zod, sortable tables, default sort by `ffmt_score`.
  Backend gained `/api/v1/tools/currency_efficiency_calculator` (Garland tradeCurrency walk
  + Universalis stackSizeHistogram).
- ✅ **Phase 4:** Razor Pages dropped from `Ffmt.Api`. `Pages/` and `wwwroot/` deleted;
  `Program.cs` no longer registers `AddRazorPages`/`MapRazorPages`/`UseStaticFiles`;
  `UseStatusCodePagesWithReExecute("/error/{0}")` collapsed to plain `UseStatusCodePages()`.

### Regenerating API types

Once a backend is reachable on `http://localhost:8080`:

```sh
pnpm openapi:gen
```

This writes `src/api/generated/schema.ts`. Until then, `src/api/types.ts` is the
hand-rolled boundary that `apiFetch` and the data hooks import from. Switching
to the generated file is a single-line change in `client.ts` per type.
