@echo off

docker container rm -f ffmt_mariadb
docker image rm mariadb
docker volume rm ffmt_mariadb_data
docker volume create ffmt_mariadb_data
docker compose up -d ffmt_mariadb