#!/bin/bash

mkdir -p /etc/apache2/ssl/private

chmod 755 /etc/apache2/ssl
chmod 710 /etc/apache2/ssl/private
chown -R root:root /etc/apache2/ssl/
chown -R root:ssl-cert /etc/apache2/ssl/private/
chmod 644 /etc/apache2/ssl/*.crt
chmod 640 /etc/apache2/ssl/private/*.key
