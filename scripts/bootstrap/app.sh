#!/bin/bash
# App VM bootstrap, invoked by the cloud-init shim. Runs under `set -u`, so the
# shim must export all of: DOMAIN, ACME_EMAIL, MONITORING_DOMAIN,
# SCYLLA_PRIVATE_IP, APP_PRIVATE_IP.

set -euo pipefail
exec > >(tee -a /var/log/ffmt-bootstrap.log) 2>&1

cd /opt/ffmt
source scripts/bootstrap/lib/common.sh
log_info "=== app.sh start ==="

# Every `docker compose` below (and any the operator runs in this shell) picks
# the app-VM file set up from here. bring_up_monitoring's explicit -f wins over it.
export COMPOSE_FILE=docker-compose.yml:docker-compose.app-vm.yml
export COMPOSE_PROFILES=host_metrics

idempotent_apt_install gettext-base dnsutils

export ZERO_SSL_USER_EMAIL="$ACME_EMAIL"
export ZERO_SSL_MAIN_DOMAIN="$DOMAIN"
export ZERO_SSL_MONITORING_DOMAIN="$MONITORING_DOMAIN"
export SCYLLA_PRIVATE_IP APP_PRIVATE_IP
export HOST_PRIVATE_IP="$APP_PRIVATE_IP"
render_env_file env .env
chmod 0600 .env

wait_for_tcp "$SCYLLA_PRIVATE_IP" 9042 600

# DNS must already point here or Caddy's first ACME attempt fails.
self_ipv4
wait_for_dns "$DOMAIN" "$SELF_IPV4" 300

# Scylla lives on the other VM; its compose profile keeps it out of this up.
log_info "Starting app stack..."
docker compose up -d --build

wait_for_http http://127.0.0.1:8080/health 600

bring_up_monitoring

# Sentinel-gated so a bootstrap re-run doesn't reseed the DB.
if [ ! -f /var/lib/ffmt/.updatedb-done ]; then
    log_info "Running first-time ffmt updatedb..."
    bash scripts/sh/update_db_data.sh
    mkdir -p /var/lib/ffmt
    touch /var/lib/ffmt/.updatedb-done
else
    log_info "updatedb sentinel already present, skipping seed."
fi

wait_for_http "https://$DOMAIN/" 300

ensure_app_crons

mkdir -p /var/lib/ffmt
touch /var/lib/ffmt/.app-bootstrap-done
log_info "=== app.sh done ==="
