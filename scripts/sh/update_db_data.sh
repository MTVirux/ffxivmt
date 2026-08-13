#!/bin/bash
set -euo pipefail

CONTAINER="${BACKEND_CONTAINER_NAME:-ffmt_backend}"

echo "Running 'ffmt updatedb' inside container ${CONTAINER}..."
docker exec "${CONTAINER}" ffmt updatedb
echo "Done."
