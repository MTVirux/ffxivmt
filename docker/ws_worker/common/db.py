import os
from cassandra.cluster import Cluster, Session
from cassandra.query import PreparedStatement

_SCYLLA_HOSTS = [os.environ.get("SCYLLADB_HOST", "10.0.0.3")]
_KEYSPACE = os.environ.get("SCYLLADB_KEYSPACE", "ffmt")

_cluster: Cluster | None = None
_session: Session | None = None
_prepared_cache: dict[str, PreparedStatement] = {}


def get_session() -> Session:
    global _cluster, _session
    if _session is None:
        _cluster = Cluster(_SCYLLA_HOSTS)
        _session = _cluster.connect(_KEYSPACE)
    return _session


def prepare_cached(cql: str) -> PreparedStatement:
    stmt = _prepared_cache.get(cql)
    if stmt is None:
        stmt = get_session().prepare(cql)
        _prepared_cache[cql] = stmt
    return stmt


def shutdown() -> None:
    global _cluster, _session
    if _cluster is not None:
        _cluster.shutdown()
    _cluster = None
    _session = None
    _prepared_cache.clear()
