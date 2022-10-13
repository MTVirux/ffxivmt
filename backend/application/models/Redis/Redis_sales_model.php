<?php
defined('BASEPATH') OR exit('No direct script access allowed');
require APPPATH.'core/MY_Redis_Model.php';


class Redis_sales_model extends MY_Redis_model{
    
    function __construct() {
        parent::__construct();
        $this->load->helper('ffxiv_worlds');
        $this->redis->select($this->config->item('redis_sales_db'));
    }

    function search_buyer($buyer_name){
        $this->load->model('Item_model', 'Item');
        $keys = $this->redis->keys('*');
        $results = [];
        $current_key = 0;
        $total_keys = count($keys);

        foreach($keys as $key){
            $current_key++;
            logger('SEARCH_BUYER', "Searching buyer ".$buyer_name." on key " . $current_key . " of " . $total_keys);
            $item_id = explode('_', $key)[1];
            $sales = json_decode($this->redis->executeRaw(['JSON.GET', $key]));
            foreach($sales as $hash => $sale){
                if(strpos($sale->buyerName, $buyer_name) !== false){
                    $sale->itemId = $item_id;
                    $sale->itemName = $this->Item->get_item_name($sale->itemId);
                    $sale->date = date('Y-m-d H:i:s', $sale->timestamp);
                    unset($sale->onMannequin);
                    unset($sale->timestamp);
                    unset($sale->worldID);
                    $results[] = $sale;
                }
            }
        }
        pretty_dump("Results:");
        pretty_dump($results);
    }
}