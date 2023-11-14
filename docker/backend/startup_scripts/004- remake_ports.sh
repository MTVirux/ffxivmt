#!/bin/sh

rm /etc/apache2/ports.conf
touch /etc/apache2/ports.conf

echo "Listen ${BACKEND_DOCKER_PORT}"        >>  /etc/apache2/ports.conf
echo "Listen ${GRAFANA_DOCKER_PORT}"        >>  /etc/apache2/ports.conf
echo "Listen ${SCYLLA_DOCKER_PORT}"         >>  /etc/apache2/ports.conf

echo "<IfModule ssl_module>"                >>  /etc/apache2/ports.conf
echo "Listen ${BACKEND_DOCKER_SSL_PORT}"    >>  /etc/apache2/ports.conf
echo "Listen ${GRAFANA_DOCKER_SSL_PORT}"    >>  /etc/apache2/ports.conf
echo "Listen ${SCYLLA_DOCKER_SSL_PORT}"     >>  /etc/apache2/ports.conf
echo "</IfModule>"                          >>  /etc/apache2/ports.conf

service apache2 restart