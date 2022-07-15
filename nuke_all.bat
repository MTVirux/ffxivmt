@echo off

docker compose down

docker image rm ffmt_backend
docker image rm mariadb

docker volume rm ffmt_mariadb_data
docker volume rm ffmt_redis_data
docker volume rm ffmt_redisinsight_data

docker volume create ffmt_mariadb_data
docker volume create ffmt_redis_data
docker volume create ffmt_redisinsight_data