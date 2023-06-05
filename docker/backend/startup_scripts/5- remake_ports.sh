#!/bin/sh

rm /etc/apache2/ports.conf
touch /etc/apache2/ports.conf

echo "Listen ${PRIMARY_DOCKER_PORT}"        >>  /etc/apache2/ports.conf
echo "Listen ${SECONDARY_DOCKER_PORT}"      >>  /etc/apache2/ports.conf
echo "<IfModule ssl_module>"                >>  /etc/apache2/ports.conf
echo "Listen ${PRIMARY_DOCKER_SSL_PORT}"    >>  /etc/apache2/ports.conf
echo "</IfModule>"                          >>  /etc/apache2/ports.conf
echo "<IfModule mod_gnutls.c>"              >>  /etc/apache2/ports.conf
echo "Listen ${SECONDARY_DOCKER_SSL_PORT}"  >>  /etc/apache2/ports.conf
echo "</IfModule>"                          >>  /etc/apache2/ports.conf

service apache2 restart