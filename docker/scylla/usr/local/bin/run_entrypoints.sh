#! /bin/bash
#sleep forver

mkdir -p /startup_scripts
touch /init.log
chmod 777 /init.log

exec /docker-entrypoint.py "$@" &

CQL = "SELECT NOW() FROM system.local;"
until cqlsh -e "$CQL"; do
  echo "Unavailable: sleeping at $(date)"
  sleep 1
done

for f in /startup_scripts/*.sh; do
  echo "Running script: $f" >> /init.log
  bash -x "$f" >> /init.log
done


#sleep forver
while true; do sleep 1000; done