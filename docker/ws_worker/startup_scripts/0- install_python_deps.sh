#!/bin/bash
# Install Python dependencies declared in /ws_worker/requirements.txt.
# Idempotent: pip is a no-op if everything is already present.
set -e
/Python-3.10.5/python -m pip install --no-warn-script-location -q -r /ws_worker/requirements.txt
echo "ws_worker python deps installed"
