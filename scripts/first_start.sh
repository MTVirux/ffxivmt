docker-compose up -d ffmt_backend
docker-compose up -d ffmt_mariadb
docker-compose up -d ffmt_pma
echo "Updating MariaDB items table..."
curl -X POST mtvirux.app/updatedb/
docker exec -it ffmt_mariadb /bin/bash import_maria_db.sh
docker-compose up -d ffmt_redis
echo "Updating item sales from universalis..."
curl -X POST mtvirux.app/updatedb/update_sales_from_universalis
echo "Transposing sales to timeseries..."
curl -X POST mtvirux.app/test/transpose_sales_to_ts
echo "Updating item scores..."
curl -X POST mtvirux.app/test/update_item_scores
echo "DB fully refreshed!"
docker-compose up -d ffmt_portainer
