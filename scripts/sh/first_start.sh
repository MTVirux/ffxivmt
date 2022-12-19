echo "Spinning up Scylla..."
docker-compose up -d ffmt_scylla

echo "Spinning up MariaDB..."
docker-compose up -d ffmt_mariadb

echo "Import MariaDB table backups..."
docker exec -it ffmt_mariadb /bin/bash import_maria_db.sh

echo "Spinning up PHP backend..."
docker-compose up -d ffmt_backend

echo "Spinning up WS Worker..."
docker-compose up -d ffmt_scylla

echo "Spinning up PMA..."
docker-compose up -d ffmt_pma

echo "Updating Item DB from CSV..."
curl -X POST localhost/updatedb/ > /dev/null

echo "Updating item sales from universalis..."
curl -X POST localhost/updatedb/update_sales_from_universalis > /dev/null

echo "Transposing sales to timeseries..."
curl -X POST localhost/test/transpose_sales_to_ts > /dev/null

echo "Updating item scores..."
curl -X POST localhost/test/update_item_scores > /dev/null