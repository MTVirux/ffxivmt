mkdir -p ${REDIS_DATA_DIR}  # Create data directory if it doesn't exist
redis-cli select ${REDIS_LISTINGS_DB}
redis-cli flushdb
redis-cli select 0
redis-cli set index ${REDIS_INDEX_DB} #Set the index database to the value of REDIS_INDEX_DB
redis-cli set sales ${REDIS_SALES_DB} #Set the sales database to the value of REDIS_SALES_DB
redis-cli set listings ${REDIS_LISTINGS_DB} #Set the listings database to the value of REDIS_LISTINGS_DB
redis-cli set timeseries ${REDIS_TIMESERIES_DB} #Set the timeseries database to the value of REDIS_TIMESERIES_DB