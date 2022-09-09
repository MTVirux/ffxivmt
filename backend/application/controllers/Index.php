<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Index extends CI_Controller{

    function __construct(){
        parent::__construct();
        $this->load->helper('ffxiv_worlds');
        $this->load->model('Redis/redis_info_model', 'redis');
    }

    public function get_current_timestamp(){
        $time = time();
        echo $time;
        return $time;
    }

    public function number_of_redis_keys(){
        $results = $this->redis->key_count();
        $data['db'] = $results;
        $total_count = 0;
        foreach($results as $result){
            $total_count += $result;
        }
        $data['total'] = $total_count;
        pretty_dump($data);
        
    }

}