#!/bin/bash

docker container rm -f ffmt_scylla
docker image rm -f $(docker image ls -q)
docker-compose up -d ffmt_scylla
