rm -rf ./docker/scylla/persistent
rm -rf ./backend/application/logs/*.log
rm -rf ./docker/ws_worker/server/logs/
rm -rf ./docker/ws_worker/sales_importer/logs/

echo "Spinning up Scylla..."
#docker-compose up -d ffmt_scylla
./prep_scylla_clusters.sh

echo "Spinning up PHP backend..."
docker-compose up -d ffmt_backend

docker-compose up -d ffmt_elastic

#Wait for backend to be ready
while ! docker exec ffmt_backend test -f "/.ffmt_backend_ready"; do
echo -en "\r$(date) - Waiting for file '/.ffmt_backend_ready' to exist inside the container..." 
sleep 1
done

echo "Updating Item DB (Log Channel SCYLLADB)..."
curl -X POST localhost/updatedb/ > /dev/null

echo "Spinning up WS Worker..."
docker-compose up -d ffmt_ws_worker
