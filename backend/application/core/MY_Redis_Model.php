<?php
defined('BASEPATH') OR exit('No direct script access allowed');
require APPPATH.'vendor/predis/predis/autoload.php';

class MY_Redis_Model extends CI_Model{

    protected $redis;

    public function __construct() {
        parent::__construct();
        $this->load->config('redis');
        Predis\Autoloader::register();
        $this->redis = new Predis\Client([
            'scheme' => 'tcp',
            'host'   => 'ffmt_redis',
            'port'   => 6379,
        ]);
        if($this->redis->ping()){
            return $this->redis;
        }else{
            $this->redis = false;
            return $this->redis;
        }
    }
}

