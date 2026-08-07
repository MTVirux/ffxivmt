#!/bin/bash

exec /docker-entrypoint.py "$@" &

until cqlsh -e "SELECT NOW() FROM system.local;"; do
  echo "Unavailable: sleeping at $(date)"
  sleep 1
done

# /startup_scripts is bind-mounted from scripts/cql/schema - idempotent DDL only.
for f in /startup_scripts/*.cql; do
  echo "Running script: $f" >> /init.log
  cqlsh -f "$f" >> /init.log
done

while true; do sleep 1000; done
