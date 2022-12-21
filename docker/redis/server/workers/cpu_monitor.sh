#! /bin/bash


mkdir -p ${REDIS_SERVER_LOGS_DIR}/system
touch ${REDIS_SERVER_LOGS_DIR}/system/cpu.log

while true; do
    TEMP=$(mpstat | tail -n 2)
    echo "$TEMP" > ${REDIS_SERVER_LOGS_DIR}/system/cpu.log
    sleep $(bc -l <<< "${REDIS_STATUS_UPDATER_INTERVAL}")
done