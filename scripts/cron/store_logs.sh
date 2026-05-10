#!/bin/bash
set -euo pipefail

# Repo path is expected to be passed via FFMT_REPO (defaults to /opt/ffmt).
FFMT_REPO="${FFMT_REPO:-/opt/ffmt}"

LOG_DIR="${LOG_DIR:-/root/logs}"
TEMP="$LOG_DIR/temp"

echo "$(date -Is) - store_logs cron started" >> "$LOG_DIR/cron.log"

mkdir -p "$LOG_DIR" \
         "$TEMP/backend" \
         "$TEMP/ws_worker/action" \
         "$TEMP/ws_worker/error" \
         "$TEMP/ws_worker/debug"

mv "$FFMT_REPO/backend/application/logs/"*.log                    "$TEMP/backend/"        2>/dev/null || true
mv "$FFMT_REPO/docker/ws_worker/server/logs/action/"*.log         "$TEMP/ws_worker/action/" 2>/dev/null || true
mv "$FFMT_REPO/docker/ws_worker/server/logs/error/"*.log          "$TEMP/ws_worker/error/"  2>/dev/null || true
mv "$FFMT_REPO/docker/ws_worker/server/logs/debug/"*.log          "$TEMP/ws_worker/debug/"  2>/dev/null || true

zip -r "$LOG_DIR/$(date +%Y-%m-%d_%H-%M-%S).zip" "$TEMP" >> "$LOG_DIR/cron.log"
rm -rf "$TEMP"

echo "$(date -Is) - store_logs cron finished" >> "$LOG_DIR/cron.log"
