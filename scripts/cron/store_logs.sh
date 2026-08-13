#!/bin/bash
set -euo pipefail

# Repo path comes from the cron line's FFMT_REPO - don't hardcode it back.
FFMT_REPO="${FFMT_REPO:-/opt/ffmt}"

LOG_DIR="${LOG_DIR:-/root/logs}"
TEMP="$LOG_DIR/temp"

echo "$(date -Is) - store_logs cron started" >> "$LOG_DIR/cron.log"

mkdir -p "$LOG_DIR" "$TEMP"

# One subdir per service, bind-mounted at /app/logs in each container
# (docker-compose.yml: ./logs/backend, ./logs/ws_worker).
for dir in "$FFMT_REPO"/logs/*/; do
    [ -d "$dir" ] || continue
    service="$(basename "$dir")"
    mkdir -p "$TEMP/$service"
    mv "$dir"*.log "$TEMP/$service/" 2>/dev/null || true
done

zip -r "$LOG_DIR/$(date +%Y-%m-%d_%H-%M-%S).zip" "$TEMP" >> "$LOG_DIR/cron.log"
rm -rf "$TEMP"

echo "$(date -Is) - store_logs cron finished" >> "$LOG_DIR/cron.log"
