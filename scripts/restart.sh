#!/bin/bash

docker-compose down
docker network create ffmt_internal
docker network create ffmt_external
docker-compose build
docker-compose up -d
