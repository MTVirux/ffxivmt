#!/bin/bash

echo "Press 1 for Linux"
echo "Press 2 for Windows"
echo "Press 3 for PowerShell"
read choice

if [ $choice -eq 1 ]; then
  cp ./scripts/sh/* .
elif [ $choice -eq 2 ]; then
  cp ./scripts/bat/* .
elif [ $choice -eq 3 ]; then
  cp ./scripts/ps/* .
else
  echo "Invalid selection"
fi