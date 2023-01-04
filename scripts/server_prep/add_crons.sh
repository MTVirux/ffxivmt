#!/bin/bash

# Navigate to cron directory
cd "$(dirname "$0")"/../cron

# Loop through all shell script files in cron directory
for script in *.sh; do
  # Check if there is already a task for the script in the current user's crontab
  if ! crontab -l | grep -Fxq "$script"; then
    # If there is not a task, add a daily task to the current user's crontab
    (crontab -l; echo "0 0 * * * /bin/bash $(pwd)/$script") | crontab -
    echo "Added daily task for $script to current user's crontab."
  else
    echo "Task for $script already exists in current user's crontab."
  fi
done
