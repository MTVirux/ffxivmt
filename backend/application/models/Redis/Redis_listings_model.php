<?php
defined('BASEPATH') OR exit('No direct script access allowed');
require APPPATH.'core/MY_Redis_Model.php';


class Redis_listings_model extends MY_Redis_model{
    
    function __construct() {
        parent::__construct();
        $this->load->helper('ffxiv_worlds');
        $this->redis->select($this->config->item('redis_listings_db'));
    }


    public function get_keys($world = null, $item_id = null){
        if(empty($world)){
            $world = '*';
        }

        if(is_null($item_id)){
            $item_id = '*';
        }

        $keys = $this->redis->keys($world.'_'.$item_id);
        
        if($keys){
            return array(
                'success' => 'true', 
                'data' => $keys
            );
        }else{
            return array(
                'success' => 'false', 
                'message' => 'No keys found'
            );
        }
    }

    public function recent_apiGet($world_id = null, $limit = null){
        
        if(is_null($limit)){
            $limit = 10;
        }

        if(is_null($world_id)){
            $world_name = 'listings';
        }else if(is_numeric($world_id)){
            $world_name = get_world_name($world_id);
        }else{
            $world_name = $world_id;
        }

        $list_name = 'recent_'. $world_name;

        $recent_items = $this->redis->lrange($list_name, 0, $limit);

        var_dump($list_name, $world_name, $limit);die();

        if($recent_items){
            return array(
                'success' => 'true',
                'data' => $recent_items
            );
        }else{
            return array(
                'success' => 'false',
                'message' => 'No recent items found'
            );
        }

    }
}