#!/bin/bash
# Dev variant of run_server.sh — wraps server.py in `watchfiles` so the consumer restarts on
# .py edits to /server, /sales_importer/python, or /ws_worker/common. WATCHFILES_FORCE_POLLING
# is required for Windows host bind-mounts where inotify events don't propagate.
export PYTHONPATH=/ws_worker:${PYTHONPATH}
export WATCHFILES_FORCE_POLLING=true

while true; do
    /Python-3.10.5/python -m watchfiles \
        --filter python \
        "/Python-3.10.5/python /server/server.py" \
        /server /sales_importer/python /ws_worker/common
    sleep 1
done
