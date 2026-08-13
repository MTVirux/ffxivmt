#!/bin/bash
# Sourced by scylla.sh / app.sh / redeploy.sh. Callers must run under
# `set -euo pipefail`.

log_info() { printf '[INFO  %s] %s\n' "$(date -Is)" "$*"; }
log_warn() { printf '[WARN  %s] %s\n' "$(date -Is)" "$*" >&2; }
log_err()  { printf '[ERROR %s] %s\n' "$(date -Is)" "$*" >&2; }

# wait_until <desc> <timeout_s> <interval_s> -- cmd...
# Drops the command's stdout but keeps stderr, so a persistent failure is still
# diagnosable from the log.
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

wait_for_tcp() { wait_until "TCP $1:$2" "${3:-300}" 2 -- _tcp_open "$1" "$2"; }

wait_for_http() { wait_until "HTTP $1" "${2:-300}" 5 -- curl -fsS -o /dev/null --max-time 5 "$1"; }

wait_for_dns() { wait_until "DNS $1 -> $2" "${3:-300}" 10 -- _dns_resolves_to "$1" "$2"; }

wait_for_volume_device() { wait_until "volume device $1" "${2:-60}" 1 -- test -b "$1"; }

# Memoizes into the global SELF_IPV4 rather than echoing - a $(...) call would
# run in a subshell and lose the cache.
self_ipv4() {
    if [ -z "${SELF_IPV4:-}" ]; then
        SELF_IPV4="$(curl -fsS https://ipv4.icanhazip.com)"
        log_info "Self public IPv4: $SELF_IPV4"
    fi
}

# Caller must `export` the substitution variables beforehand.
render_env_file() {
    local template="$1" output="$2"
    log_info "Rendering $output from $template..."
    envsubst < "$template" > "${output}.tmp"
    mv -f "${output}.tmp" "$output"
}

ensure_cron() {
    local line="$1"
    log_info "Ensuring cron line: $line"
    if crontab -l 2>/dev/null | grep -Fxq "$line"; then
        log_info "  (already present)"
        return 0
    fi
    { crontab -l 2>/dev/null || true; echo "$line"; } | crontab -
}

# Called from app.sh and redeploy.sh, so a cron added here also reaches VMs that
# have already booted - cloud-init only ever runs first-boot.
ensure_app_crons() {
    ensure_cron "0 0 * * * FFMT_REPO=/opt/ffmt /opt/ffmt/scripts/cron/store_logs.sh >> /var/log/ffmt-cron.log 2>&1"
    ensure_cron "0 1 * * * docker exec ffmt_backend ffmt update-baselines >> /var/log/ffmt-baselines.log 2>&1"
    # Archiving stays manual: ensure_cron only ever adds, so listing an archive
    # cron here would switch archiving back on for every VM on the next deploy.
    # update-garland is the only writer of the craftable flag and update-items
    # clears it, so without this weekly re-derive a half-finished updatedb leaves
    # the gilflux crafted filter matching nothing.
    ensure_cron "30 4 * * 0 docker exec ffmt_backend ffmt update-garland >> /var/log/ffmt-garland.log 2>&1"
}

ensure_fstab() {
    local line="$1"
    log_info "Ensuring fstab line: $line"
    if grep -Fxq "$line" /etc/fstab; then
        log_info "  (already present)"
        return 0
    fi
    echo "$line" >> /etc/fstab
}

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

# Prints the secret at $1, generating it on first use. Logs go to stderr so the
# value stays alone on stdout for command substitution.
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

    # app.sh gets this from cloud-init; redeploy.sh only has the rendered .env,
    # where Caddy and Grafana read the same value.
    : "${MONITORING_DOMAIN:=${ZERO_SSL_MONITORING_DOMAIN:-monitoring.${ZERO_SSL_MAIN_DOMAIN}}}"
    log_info "Monitoring domain: $MONITORING_DOMAIN"

    mkdir -p /var/lib/ffmt
    chmod 0750 /var/lib/ffmt

    local admin_pass secret_key
    admin_pass="$(ensure_secret /var/lib/ffmt/grafana-admin-pass)"
    secret_key="$(ensure_secret /var/lib/ffmt/grafana-secret-key)"

    mkdir -p docker/monitoring/prometheus/rendered
    chmod 0755 docker/monitoring/prometheus/rendered
    export SCYLLA_PRIVATE_IP APP_PRIVATE_IP
    envsubst < docker/monitoring/prometheus/scylla_servers.yml.tpl > docker/monitoring/prometheus/rendered/scylla_servers.yml
    envsubst < docker/monitoring/prometheus/node_exporter_servers.yml.tpl > docker/monitoring/prometheus/rendered/node_exporter_servers.yml

    # Both callers re-render .env from the template immediately before this, so
    # appending is safe - there is no stale copy of these keys to strip first.
    {
        echo "GF_SECURITY_ADMIN_PASSWORD=${admin_pass}"
        echo "GF_SECURITY_SECRET_KEY=${secret_key}"
    } >> .env
    chmod 0600 .env

    # The monitoring subdomain must resolve here before Caddy attempts ACME.
    self_ipv4
    wait_for_dns "$MONITORING_DOMAIN" "$SELF_IPV4" 300

    docker compose -f docker-compose.monitoring.yml up -d --build

    # Probed through docker exec because this stack publishes no host ports.
    wait_until "Grafana /api/health" 300 5 -- \
        docker exec ffmt_grafana wget -qO- http://127.0.0.1:3000/api/health
    wait_until "Prometheus /-/ready" 60 5 -- \
        docker exec ffmt_prometheus wget -qO- http://127.0.0.1:9090/-/ready

    # Public HTTPS answering proves Caddy's ACME run succeeded.
    wait_for_http "https://$MONITORING_DOMAIN/login" 300

    log_info "=== bring_up_monitoring done ==="
}
