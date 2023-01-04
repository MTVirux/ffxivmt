rm -rf ./docker/scylla/persistent

echo "Spinning up Scylla..."
docker-compose up -d ffmt_scylla

echo "Spinning up PHP backend..."
docker-compose up -d ffmt_backend

echo "Updating Item DB from CSV (Log Channel ITEM_DB)..."
curl -X POST localhost/updatedb/ > /dev/null

echo "Spinning up WS Worker..."
docker-compose up -d ffmt_ws_worker
