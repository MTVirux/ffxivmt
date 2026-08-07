#!/bin/bash
set -euo pipefail

CONTAINER="${SCYLLA_CONTAINER:-ffmt_scylla_node}"

docker exec "$CONTAINER" bash /usr/local/bin/make_backup.sh
