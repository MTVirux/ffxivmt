#! /bin/bash

for f in /startup_scripts/*.sh; do
  chmod +x "$f"
  echo Running "$f"
  bash "$f" 
done

touch /.ffmt_backend_ready

#sleep forver
while true; do sleep 1000; done