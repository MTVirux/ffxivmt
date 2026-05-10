#!/bin/bash
set -euo pipefail

CONTAINER="${SCYLLA_CONTAINER:-ffmt_scylla_node}"

docker exec "$CONTAINER" chmod +x /usr/local/bin/make_backup.sh
docker exec "$CONTAINER" /usr/local/bin/make_backup.sh
