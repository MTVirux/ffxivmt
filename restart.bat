@echo off
docker compose down
docker image rm ffxivmerchanttools_ffxiv_app
docker network create ffmt_internal
docker network create ffmt_external
docker compose build
docker compose up -d
exit
