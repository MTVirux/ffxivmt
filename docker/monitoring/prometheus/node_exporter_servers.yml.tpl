# Rendered by bootstrap via envsubst. Both VMs run node_exporter bound to the
# private IP only.
- targets:
    - ${APP_PRIVATE_IP}:9100
  labels:
    role: app
- targets:
    - ${SCYLLA_PRIVATE_IP}:9100
  labels:
    role: scylla
