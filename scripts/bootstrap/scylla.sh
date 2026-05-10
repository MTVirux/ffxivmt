#!/bin/bash
# scylla.sh — Scylla VM in-box bootstrap. Invoked by the cloud-init shim.
# Required env vars (set by the shim):
#   SCYLLA_VOLUME_DEVICE  — /dev/disk/by-id/... path of the Hetzner volume
#   SCYLLA_PRIVATE_IP     — this VM's private network IP
#   APP_PRIVATE_IP        — peer app VM's private IP
#   REPO_URL, REPO_REF    — informational; repo is already cloned at /opt/ffmt

set -euo pipefail
exec > >(tee -a /var/log/ffmt-bootstrap.log) 2>&1

cd /opt/ffmt
source scripts/bootstrap/lib/common.sh
log_info "=== scylla.sh start ==="

# 1. Volume prep
wait_for_volume_device "$SCYLLA_VOLUME_DEVICE" 60
if ! blkid "$SCYLLA_VOLUME_DEVICE" >/dev/null 2>&1; then
    log_info "Formatting $SCYLLA_VOLUME_DEVICE as ext4..."
    mkfs.ext4 -L scylla-data "$SCYLLA_VOLUME_DEVICE"
fi
ensure_fstab "$SCYLLA_VOLUME_DEVICE  /mnt/scylla-data  ext4  defaults,nofail,discard  0 0"
mkdir -p /mnt/scylla-data/{data,commitlog,saved_caches,log}
mount -a

# 2. Render env (only fields Scylla compose interpolates)
render_env_file env .env

# 3. Bring up Scylla
log_info "Starting Scylla container..."
docker compose \
    -f docker-compose.yml \
    -f docker-compose.scylla-vm.yml \
    --profile scylla up -d --build

# 4. Wait for CQL
wait_for_tcp 127.0.0.1 9042 600
# Schema/keyspace creation runs from the container's run_entrypoints.sh
# (docker/scylla/startup_scripts/*) — no extra step needed here.

# 5. Install backup cron
ensure_cron "0 3 * * * FFMT_REPO=/opt/ffmt /opt/ffmt/scripts/cron/backup_scylla.sh >> /var/log/ffmt-cron.log 2>&1"

# 6. Sentinel
mkdir -p /var/lib/ffmt
touch /var/lib/ffmt/.scylla-bootstrap-done
log_info "=== scylla.sh done ==="
