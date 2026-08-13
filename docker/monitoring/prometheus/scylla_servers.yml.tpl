# Rendered by bootstrap via envsubst. ScyllaDB's bundled dashboards filter on the
# dc/cluster labels; do not remove them.
- targets:
    - ${SCYLLA_PRIVATE_IP}:9180
  labels:
    dc: ffmt
    cluster: ffmt_cluster
