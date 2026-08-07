#!/bin/bash
# app.sh — App VM in-box bootstrap. Invoked by the cloud-init shim.
# Required env vars (set by the shim):
#   DOMAIN, ACME_EMAIL    — Caddy / ACME inputs
#   SCYLLA_PRIVATE_IP     — peer Scylla VM's private IP
#   APP_PRIVATE_IP        — this VM's private IP
#   REPO_URL, REPO_REF    — informational; repo is already cloned at /opt/ffmt

set -euo pipefail
exec > >(tee -a /var/log/ffmt-bootstrap.log) 2>&1

cd /opt/ffmt
source scripts/bootstrap/lib/common.sh
log_info "=== app.sh start ==="

# Every `docker compose` below (and any the operator runs in this shell) picks
# the app-VM file set up from here. bring_up_monitoring's explicit -f wins over it.
export COMPOSE_FILE=docker-compose.yml:docker-compose.app-vm.yml
export COMPOSE_PROFILES=host_metrics

# 1. Install gettext-base for envsubst (used by render_env_file).
idempotent_apt_install gettext-base dnsutils

# 2. Render .env from template
export ZERO_SSL_USER_EMAIL="$ACME_EMAIL"
export ZERO_SSL_MAIN_DOMAIN="$DOMAIN"
export ZERO_SSL_MONITORING_DOMAIN="$MONITORING_DOMAIN"
export SCYLLA_PRIVATE_IP APP_PRIVATE_IP
export HOST_PRIVATE_IP="$APP_PRIVATE_IP"
render_env_file env .env
chmod 0600 .env

# 3. Wait on Scylla (peer VM may still be booting)
wait_for_tcp "$SCYLLA_PRIVATE_IP" 9042 600

# 4. Wait on DNS — avoid ACME race
self_ipv4
wait_for_dns "$DOMAIN" "$SELF_IPV4" 300

# 5. Bring up app stack (Scylla excluded by profile)
log_info "Starting app stack..."
docker compose up -d --build

# 6. Wait for backend health
wait_for_http http://127.0.0.1:8080/health 600

# 6a. Monitoring stack.
bring_up_monitoring

# 7. First-time DB seed
if [ ! -f /var/lib/ffmt/.updatedb-done ]; then
    log_info "Running first-time ffmt updatedb..."
    bash scripts/sh/update_db_data.sh
    mkdir -p /var/lib/ffmt
    touch /var/lib/ffmt/.updatedb-done
else
    log_info "updatedb sentinel already present, skipping seed."
fi

# 8. Wait for HTTPS via Caddy
wait_for_http "https://$DOMAIN/" 300

# 9. Crons
ensure_app_crons

# 10. Sentinel
mkdir -p /var/lib/ffmt
touch /var/lib/ffmt/.app-bootstrap-done
log_info "=== app.sh done ==="
