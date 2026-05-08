<?php
defined('BASEPATH') OR exit('No direct script access allowed');


/**
 * 
 *  REDIS CONNECTION CONFIGURATION
 * 
 */
$config['redis_hosts'] = array(
    'ffmt_redis'=> array(
                        'scheme'    =>  'tcp',
                        'host'      =>  'ffmt_redis',
                        'port'      =>  6379,
                        )
);
$config['redis_port'] = 6379;
$config['redis_timeout'] = 0;


/**
 * 
 *      DATABASE INDEXES
 * 
 */
$config['redis_index_db'] = 0;
$config['redis_sales_db'] = 1;
$config['redis_timeseries_db'] = 2;
$config['redis_listings_db'] = 3;
