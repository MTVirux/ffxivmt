#! /bin/bash


i=1
sp="/-\|"
echo -n ' '
until mariadb -u root --password=${MARIADB_ROOT_PASSWORD} -e ";" > /dev/null 2>&1;do 
  printf "\rLoading MariaDB...${sp:i++%${#sp}:1}"
  sleep 0.05
done

printf "\rMariaDB is ready!\n"


printf "Creating ${MARIADB_DATABASE} database\n"
until mariadb -u root --password=${MARIADB_ROOT_PASSWORD} -e "CREATE DATABASE IF NOT EXISTS ${MARIADB_DATABASE};";do
  printf "\rCreating ${MARIADB_DATABASE} database... ${sp:i++%${#sp}:1}"
done
printf "\r${MARIADB_DATABASE} Database Created!\n"


for f in /usr/local/bin/*.sql; do 
  until mariadb -u root --password=${MARIADB_ROOT_PASSWORD} ${MARIADB_DATABASE} < $f &> /dev/null ;do
    printf "\r\rImporting ${f##*/}..."  
  done
  printf "\rDone importing ${f##*/}!"
  printf "\n"

done

printf "\n---Migration Complete!---\n\n"
