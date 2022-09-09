<?php
defined ('BASEPATH') OR exit('No direct script access allowed');
require APPPATH.'core/MY_Redis_Model.php';


class Redis_info_model extends MY_Redis_model{
    
    function __construct() {
        parent::__construct();
        $this->load->helper('ffxiv_worlds');
        $this->redis->select($this->config->item('redis_index_db'));
    }
    
    function key_count(){
        $keys = $this->redis->executeRaw(['KEYS', '*']);
        $results=[];
        foreach($keys as $key=>$value){
            $this->redis->select((int)$key);
            $current_db_keys = $this->redis->executeRaw(['KEYS', '*']);
            $results[$value] = count($current_db_keys);
        }

        return $results;
    }
}