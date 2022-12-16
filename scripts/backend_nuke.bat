#!/bin/bash

docker container rm -f ffmt_backend
for /f "tokens=*" %%i in ('docker images -q') do (
    docker rmi %%i
)
docker-compose --compatibility up -d ffmt_backend
