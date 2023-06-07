#!/bin/bash

docker-compose down

docker container rm -f ffmt_ws_worker
docker container rm -f ffmt_elastic
docker container rm -f ffmt_backend

docker rmi $(docker image ls -q)

docker volume rm -f $(docker volume ls -q)

docker network rm ffmt_network
