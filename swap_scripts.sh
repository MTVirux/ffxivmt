#!/bin/bash

echo "Select what scripts to bring to the current directory:"
echo "[1] - Linux"
echo "[2] - Clean up existing scripts"
echo "[3] - Save scripts"
echo
read -r choice

# Create a temporary directory and move swap_scripts.sh to a safe location
preserve_swapping_script() {
  mkdir -p ./temp
  mv -f swap_scripts.sh ./temp/
}

restore_swap_script() {
  mv -f ./temp/swap_scripts.sh .
  rm -rf ./temp
}

case $choice in
  1)
    preserve_swapping_script
    # Remove existing scripts
    rm -f *.sh *.bat *.ps
    # Copy Linux scripts and set executable permissions
    cp -f ./scripts/sh/* .
    chmod +x ./*.sh
    restore_swap_script
    ;;
  2)
    preserve_swapping_script
    # Remove existing scripts
    rm -f *.sh *.bat *.ps
    echo "All .sh / .bat / .ps1 scripts removed from the current directory..."
    restore_swap_script
    ;;
  3)
    preserve_swapping_script
    # Move scripts to their respective folders
    mv -f *.sh ./scripts/sh/
    mv -f *.ps1 ./scripts/ps1/
    mv -f *.bat ./scripts/bat/
    restore_swap_script
    ;;
  *)
    echo "Invalid selection"
    ;;
esac
