#!/bin/bash

docker-compose down

docker volume rm $(docker volume ls -q)

docker rmi -f $(docker images -aq)

docker volume rm ffmt_mariadb_data
docker volume rm ffmt_redis_data
docker volume rm ffmt_redisinsight_data

docker volume create ffmt_mariadb_data
docker volume create ffmt_redis_data
docker volume create ffmt_redisinsight_data