<?php
defined ('BASEPATH') OR exit('No direct script access allowed');
//require APPPATH.'core/MY_Redis_Model.php';


class Redis_listings_model extends MY_Redis_model{
    
    function __construct() {
        parent::__construct();
        $this->redis->select($this->config->item('redis_listings_db'));
    }
    
    //NOT SURE IF WORKING
    function get_listings($item_id){
        $item_id = 33299;
        $keys = $this->redis->keys('*_'.$item_id);
        foreach($keys as $key){
            $world = explode('_', $key)[0];
            $item_id = explode('_', $key)[1];
            $listings[$key][] = json_decode($this->redis->executeRaw(['JSON.GETALL', $key]));
        }

        pretty_dump($listings);die();
    }
}