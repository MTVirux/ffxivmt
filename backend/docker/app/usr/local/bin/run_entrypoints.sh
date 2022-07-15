#! /bin/bash

for f in /startup_scripts/*.sh; do
  bash "$f" 
done

#sleep forver
while true; do sleep 1000; done