echo "Creating external volumes..."
docker volume create ffmt_mariadb_data
##docker volume create ffmt_redis_data #UNUSED AT THE TIME OF MAKING
##docker volume create ffmt_redisinsight_data #UNUSED AT THE TIME OF MAKING

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
rm /root/ffxiv-market-tools/docker/redis/server/persistent_data/*.rdb
rm /root/ffxiv-market-tools/docker/redis/server/persistent_data/aofdir/*.rdb
rm /root/ffxiv-market-tools/docker/redis/server/persistent_data/aofdir/*.aof
rm /root/ffxiv-market-tools/docker/redis/server/persistent_data/aofdir/*.manifest

echo "Spinning up Redis..."
docker-compose up -d ffmt_redis

#Wait until redis is up and done loading
echo "Waiting for Redis to wake up..."
while ! docker exec -it ffmt_redis redis-cli ping | grep -q 'PONG'; do
  sleep 1
done

echo "Updating Item DB from CSV..."
curl -X POST localhost/updatedb/ > /dev/null

echo "Updating item sales from universalis..."
curl -X POST localhost/updatedb/update_sales_from_universalis > /dev/null

echo "Transposing sales to timeseries..."
curl -X POST localhost/test/transpose_sales_to_ts > /dev/null

echo "Updating item scores..."
curl -X POST localhost/test/update_item_scores > /dev/null