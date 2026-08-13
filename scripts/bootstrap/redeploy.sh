#!/bin/bash
# Usage: bash scripts/bootstrap/redeploy.sh [--updatedb] [--ref <ref>]

set -euo pipefail
exec > >(tee -a /var/log/ffmt-redeploy.log) 2>&1

cd /opt/ffmt
source scripts/bootstrap/lib/common.sh
log_info "=== redeploy.sh start ==="

# Every `docker compose` below (and any the operator runs in this shell) picks
# the app-VM file set up from here. bring_up_monitoring's explicit -f wins over it.
export COMPOSE_FILE=docker-compose.yml:docker-compose.app-vm.yml
export COMPOSE_PROFILES=host_metrics

ARG_UPDATEDB=0
ARG_REF=""
while [ $# -gt 0 ]; do
    case "$1" in
        --updatedb) ARG_UPDATEDB=1; shift ;;
        --ref) ARG_REF="$2"; shift 2 ;;
        *) log_err "Unknown arg: $1"; exit 2 ;;
    esac
done

if ! git diff --quiet || ! git diff --cached --quiet; then
    log_err "Working tree has uncommitted changes - refusing to redeploy."
    exit 1
fi

git fetch --tags
if [ -n "$ARG_REF" ]; then
    log_info "Checking out ref: $ARG_REF"
    git checkout "$ARG_REF"
else
    git pull --ff-only
fi

# The new template is rendered from the OLD .env, so source it first to carry
# over the values bootstrap set.
if [ -f .env ]; then
    set -a
    # shellcheck disable=SC1091
    . .env
    set +a
fi

# An .env predating the HOST_PRIVATE_IP entry has nothing to carry over, so
# re-export it here - this is the app VM, so it is APP_PRIVATE_IP.
export HOST_PRIVATE_IP="${APP_PRIVATE_IP:?APP_PRIVATE_IP missing from .env}"

render_env_file env .env

docker compose up -d --build

wait_for_http http://127.0.0.1:8080/health 300

bring_up_monitoring

ensure_app_crons

if [ "$ARG_UPDATEDB" -eq 1 ]; then
    log_info "Running ffmt updatedb..."
    bash scripts/sh/update_db_data.sh
fi

docker compose ps
log_info "=== redeploy.sh done ==="
