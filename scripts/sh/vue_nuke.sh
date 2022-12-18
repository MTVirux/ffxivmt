#!/bin/bash

docker container rm -f ffmt_vue
docker image rm -f $(docker image ls -q)
docker volume rm $(docker volume ls -q)
docker-compose up -d ffmt_vue
