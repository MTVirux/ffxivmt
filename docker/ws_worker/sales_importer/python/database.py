from cassandra.cluster import Cluster

SCYLLA_DB_CLUSTER = Cluster(['10.0.0.3'])
SCYLLA_DB = SCYLLA_DB_CLUSTER.connect('ffmt')
