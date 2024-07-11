rm -rf ./docker/scylla/persistent
rm -rf ./backend/application/logs/*.log
rm -rf ./docker/ws_worker/server/logs/
rm -rf ./docker/ws_worker/sales_importer/logs/

echo "Spinning up Scylla..."
#Use if locally hosting scylla
docker-compose up -d ffmt_scylla_node
while ! docker exec ffmt_scylla_node test -f "/.ffmt_scylla_ready"; do
echo -en "\r$(date) - Waiting for file '/.ffmt_scylla_ready' to exist inside the container..." 
sleep 1
done

#Use if hosting remote nodes
#./prep_scylla_clusters.sh

echo "Spinning up PHP backend..."
docker-compose up -d ffmt_backend

docker-compose up -d ffmt_elastic

#Wait for backend to be ready
while ! docker exec ffmt_backend test -f "/.ffmt_backend_ready"; do
echo -en "\r$(date) - Waiting for file '/.ffmt_backend_ready' to exist inside the container..." 
sleep 1
done

chmod +x ./update_db_data.sh
./update_db_data.sh



#echo "Spinning up WS Worker..."
docker-compose up -d ffmt_ws_worker
