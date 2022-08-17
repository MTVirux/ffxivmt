import redis
import config

DB_LISTINGS = redis.Redis(host=config.REDIS_HOST, port=config.REDIS_PORT, db=config.REDIS_LISTINGS_DB)
DB_LISTINGS.client_setname("LISTINGS_CLIENT")
DB_SALES = redis.Redis(host=config.REDIS_HOST, port=config.REDIS_PORT, db=config.REDIS_SALES_DB)
DB_SALES.client_setname("SALES_CLIENT")
DB_RECENT = redis.Redis(host=config.REDIS_HOST, port=config.REDIS_PORT, db=config.REDIS_RECENT_DB)
DB_RECENT.client_setname("RECENT_CLIENT")