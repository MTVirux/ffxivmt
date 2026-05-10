# Deployment

How to bring up a fresh production stack on Hetzner Cloud using the `ffxivmt-tf`
Terraform module.

The short version: `terraform apply` provisions two VMs and a block volume,
cloud-init clones this repo onto each VM and runs the matching script under
`scripts/bootstrap/`. End-to-end from a clean laptop to a live, seeded
`https://<your-domain>` is around ten to fifteen minutes, most of which is the
.NET image build on the app VM.

If you've done this before and just need the commands, jump to
[Doing it](#doing-it). Otherwise, the Hetzner-side setup matters and isn't
undone by `terraform destroy`, so it's worth getting right once.

## Hetzner-side setup (one time)

Things that need to exist in your Hetzner account before `terraform apply`
works. None of this is created by Terraform — it's where Terraform points.

A **Cloud project**. Create one in the web console. Resources, API tokens, and
billing all scope to a project. Name it whatever; `ffmt` is fine. The TF
module doesn't reference the project by name — it just uses whichever project
the API token belongs to.

A **Cloud API token** scoped to that project. Console → Security → API Tokens
→ Read+Write. Call it `ffmt-tf` so revoking it later doesn't kill any other
automation.

A **DNS zone** for the apex domain you're going to deploy on. Hetzner DNS
console → Add Zone. The TF module references this as a data source, so it has
to exist before apply — TF won't create it. After Hetzner shows you the zone's
nameservers, point your registrar at them. Propagation can take an hour; do
this early.

A **DNS API token**. Separate from the Cloud token. Same place but in the DNS
console.

That's it. Everything else — networks, firewalls, volumes, servers, the actual
A/AAAA records — TF creates.

## Operator side (one time)

You need `terraform >= 1.6`, `git`, and `direnv` on your laptop. The wizard
uses direnv to load API tokens into the shell so they don't end up in
`terraform.tfvars` (and, by extension, in state). If you really hate direnv,
the wizard prints a `source .envrc` line you can paste each session, but
direnv is worth the five minutes of one-time setup.

```sh
git clone git@github.com:MTVirux/ffxivmt-tf.git ffmt-tf
cd ffmt-tf
bash setup.sh
```

The wizard prompts through everything. Defaults are sensible — if you're
deploying in Falkenstein on CCX13/CCX23 with a 100 GB Scylla volume, hit Enter
through most of it. The prompts that need your real values are the apex
domain, the ACME contact email, and the SSH allowlist (which auto-detects
your current public IP — broaden it if you SSH from multiple places). Tokens
go in last, hidden. The wizard verifies them by hitting the Hetzner API
before writing anything to disk.

It writes `terraform.tfvars` and `.envrc`, both gitignored, both `chmod 600`.

## Doing it

```sh
direnv allow
terraform init
terraform apply
```

Approve the plan. About two minutes later TF returns with public IPs in its
outputs. The VMs themselves are still busy — cloud-init runs unattended on
each, and the slow part is the first-time Docker image build for the .NET
backend. Plan on five to ten more minutes.

To watch:

```sh
ssh root@$(terraform output -raw app_public_ipv4) \
  'tail -f /var/log/ffmt-bootstrap.log'
```

When you see `=== app.sh done ===`, the stack is up. Run `bash check.sh` —
it's read-only, calls `dig`/`ssh`/`curl` to confirm DNS resolves, both VMs
are reachable, the bootstrap sentinels exist, and Caddy got a real cert from
Let's Encrypt. If it prints "All checks passed", you're done.

If something's gone sideways at this point, see
[When things go wrong](#when-things-go-wrong).

## What's actually happening while you wait

Worth understanding because it determines what to do when something gets
stuck.

Both VMs boot Ubuntu 24.04 with a cloud-init `user_data` script TF rendered
for them. The shim is identical between roles — install Docker from the
official apt repo, clone this repo to `/opt/ffmt`, then `exec` the matching
role bootstrap. Anything role-specific lives in `scripts/bootstrap/scylla.sh`
or `scripts/bootstrap/app.sh`.

The Scylla VM goes first because the app VM has a TF dependency on it. Its
bootstrap waits for the block volume's device node to appear, formats it
ext4 if blank, mounts it at `/mnt/scylla-data`, and brings up the Scylla
container with the override compose file that re-points the data path at the
volume. Then it waits for CQL on `:9042` and installs the nightly backup
cron. Done in about a minute.

The app VM has more to do. After Docker installs and the repo clones, it
renders `.env` from the `env` template (envsubst fills in the domain, ACME
email, and Scylla's private IP), waits for the Scylla VM's CQL port across
the private network, then waits for DNS to resolve to its own public IP.
That DNS wait is what prevents Caddy from issuing an ACME cert for the
wrong address — without it, a fast Hetzner+slow registrar combination would
race and fail. Then `docker compose up -d --build`. The build is the bulk
of the wait; nothing fast about compiling .NET on a 2-vCPU VM.

Once the backend's `/health` endpoint comes alive on `127.0.0.1:8080`, the
bootstrap runs `ffmt updatedb` once to populate items, worlds, marketability,
and the Elasticsearch index. The first run is slow (minutes — Garland Tools
and the items CSV both need to be pulled and processed). Subsequent boots
skip it because of the sentinel at `/var/lib/ffmt/.updatedb-done`. Last step
is the log-rotation cron.

If you're curious, `cat /var/log/ffmt-bootstrap.log` on either VM gives you
the full timeline with timestamps.

## Updates after the initial deploy

Push your changes to the public ffxivmt repo. Then:

```sh
ssh root@$(terraform output -raw app_public_ipv4) \
  'bash /opt/ffmt/scripts/bootstrap/redeploy.sh'
```

It refuses if the working tree on the box is dirty — you shouldn't be editing
files there, fix this on your laptop and push. Otherwise: fetch,
fast-forward pull, re-render `.env` from the (possibly updated) template,
`docker compose up -d --build`, wait for `/health`, print container status.
Three minutes for a typical change.

To roll back to a specific ref instead of pulling the tip:

```sh
bash /opt/ffmt/scripts/bootstrap/redeploy.sh --ref v0.5.2
```

If your change involves a schema-affecting `ffmt updatedb` (rare):

```sh
bash /opt/ffmt/scripts/bootstrap/redeploy.sh --updatedb
```

The Scylla VM rarely needs anything done to it. If you ever bump the Scylla
container version, the same `redeploy.sh` works there — SSH in and
`git pull && docker compose -f docker-compose.yml -f docker-compose.scylla-vm.yml --profile scylla up -d --build`
directly.

## Tearing it down

```sh
terraform destroy
```

A minute later everything's gone — VMs, network, firewalls, volume, DNS
records. **Volume data is destroyed with the volume.** If you want history
preserved through a destroy, take a Hetzner snapshot first via the web
console or API.

Local TF state stays put, so you can `terraform apply` again from the same
configuration whenever you want the stack back. Cloud-init reruns from
scratch on the new VMs.

## When things go wrong

A list of things that have actually broken.

**Cloud-init hangs in the middle of bootstrap.** SSH in and
`tail -200 /var/log/ffmt-bootstrap.log`. The bootstrap scripts run under
`set -euo pipefail` and propagate failures, so the last log lines tell you
exactly which step died. Fix the root cause, then re-run
`bash /opt/ffmt/scripts/bootstrap/<role>.sh`. Every step is idempotent;
sentinel files gate the things that shouldn't repeat.

**Caddy keeps failing ACME.** Almost always a DNS race — your domain isn't
resolving to the app VM yet. `dig +short $domain` should print the app VM's
public IPv4. If it doesn't, give it ten minutes and check again, then
`docker compose restart ffmt_proxy`. ACME has aggressive backoff, so if
you've been failing for a while you may need to wait Let's Encrypt out
before it retries.

**Backend logs `unable to connect to Scylla`.** The app VM's `.env` has
the wrong `SCYLLA_PRIVATE_IP`. This shouldn't happen if you didn't tweak
`app_private_ip` / `scylla_private_ip` in tfvars after first apply, but
it's the first thing to check. Then SSH to the Scylla VM and confirm
`docker logs ffmt_scylla_node` shows it listening on the right address.

**`ffmt updatedb` failed and now the app is up but empty.** The sentinel
didn't get written, so `redeploy.sh --updatedb` (or just running
`bash /opt/ffmt/scripts/sh/update_db_data_dotnet.sh` directly) will retry.
Common causes: Garland or the items CSV being temporarily unreachable. The
script is mostly idempotent; rerunning is safe.

**You destroyed the Scylla VM by accident.** Re-`terraform apply` recreates
it. The volume is a separate resource — TF doesn't destroy it unless you
`taint` it explicitly — so it reattaches to the new VM. Cloud-init's
`mkfs.ext4` step is gated by `blkid`; if there's already a filesystem, it
skips. Scylla starts, reads the existing data, you're back. Five minutes,
no data loss.

**You destroyed the app VM by accident.** Same drill, slightly faster (no
volume reattachment dance, but `ffmt updatedb` will run again on the fresh
VM since the sentinel is gone).

**The SPA renders but `/api/*` returns 502.** Caddy can't reach the
backend. If you upgraded from an older version of this repo, check the
Caddyfile is referencing `ffmt_backend` and not `ffmt_backend_dotnet` —
that was the old service name. Otherwise, `docker logs ffmt_proxy` for
hostname resolution errors and `docker logs ffmt_backend` for whether the
backend's actually up.

If none of those match, SSH in, `docker compose ps`, and start poking. The
stack is small enough you can hold the whole thing in your head.
