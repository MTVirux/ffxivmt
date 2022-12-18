@echo off

docker container rm -f ffmt_redis
for /f "tokens=*" %%i in ('docker images -q') do (
    docker rmi %%i
)
docker-compose up -d ffmt_redis
