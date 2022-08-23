#! /bin/bash

# watch files in /server/logs/status

watch -n 0.1 -t tail -n 1 "/server/logs/status/sales*" "/server/logs/status/list*" "/server/logs/status/red*"