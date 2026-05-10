#!/bin/bash
# redeploy.sh — pull + rebuild + re-up the app stack.
# Usage: bash scripts/bootstrap/redeploy.sh [--updatedb] [--ref <ref>]

set -euo pipefail
exec > >(tee -a /var/log/ffmt-redeploy.log) 2>&1

cd /opt/ffmt
source scripts/bootstrap/lib/common.sh
log_info "=== redeploy.sh start ==="

ARG_UPDATEDB=0
ARG_REF=""
while [ $# -gt 0 ]; do
    case "$1" in
        --updatedb) ARG_UPDATEDB=1; shift ;;
        --ref) ARG_REF="$2"; shift 2 ;;
        *) log_err "Unknown arg: $1"; exit 2 ;;
    esac
done

# Refuse on dirty tree (no implicit stash).
if ! git diff --quiet || ! git diff --cached --quiet; then
    log_err "Working tree has uncommitted changes — refusing to redeploy."
    exit 1
fi

# Sync to the requested ref (default: tracked branch's tip).
git fetch --tags
if [ -n "$ARG_REF" ]; then
    log_info "Checking out ref: $ARG_REF"
    git checkout "$ARG_REF"
else
    git pull --ff-only
fi

# Re-render env in case template changed.
render_env_file env .env

# Rebuild + re-up.
docker compose \
    -f docker-compose.yml \
    -f docker-compose.app-vm.yml \
    up -d --build

wait_for_http http://127.0.0.1:8080/health 300

if [ "$ARG_UPDATEDB" -eq 1 ]; then
    log_info "Running ffmt updatedb..."
    bash scripts/sh/update_db_data_dotnet.sh
fi

docker compose -f docker-compose.yml -f docker-compose.app-vm.yml ps
log_info "=== redeploy.sh done ==="
