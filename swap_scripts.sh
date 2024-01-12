#!/bin/bash

echo "Select what scripts to bring to current directory:"
echo "[1] - Linux"
echo "[2] - Clean up existing scripts"
echo 
read choice

if [ $choice -eq 1 ] || [ $choice -eq 2 ]; then
  # Move swap_scripts.sh to a safe location
  mkdir -p ./temp
  mv swap_scripts.sh ./temp/

  # Remove existing .sh, .bat, and .ps files from the current directory
  rm -f *.sh
  rm -f *.bat
  rm -f *.ps

  if [ $choice -eq 1 ]; then
    cp ./scripts/sh/* .
    chmod +x ./*.sh
  elif [ $choice -eq 2 ]; then
    echo "All .sh / .bat / .ps1 scripts removed from current directory..."
  fi

  # Move swap_scripts.sh back to the current directory
  mv temp/swap_scripts.sh .
  rm -rf ./temp
else
  echo "Invalid selection"
fi