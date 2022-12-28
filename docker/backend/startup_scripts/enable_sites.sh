#! /bin/bash

a2ensite mtvirux_app.conf
a2ensite mtvirux_app_ssl.conf
systemctl restart apache2