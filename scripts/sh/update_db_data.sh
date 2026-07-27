#!/bin/bash
#
# Triggers the full DB rebuild via the Ffmt.Cli CLI inside the backend container.

set -euo pipefail

CONTAINER="${BACKEND_CONTAINER_NAME:-ffmt_backend}"

echo "Running 'ffmt updatedb' inside container ${CONTAINER}..."
docker exec "${CONTAINER}" ffmt updatedb
echo "Done."
