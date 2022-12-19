@echo off

docker compose down

for /f "tokens=*" %%i in ('docker container ls -qa') do (
    docker container rm -f %%i
)

for /f "tokens=*" %%i in ('docker images ls -qa') do (
    docker rmi %%i
)