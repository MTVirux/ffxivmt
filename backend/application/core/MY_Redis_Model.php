<?php
defined('BASEPATH') OR exit('No direct script access allowed');
include_once APPPATH.'vendor/predis/predis/autoload.php';

class MY_Redis_Model extends CI_Model{

    protected $redis;

    public function __construct() {
        parent::__construct();
        $this->load->config('redis');

        $redis_hosts = $this->config->item('redis_hosts');
        
        Predis\Autoloader::register();
        $this->redis = new Predis\Client([
            'scheme' => $redis_hosts['ffmt_redis']['scheme'],
            'host'   => $redis_hosts['ffmt_redis']['host'],
            'port'   => $redis_hosts['ffmt_redis']['port']
        ]);
        if($this->redis->ping()){
            return $this->redis;
        }else{
            $this->redis = false;
            return $this->redis;
        }
    }
}

