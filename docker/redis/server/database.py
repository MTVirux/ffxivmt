import redis
import config

DB_LISTING = redis.Redis(host=config.REDIS_HOST, port=config.REDIS_PORT, db=config.REDIS_LISTINGS_DB)
DB_LISTING.client_setname("LISTINGS_CLIENT")
DB_SALES = redis.Redis(host=config.REDIS_HOST, port=config.REDIS_PORT, db=config.REDIS_SALES_DB)
DB_SALES.client_setname("SALES_CLIENT")
DB_LISTINGS_CLEAN = redis.Redis(host=config.REDIS_HOST, port=config.REDIS_PORT, db=config.REDIS_LISTINGS_CLEANING_DB)
DB_LISTINGS_CLEAN.client_setname("LISTINGS_CLEAN_CLIENT")
DB_SALES_CLEAN = redis.Redis(host=config.REDIS_HOST, port=config.REDIS_PORT, db=config.REDIS_SALES_CLEANING_DB)
DB_SALES_CLEAN.client_setname("SALES_CLEAN_CLIENT")