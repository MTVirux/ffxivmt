# ffmt-frontend

React + Vite + TypeScript SPA for [FFXIV Market Tools](https://mtvirux.app). Talks to the
.NET 9 backend (`backend/Ffmt.Api`) over `/api/v1/*`.

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
cd ../backend
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

OpenAPI-driven. With the backend live on `:8080`:

```sh
pnpm openapi:gen          # writes src/api/generated/schema.ts
```

The generated file is gitignored (only `.gitkeep` is tracked) — regenerate
locally when the backend contract changes.

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

`src/api/types.ts` is the hand-rolled boundary that `apiFetch` and the data
hooks import from. It must use **snake_case** field names — the backend
serializes that way (`Program.cs` `ConfigureHttpJsonOptions`). Switching a type
over to the generated file is a single-line change in `client.ts`.
