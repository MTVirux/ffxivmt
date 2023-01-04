#! /bin/bash

# Give +x permissions to the make_backup.sh script inside ffmt_scylla container
docker exec ffmt_scylla chmod +x /usr/local/bin/make_backup.sh

# Run the backup script in /usr/local/bin inside the ffmt_scylla container
docker exec ffmt_scylla /usr/local/bin/make_backup.sh