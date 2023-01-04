#!/bin/bash

docker container rm -f ffmt_scylla
rm -rf ./docker/scylla/persistent
docker image rm -f ffmt_scylla
docker-compose up -d ffmt_scylla
