#!/bin/bash

echo "$(date +%Y-%m-%d) @ $(date +%H:%M:%S) - Cron task was run to backup and zip logs started" >> /root/logs/cron.log

# Create folders if they don't exist
mkdir -p /root/logs
mkdir -p /root/logs/temp
mkdir -p /root/logs/temp/backend
mkdir -p /root/logs/temp/ws_worker
mkdir -p /root/logs/temp/ws_worker/action
mkdir -p /root/logs/temp/ws_worker/error
mkdir -p /root/logs/temp/ws_worker/debug


# Copy logs to backup folder
echo "$(date +%Y-%m-%d) @ $(date +%H:%M:%S) - Moving logs to /root/logs/temp..." >> /root/logs/cron.log
mv /root/ffxiv-market-tools/backend/application/logs/*.log /root/logs/temp/backend
mv /root/ffxiv-market-tools/docker/ws_worker/server/logs/action/*.log /root/logs/temp/ws_worker/action
mv /root/ffxiv-market-tools/docker/ws_worker/server/logs/error/*.log /root/logs/temp/ws_worker/error
mv /root/ffxiv-market-tools/docker/ws_worker/server/logs/debug/*.log /root/logs/temp/ws_worker/debug

#zip the temp folder into a timestamped zip file
zip -r /root/logs/$(date +%Y-%m-%d_%H-%M-%S).zip /root/logs/temp >> /root/logs/cron.log

#Delete the temp folder
rm -rf /root/logs/temp

#Write hello to cron.log
echo "$(date +%Y-%m-%d) @ $(date +%H:%M:%S) - Cron task was run to backup and zip logs and finished" >> /root/logs/cron.log