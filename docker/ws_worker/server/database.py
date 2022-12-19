from cassandra.cluster import Cluster

SCYLLA_DB_CLUSTER = Cluster(['ffmt_scylla'])
SCYLLA_DB = SCYLLA_DB_CLUSTER.connect('sales')

# Check if the connection is working by executing a simple query
result = SCYLLA_DB.execute("SELECT release_version FROM system.local")
print(result.one())