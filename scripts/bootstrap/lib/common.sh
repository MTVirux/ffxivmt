#!/bin/bash
# Common bootstrap helpers. Sourced by scylla.sh / app.sh / redeploy.sh.
# Callers MUST run under `set -euo pipefail`.

log_info() { printf '[INFO  %s] %s\n' "$(date -Is)" "$*"; }
log_warn() { printf '[WARN  %s] %s\n' "$(date -Is)" "$*" >&2; }
log_err()  { printf '[ERROR %s] %s\n' "$(date -Is)" "$*" >&2; }

# Wait for a TCP host:port to accept connections.
wait_for_tcp() {
    local host="$1" port="$2" timeout="${3:-300}" elapsed=0
    log_info "Waiting on TCP $host:$port (timeout ${timeout}s)..."
    while ! (echo > "/dev/tcp/$host/$port") 2>/dev/null; do
        if [ "$elapsed" -ge "$timeout" ]; then
            log_err "Timed out waiting for $host:$port"
            return 1
        fi
        sleep 2
        elapsed=$((elapsed + 2))
    done
    log_info "TCP $host:$port is open."
}

# Wait for an HTTP URL to return any 2xx/3xx.
wait_for_http() {
    local url="$1" timeout="${2:-300}" elapsed=0
    log_info "Waiting on HTTP $url (timeout ${timeout}s)..."
    while ! curl -fsS -o /dev/null --max-time 5 "$url"; do
        if [ "$elapsed" -ge "$timeout" ]; then
            log_err "Timed out waiting for $url"
            return 1
        fi
        sleep 5
        elapsed=$((elapsed + 5))
    done
    log_info "HTTP $url returned 2xx/3xx."
}

# Wait for $domain's first A record to equal $expected.
wait_for_dns() {
    local domain="$1" expected="$2" timeout="${3:-300}" elapsed=0
    log_info "Waiting on DNS $domain to resolve to $expected (timeout ${timeout}s)..."
    while [ "$(dig +short "$domain" A | head -1)" != "$expected" ]; do
        if [ "$elapsed" -ge "$timeout" ]; then
            log_err "Timed out waiting for DNS $domain → $expected"
            return 1
        fi
        sleep 10
        elapsed=$((elapsed + 10))
    done
    log_info "DNS $domain resolves to $expected."
}

# Wait for a block device (e.g. attached Hetzner volume) to appear.
wait_for_volume_device() {
    local path="$1" timeout="${2:-60}" elapsed=0
    log_info "Waiting on volume device $path (timeout ${timeout}s)..."
    while [ ! -b "$path" ]; do
        if [ "$elapsed" -ge "$timeout" ]; then
            log_err "Volume device $path did not appear"
            return 1
        fi
        sleep 1
        elapsed=$((elapsed + 1))
    done
    log_info "Volume device $path is available."
}

# Render an envsubst template to an output path atomically.
# Caller must `export` the substitution variables beforehand.
render_env_file() {
    local template="$1" output="$2"
    log_info "Rendering $output from $template..."
    envsubst < "$template" > "${output}.tmp"
    mv -f "${output}.tmp" "$output"
}

# Append a crontab line if not already present (exact-match).
ensure_cron() {
    local line="$1"
    log_info "Ensuring cron line: $line"
    if crontab -l 2>/dev/null | grep -Fxq "$line"; then
        log_info "  (already present)"
        return 0
    fi
    { crontab -l 2>/dev/null || true; echo "$line"; } | crontab -
}

# Append an /etc/fstab line if not already present (exact-match).
ensure_fstab() {
    local line="$1"
    log_info "Ensuring fstab line: $line"
    if grep -Fxq "$line" /etc/fstab; then
        log_info "  (already present)"
        return 0
    fi
    echo "$line" >> /etc/fstab
}

# apt-install only the packages that aren't already installed.
idempotent_apt_install() {
    local missing=()
    local pkg
    for pkg in "$@"; do
        if ! dpkg -s "$pkg" >/dev/null 2>&1; then
            missing+=("$pkg")
        fi
    done
    if [ "${#missing[@]}" -gt 0 ]; then
        log_info "Installing: ${missing[*]}"
        apt-get update
        apt-get install -y "${missing[@]}"
    else
        log_info "All packages already installed: $*"
    fi
}

bring_up_monitoring() {
    log_info "=== bring_up_monitoring start ==="

    : "${MONITORING_DOMAIN:=monitoring.${ZERO_SSL_MAIN_DOMAIN}}"
    log_info "Monitoring domain: $MONITORING_DOMAIN"

    # 1. Generate / load Grafana secrets.
    mkdir -p /var/lib/ffmt
    chmod 0750 /var/lib/ffmt

    if [ ! -f /var/lib/ffmt/grafana-admin-pass ]; then
        openssl rand -base64 32 | tr -d /=+ | cut -c1-32 > /var/lib/ffmt/grafana-admin-pass
        chmod 0600 /var/lib/ffmt/grafana-admin-pass
        log_info "Grafana admin password written to /var/lib/ffmt/grafana-admin-pass — read once via SSH and store it."
    fi
    local admin_pass
    admin_pass="$(cat /var/lib/ffmt/grafana-admin-pass)"

    if [ ! -f /var/lib/ffmt/grafana-secret-key ]; then
        openssl rand -base64 32 | tr -d /=+ | cut -c1-32 > /var/lib/ffmt/grafana-secret-key
        chmod 0600 /var/lib/ffmt/grafana-secret-key
    fi
    local secret_key
    secret_key="$(cat /var/lib/ffmt/grafana-secret-key)"

    # 2. Render Prometheus scrape target files.
    mkdir -p docker/monitoring/prometheus/rendered
    chmod 0755 docker/monitoring/prometheus/rendered
    export SCYLLA_PRIVATE_IP APP_PRIVATE_IP
    envsubst < docker/monitoring/prometheus/scylla_servers.yml.tpl > docker/monitoring/prometheus/rendered/scylla_servers.yml
    envsubst < docker/monitoring/prometheus/node_exporter_servers.yml.tpl > docker/monitoring/prometheus/rendered/node_exporter_servers.yml

    # 3. Upsert monitoring env in .env (idempotent — sed-delete then append).
    sed -i '/^ZERO_SSL_MONITORING_DOMAIN=/d' .env
    sed -i '/^GF_SECURITY_ADMIN_PASSWORD=/d' .env
    sed -i '/^GF_SECURITY_SECRET_KEY=/d' .env
    sed -i '/^GF_SERVER_DOMAIN=/d' .env
    {
        echo "ZERO_SSL_MONITORING_DOMAIN=${MONITORING_DOMAIN}"
        echo "GF_SECURITY_ADMIN_PASSWORD=${admin_pass}"
        echo "GF_SECURITY_SECRET_KEY=${secret_key}"
        echo "GF_SERVER_DOMAIN=${MONITORING_DOMAIN}"
    } >> .env
    chmod 0600 .env

    # 4. Wait for DNS on the monitoring subdomain so Caddy ACME can succeed.
    wait_for_dns "$MONITORING_DOMAIN" "$SELF_IPV4" 300

    # 5. Bring up the monitoring stack.
    docker compose -f docker-compose.monitoring.yml up -d --build

    # 6. Readiness via docker exec (no host port bindings).
    local elapsed=0
    log_info "Waiting on Grafana /api/health (up to 300s)..."
    until docker exec ffmt_grafana wget -qO- http://127.0.0.1:3000/api/health >/dev/null 2>&1; do
        if [ "$elapsed" -ge 300 ]; then log_err "Grafana not ready"; return 1; fi
        sleep 5; elapsed=$((elapsed + 5))
    done
    log_info "Grafana is healthy."

    elapsed=0
    log_info "Waiting on Prometheus /-/ready (up to 60s)..."
    until docker exec ffmt_prometheus wget -qO- http://127.0.0.1:9090/-/ready >/dev/null 2>&1; do
        if [ "$elapsed" -ge 60 ]; then log_err "Prometheus not ready"; return 1; fi
        sleep 5; elapsed=$((elapsed + 5))
    done
    log_info "Prometheus is ready."

    # 7. Wait for public HTTPS (proves Caddy ACME succeeded).
    wait_for_http "https://$MONITORING_DOMAIN/login" 300

    log_info "=== bring_up_monitoring done ==="
}
