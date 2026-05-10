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
    (crontab -l 2>/dev/null; echo "$line") | crontab -
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
