<?php
defined('BASEPATH') OR exit('No direct script access allowed');
include_once APPPATH.'core/MY_Redis_Model.php';


class Redis_sales_model extends MY_Redis_Model{
    
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
        return $results;
    }

    public function get_sales_entries(){

        $keys = $this->redis->keys('Spriggan_*');
        $from_time = time() - (60 * 60 * 24);
        $to_time = time();
        $final = [];

        foreach($keys as $key){
            
            $results = json_decode($this->redis->executeRaw(['JSON.GET', $key]));
            foreach($results as $result){
                if($result->timestamp > $from_time && $result->timestamp < $to_time){
                    if(!array_key_exists($key, $final)){
                        $final[$key] = 0;
                    }
                        $final[$key] = $final[$key] + 1;    
                }
            }

        }
        pretty_dump($final);
    }

    public function get_sales_volumes(){

        $keys = $this->redis->keys('Spriggan_*');
        $from_time = time() - (60 * 60 * 24);
        $to_time = time();
        $final = [];

        foreach($keys as $key){
            
            $results = json_decode($this->redis->executeRaw(['JSON.GET', $key]));
            foreach($results as $result){
                    if(!array_key_exists($key, $final)){
                        $final[$key] = 0;
                    }
                    
                    $final[$key] = $final[$key] + $result->quantity;    
            }
        }

    pretty_dump($final);

    }

    public function add_sale($sale){
        $sales_array = [];
        if(gettype($sale) == 'array'){
            $sales_array = $sale;
        }else{
            $sales_array[] = $sale;
        }

        $fulfilled_updates = 0;

        logger("REDIS_SALES", "Parsing sales array (".count($sales_array)." entries)");

        foreach($sales_array as $sale_data){
            $hash = $sale_data["worldName"] . "_" . $sale_data["itemID"];
            $key = $sale_data["buyerName"] . "_" . $sale_data["timestamp"];;

            $sale_data_to_insert = [
                "buyerName" => $sale_data["buyerName"],
                "hq" => $sale_data["hq"],
                "onMannequin" => $sale_data["onMannequin"],
                "quantity" => $sale_data["quantity"],
                "timestamp" => $sale_data["timestamp"],
                "total" => $sale_data["total"],
                "worldID" => $sale_data["worldID"],
                "worldName" => $sale_data["worldName"],
                "itemID" => $sale_data["itemID"],
            ];

            //Get hash
            $current_json = json_decode($this->redis->executeRaw(['JSON.GET', $hash]));
            
            //if current json is null, create new object
            if($current_json == null){
                $current_json = new stdClass();
            }
            
            $current_json->$key = $sale_data_to_insert;

            if($current_json == null){
                $current_json = json_encode($sale_data_to_insert);
            }
            
            $transaction_status = $this->redis->executeRaw(['JSON.SET', $hash, $key, json_encode($sale_data_to_insert)]);

            if(is_null($transaction_status)){
                logger('REDIS_SALES', "Error inserting sale into redis: " . $transaction_status);
            }else{

                //Add to fulfilled updates
                $fulfilled_updates = $fulfilled_updates + 1;
                //Percentage of updated sales
                $percentage_of_fulfilled_updates = round(($fulfilled_updates / count($sales_array)) * 100, 2);
                logger('REDIS_SALES', "Updated sales for hash(". $fulfilled_updates . " / " . count($sales_array) . " - " . $percentage_of_fulfilled_updates . "%): " . $hash);

            }
        }
        
    }
}