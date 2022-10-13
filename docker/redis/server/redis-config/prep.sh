mkdir -p ${REDIS_DATA_DIR}  # Create data directory if it doesn't exist
redis-cli config set dir ${REDIS_DATA_DIR} # Set data directory
redis-cli config set dbfilename dump.rdb #Set the name of the dump file to current timestamp
redis-cli config set save "" #Disable automatic saving
redis-cli config set dir /server/redis-config #Set the config file to the one in the repo
redis-cli select 0
redis-cli set index ${REDIS_INDEX_DB} #Set the index database to the value of REDIS_INDEX_DB
redis-cli set sales ${REDIS_SALES_DB} #Set the sales database to the value of REDIS_SALES_DB
redis-cli set timeseries ${REDIS_TIMESERIES_DB} #Set the timeseries database to the value of REDIS_TIMESERIES_DB
redis-cli select ${REDIS_TIMESERIES_DB} #Select the timeseries database