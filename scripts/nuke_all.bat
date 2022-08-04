@echo off

docker compose down

docker images -a -q | % { docker image rm $_ -f }

docker volume rm ffmt_mariadb_data
docker volume rm ffmt_redis_data
docker volume rm ffmt_redisinsight_data

docker volume create ffmt_mariadb_data
docker volume create ffmt_redis_data
docker volume create ffmt_redisinsight_data