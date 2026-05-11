# Rendered by bootstrap via envsubst. The dc/cluster labels are required
# by ScyllaDB's bundled dashboards; do not remove them.
- targets:
    - ${SCYLLA_PRIVATE_IP}:9180
  labels:
    dc: ffmt
    cluster: ffmt_cluster
