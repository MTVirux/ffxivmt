@echo off

docker compose down

for /f "tokens=*" %%i in ('docker images -q') do (
    docker rmi %%i
)

docker volume rm ffmt_mariadb_data
docker volume rm ffmt_redis_data

docker volume create ffmt_mariadb_data
docker volume create ffmt_redis_data
