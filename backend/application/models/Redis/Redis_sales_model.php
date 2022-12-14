<?php
defined('BASEPATH') OR exit('No direct script access allowed');
include_once APPPATH.'core/MY_Redis_Model.php';


class Redis_sales_model extends MY_Redis_Model{
    
    function __construct() {
        parent::__construct();
        $this->load->helper('ffxiv_worlds');
        $this->redis->select($this->config->item('redis_sales_db'));
    }

    function search_buyer($buyer_name, $world = 'all'){
        $this->load->model('Item_model', 'Item');
        if($world == 'all'){
            $world='*';
        }
        $keys = $this->redis->keys($world.'_*');
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
        ini_set('memory_limit', '2048M');
        //Make whatever var we get into an array
        $sales_array = [];
        if(gettype($sale) == 'array'){
            $sales_array = $sale;
        }else{
            $sales_array[] = $sale;
        }

        //Init metric vars
        $fulfilled_inserts = 0;
        $parsed_sales = 0;

        //Init array of objects to be inserted into the redis db
        $sales_final_object = array();

        //Foreach of the sales info, parse it
        //and add it to the sales_final_object
        if(count($sales_array) > 0){
            logger("REDIS_SALES", "Parsing sales for hash:" . $hash );
        }

        foreach($sales_array as $sale_data){
            $parsed_sales++;
            $hash = $sale_data["worldName"] . "_" . $sale_data["itemID"];
            $key = $sale_data["buyerName"] . "_" . $sale_data["timestamp"];
            //logger("REDIS_SALES", "Parsing sale (" . $parsed_sales . " / " . count($sales_array) . ") with hash: " . $hash );

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

            if(!array_key_exists($hash, $sales_final_object)){
                $sales_final_object[$hash] = new stdClass();
            }
            $sales_final_object[$hash]->$key = $sale_data_to_insert;
        }


        foreach($sales_final_object as $hash => $hash_sales_data){
        
            //Get current json data from Redis DB
            $current_json = json_decode($this->redis->executeRaw(['JSON.GET', $hash]));

            //If there was no data in Redis DB, create new object
            if($current_json == null){
                $current_json = new stdClass();
            }
            
            //Add hash sales data to current json data
            foreach($hash_sales_data as $key => $value){
                $current_json->$key = $value;
            }
            
            $transaction_status = $this->redis->executeRaw(['JSON.SET', $hash, "$", json_encode($current_json)]);

            if($transaction_status != "OK"){
                logger('REDIS_SALES', "Error inserting sale into redis: " . $transaction_status);
                die();
            }else{
                //Add to fulfilled updates
                $fulfilled_inserts++;
                //Percentage of updated sales
                $percentage_of_fulfilled_updates = round(($fulfilled_inserts / count($sales_final_object)) * 100, 2);
                logger('REDIS_SALES', "Inserted hash data ". $fulfilled_inserts . " of " . count($sales_final_object) . " (" . $percentage_of_fulfilled_updates . "%) for hash:" . $hash);
            }
        }
    }
}