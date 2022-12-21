#!/bin/bash

docker container rm -f ffmt_redis
docker image rm -f $(docker image ls -q)
docker-compose up -d ffmt_redis
