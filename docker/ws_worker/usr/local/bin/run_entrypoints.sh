#! /bin/bash

for f in /startup_scripts/*.sh; do
  chmod +x "$f"
  bash "$f" 
done

#sleep forver
while true; do sleep 1000; done