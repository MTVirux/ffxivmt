#!/bin/bash

chmod +x "/usr/local/bin/acme.sh"
bash /usr/local/bin/acme.sh --register-account -m ${ZERO_SSL_USER_EMAIL}
bash /usr/local/bin/acme.sh --issue --domain ${ZERO_SSL_MAIN_DOMAIN} --webroot /var/www/html --debug >> /zero_ssl_debug.log