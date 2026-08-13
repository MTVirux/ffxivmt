#!/bin/bash
# Scylla VM bootstrap, invoked by the cloud-init shim. Runs under `set -u`, so
# the shim must export all of: SCYLLA_VOLUME_DEVICE (the /dev/disk/by-id path of
# the Hetzner volume), SCYLLA_PRIVATE_IP, APP_PRIVATE_IP.

set -euo pipefail
exec > >(tee -a /var/log/ffmt-bootstrap.log) 2>&1

cd /opt/ffmt
source scripts/bootstrap/lib/common.sh
log_info "=== scylla.sh start ==="

# Every `docker compose` below (and any the operator runs in this shell) picks
# the scylla-VM file set up from here.
export COMPOSE_FILE=docker-compose.yml:docker-compose.scylla-vm.yml
export COMPOSE_PROFILES=scylla,host_metrics

wait_for_volume_device "$SCYLLA_VOLUME_DEVICE" 60
# Only format when the device carries no filesystem, so a re-run keeps the data.
if ! blkid "$SCYLLA_VOLUME_DEVICE" >/dev/null 2>&1; then
    log_info "Formatting $SCYLLA_VOLUME_DEVICE as ext4..."
    mkfs.ext4 -L scylla-data "$SCYLLA_VOLUME_DEVICE"
fi
ensure_fstab "$SCYLLA_VOLUME_DEVICE  /mnt/scylla-data  ext4  defaults,nofail,discard  0 0"
mkdir -p /mnt/scylla-data/{data,commitlog,saved_caches,log,backup}
mount -a

export HOST_PRIVATE_IP="$SCYLLA_PRIVATE_IP"
render_env_file env .env

log_info "Starting Scylla container..."
docker compose up -d --build ffmt_scylla_node ffmt_node_exporter

wait_for_tcp "${SCYLLA_PRIVATE_IP}" 9042 600
# Keyspace and tables come from the container's run_entrypoints.sh over the
# bind-mounted scripts/cql/schema/*.cql, so there is no schema step here.

ensure_cron "0 3 * * * FFMT_REPO=/opt/ffmt /opt/ffmt/scripts/cron/backup_scylla.sh >> /var/log/ffmt-cron.log 2>&1"

mkdir -p /var/lib/ffmt
touch /var/lib/ffmt/.scylla-bootstrap-done
log_info "=== scylla.sh done ==="
