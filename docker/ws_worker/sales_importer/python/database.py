from cassandra.cluster import Cluster

SCYLLA_DB_CLUSTER = Cluster(['ffmt_scylla_node'])
SCYLLA_DB = SCYLLA_DB_CLUSTER.connect('ffmt')
