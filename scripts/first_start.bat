@echo off

echo "Creating external volumes..."
docker volume create ffmt_mariadb_data

echo "Spinning up PHP backend..."
docker-compose up -d ffmt_backend

echo "Spinning up MariaDB..."
docker-compose up -d ffmt_mariadb

echo "Spinning up PMA..."
docker-compose up -d ffmt_pma

echo "Updating MariaDB items table..."
docker exec -it ffmt_mariadb /bin/bash import_maria_db.sh

echo "Updating MariaDB items table from CSV file..."
curl -X POST localhost/updatedb/

echo "Delete pre-existing Redis data"
del /root/ffxiv-market-tools/docker/redis/server/persistent_data/.rdb
del /root/ffxiv-market-tools/docker/redis/server/persistent_data/aofdir/.rdb
del /root/ffxiv-market-tools/docker/redis/server/persistent_data/aofdir/.aof
del /root/ffxiv-market-tools/docker/redis/server/persistent_data/aofdir/.manifest

echo "Spinning up Redis..."
docker-compose up -d ffmt_redis

echo "Waiting for Redis to wake up..."
:loop
docker exec -it ffmt_redis redis-cli ping | find "PONG"
if %errorlevel% equ 0 (
goto done_waiting
) else (
timeout /t 1 > nul
goto loop
)
:done_waiting

echo "Updating item sales from universalis..."
curl -X POST localhost/updatedb/update_sales_from_universalis > nul

echo "Transposing sales to timeseries..."
curl -X POST localhost/test/transpose_sales_to_ts > nul

echo "Updating item scores..."
curl -X POST localhost/test/update_item_scores > nul

echo "DB fully refreshed!"
docker-compose up -d ffmt_portainer

pause