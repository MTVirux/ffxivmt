#!/bin/bash

docker container rm -f ffmt_backend
docker image rm -f $(docker image ls -q)
docker-compose --compatibility up -d ffmt_backend
