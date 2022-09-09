<?php
defined('BASEPATH') OR exit('No direct script access allowed');
require APPPATH.'core/MY_Redis_Model.php';


class Redis_timeseries_model extends MY_Redis_model{
    
    function __construct() {
        parent::__construct();
        $this->load->helper('ffxiv_worlds');
        $this->redis->select($this->config->item('redis_timeseries_db'));
    }

    function test(){
        $unix_timestamp_24h_ago = time() - (24 * 60 * 60);
        $unix_timestamp_now = time();
        $result = $this->redis->executeRaw(['TS.RANGE', 'Spriggan_2156', 0, 99999999999999]);
        $result = $this->redis->executeRaw(['TS.RANGE', 'Spriggan_2156', $unix_timestamp_24h_ago, $unix_timestamp_now]);
        pretty_dump($result);
        pretty_dump($unix_timestamp_24h_ago);
        pretty_dump($unix_timestamp_now);
        //get unix timestamp 24h ago

        pretty_dump();
    }

    public function get_by_key($key, $minutes = 60){
        $unix_to = time();
        $unix_from = time() - ($minutes * 60);
        $result = $this->redis->executeRaw(['TS.RANGE', 'Spriggan_2156', $unix_from, $unix_to]);
    }

    public function get_world_scores($world_name){
        $this->load->model('Item_model', 'Item');
        $start = time();
        $result = $this->redis->executeRaw(['KEYS', '*']);
        $i = 0;

        foreach($result as $key=>$value){
            $profits = 0;
            $number_of_sales = 0;
            //Get value after the underscore
            $key_parts = explode('_', $value);
            $world = $key_parts[0];
            $item_id = $key_parts[1];
            if(strpos($value, $world_name) === false){
                continue;
            }
            $name = $this->Item->get($item_id)->name;
            
            $sales = $this->redis->executeRaw(['TS.RANGE', $value, 0, 99999999999999]);

            foreach($sales as $sale){
                $number_of_sales++;
                $profits += $sale[1]->getPayload();
            }

            $item = $this->Item->get($item_id);
            if(empty($item->name) || is_null($item->name)){
                pretty_dump($item);die();
            }
            $final_results[$name]['score'] = $profits * $number_of_sales;
            $final_results[$name]['world'] = $world;
        }


        $end = time();
        arsort($final_results);
        pretty_dump($final_results);
        pretty_dump($end - $start);
        
    }

    public function get_dc_scores($dc_name){
        $this->load->model('Item_model', 'Item');
        $start = time();
        $result = $this->redis->executeRaw(['KEYS', '*']);
        $i = 0;

        //Prepare final results array
        $final_results = array();
        $result = array();
        $worlds_in_dc = get_worlds_in_dc($dc_name, $this->config->item('ffxiv_worlds'));


        foreach($worlds_in_dc as $world_in_dc){
            foreach($this->redis->executeRaw(['KEYS', "*".$world_in_dc."*"]) as $entry){
                array_push($result, $entry);
            }
        }
        
        foreach($result as $key=>$value){
            //Score is the profit * number of sales
            $profits = 0;
            $number_of_sales = 0;

            //Get values for indexing
            //pretty_dump($value);
            $key_parts = explode('_', $value);
            $world = $key_parts[0];
            $item_id = $key_parts[1];

            if(in_array($world, $worlds_in_dc) === false){
                continue;
            }

            $name = $this->Item->get($item_id)->name;
            
            $sales = $this->redis->executeRaw(['TS.RANGE', $value, 0, 99999999999999]);

            foreach($sales as $sale){
                $number_of_sales++;
                $profits += $sale[1]->getPayload();
            }

            $item = $this->Item->get($item_id);
            if(empty($item->name) || is_null($item->name)){
                pretty_dump($item);die();
            }

            if(isset($final_results[$name])){
                $final_results[$name]['score'] = $final_results[$name]['score'] + $profits * $number_of_sales;
                $final_results[$name]['world_data_used'] = $final_results[$name]['world_data_used'] .= ', '.$world;
            }else{
                $final_results[$name]['score'] = $profits * $number_of_sales;
                $final_results[$name]['world_data_used'] = $world;
            }

        }


        $end = time();
        arsort($final_results);
        pretty_dump($final_results);
        pretty_dump($end - $start);
        
    }
}