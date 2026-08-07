#!/bin/bash
# Common bootstrap helpers. Sourced by scylla.sh / app.sh / redeploy.sh.
# Callers MUST run under `set -euo pipefail`.

log_info() { printf '[INFO  %s] %s\n' "$(date -Is)" "$*"; }
log_warn() { printf '[WARN  %s] %s\n' "$(date -Is)" "$*" >&2; }
log_err()  { printf '[ERROR %s] %s\n' "$(date -Is)" "$*" >&2; }

# wait_until <desc> <timeout_s> <interval_s> -- cmd...
# Retries cmd until it succeeds or the timeout elapses. Command stdout is
# dropped; stderr is kept so a persistent failure is diagnosable from the log.
wait_until() {
    local desc="$1" timeout="$2" interval="$3" elapsed=0
    shift 3
    if [ "${1:-}" = "--" ]; then shift; fi
    log_info "Waiting on $desc (timeout ${timeout}s)..."
    until "$@" >/dev/null; do
        if [ "$elapsed" -ge "$timeout" ]; then
            log_err "Timed out waiting for $desc"
            return 1
        fi
        sleep "$interval"
        elapsed=$((elapsed + interval))
    done
    log_info "$desc is ready."
}

_tcp_open() { (echo > "/dev/tcp/$1/$2") 2>/dev/null; }
_dns_resolves_to() { [ "$(dig +short "$1" A | head -1)" = "$2" ]; }

# Wait for a TCP host:port to accept connections.
wait_for_tcp() { wait_until "TCP $1:$2" "${3:-300}" 2 -- _tcp_open "$1" "$2"; }

# Wait for an HTTP URL to return any 2xx/3xx.
wait_for_http() { wait_until "HTTP $1" "${2:-300}" 5 -- curl -fsS -o /dev/null --max-time 5 "$1"; }

# Wait for $1's first A record to equal $2.
wait_for_dns() { wait_until "DNS $1 -> $2" "${3:-300}" 10 -- _dns_resolves_to "$1" "$2"; }

# Wait for a block device (e.g. attached Hetzner volume) to appear.
wait_for_volume_device() { wait_until "volume device $1" "${2:-60}" 1 -- test -b "$1"; }

# Memoizes into the global SELF_IPV4 rather than echoing - a $(...) call would
# run in a subshell and lose the cache.
self_ipv4() {
    if [ -z "${SELF_IPV4:-}" ]; then
        SELF_IPV4="$(curl -fsS https://ipv4.icanhazip.com)"
        log_info "Self public IPv4: $SELF_IPV4"
    fi
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

# App-VM cron set. Called from app.sh and redeploy.sh so that adding a cron here
# reaches VMs that have already booted - cloud-init only ever runs first-boot.
ensure_app_crons() {
    ensure_cron "0 0 * * * FFMT_REPO=/opt/ffmt /opt/ffmt/scripts/cron/store_logs.sh >> /var/log/ffmt-cron.log 2>&1"
    ensure_cron "0 1 * * * docker exec ffmt_backend ffmt update-baselines >> /var/log/ffmt-baselines.log 2>&1"
    # The archive crons are deliberately not installed - archiving stays manual
    # for now. ensure_cron only ever adds, so re-listing them here would silently
    # switch archiving back on for every VM on the next deploy.
    # update-garland is the only writer of the craftable flag, and update-items
    # clears it. Without a recurring re-derive, a half-finished updatedb leaves
    # the gilflux crafted filter matching nothing until someone reruns it by hand.
    ensure_cron "30 4 * * 0 docker exec ffmt_backend ffmt update-garland >> /var/log/ffmt-garland.log 2>&1"
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

# Print the secret at $1, generating it first if it does not exist yet.
# Logs to stderr so the value stays alone on stdout for command substitution.
ensure_secret() {
    local path="$1"
    if [ ! -f "$path" ]; then
        openssl rand -base64 32 | tr -d /=+ | cut -c1-32 > "$path"
        chmod 0600 "$path"
        log_info "Generated $path - read it once via SSH and store it." >&2
    fi
    cat "$path"
}

bring_up_monitoring() {
    log_info "=== bring_up_monitoring start ==="

    : "${MONITORING_DOMAIN:=monitoring.${ZERO_SSL_MAIN_DOMAIN}}"
    log_info "Monitoring domain: $MONITORING_DOMAIN"

    # 1. Generate / load Grafana secrets.
    mkdir -p /var/lib/ffmt
    chmod 0750 /var/lib/ffmt

    local admin_pass secret_key
    admin_pass="$(ensure_secret /var/lib/ffmt/grafana-admin-pass)"
    secret_key="$(ensure_secret /var/lib/ffmt/grafana-secret-key)"

    # 2. Render Prometheus scrape target files.
    mkdir -p docker/monitoring/prometheus/rendered
    chmod 0755 docker/monitoring/prometheus/rendered
    export SCYLLA_PRIVATE_IP APP_PRIVATE_IP
    envsubst < docker/monitoring/prometheus/scylla_servers.yml.tpl > docker/monitoring/prometheus/rendered/scylla_servers.yml
    envsubst < docker/monitoring/prometheus/node_exporter_servers.yml.tpl > docker/monitoring/prometheus/rendered/node_exporter_servers.yml

    # 3. Append the Grafana secrets. Both callers render .env from the template
    # immediately before this, so there is nothing stale to strip first.
    {
        echo "GF_SECURITY_ADMIN_PASSWORD=${admin_pass}"
        echo "GF_SECURITY_SECRET_KEY=${secret_key}"
    } >> .env
    chmod 0600 .env

    # 4. Wait for DNS on the monitoring subdomain so Caddy ACME can succeed.
    self_ipv4
    wait_for_dns "$MONITORING_DOMAIN" "$SELF_IPV4" 300

    # 5. Bring up the monitoring stack.
    docker compose -f docker-compose.monitoring.yml up -d --build

    # 6. Readiness via docker exec (no host port bindings).
    wait_until "Grafana /api/health" 300 5 -- \
        docker exec ffmt_grafana wget -qO- http://127.0.0.1:3000/api/health
    wait_until "Prometheus /-/ready" 60 5 -- \
        docker exec ffmt_prometheus wget -qO- http://127.0.0.1:9090/-/ready

    # 7. Wait for public HTTPS (proves Caddy ACME succeeded).
    wait_for_http "https://$MONITORING_DOMAIN/login" 300

    log_info "=== bring_up_monitoring done ==="
}
