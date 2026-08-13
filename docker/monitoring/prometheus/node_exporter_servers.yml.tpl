# Rendered by bootstrap via envsubst. node_exporter binds the private IP only on both VMs.
- targets:
    - ${APP_PRIVATE_IP}:9100
  labels:
    role: app
- targets:
    - ${SCYLLA_PRIVATE_IP}:9100
  labels:
    role: scylla
