#!/bin/bash
#
# .NET-era replacement for update_db_data.sh.
#
# Triggers the full DB rebuild via the Ffmt.Cli CLI inside the dotnet backend container.
# No temp-key handshake, no secrets.php write/clear — auth disappears with the controller.
#
# During cutover week both this script and the PHP-era update_db_data.sh live side-by-side
# so a rollback can use the prior form without restoring it from git history.

set -euo pipefail

CONTAINER="${BACKEND_CONTAINER_NAME:-ffmt_backend}"

echo "Running 'ffmt updatedb' inside container ${CONTAINER}..."
docker exec "${CONTAINER}" ffmt updatedb
echo "Done."
