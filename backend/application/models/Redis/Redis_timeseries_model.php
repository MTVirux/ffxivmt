<?php
defined('BASEPATH') OR exit('No direct script access allowed');
require APPPATH.'core/MY_Redis_Model.php';


class Redis_timeseries_model extends MY_Redis_model{
    
    function __construct() {
        parent::__construct();
        $this->load->helper('ffxiv_worlds');
        $this->redis->select($this->config->item('redis_timeseries_db'));
        $this->load->model('Item_model', 'Item');
        $this->load->model('Item_score_model', 'Item_score');
        $this->craft_complexity_weight = $this->config->item('craft_complexity_weight');
    }


    private function get_times(){
        return array_reverse(
            array(
                '5min' => time() - (5 * 60),
                '15min' => time() - (15 * 60),
                '30min' => time() - (30 * 60),
                '1h' => time() - (60 * 60),
                '2h' => time() - (60 * 60 * 2),
                '6h' => time() - (60 * 60 * 6),
                '12h' => time() - (60 * 60 * 12),
                '1d' => time() - (60 * 60 * 24),
                '2d' => time() - (60 * 60 * 24 * 2),
                '5d' => time() - (60 * 60 * 24 * 5),
                '1w' => time() - (60 * 60 * 24 * 7),
                '2w' => time() - (60 * 60 * 24 * 14),
                '1mo' => time() - (60 * 60 * 24 * 30),
                '2mo' => time() - (60 * 60 * 24 * 60),
                '6mo' => time() - (60 * 60 * 24 * 180),
                'patch' => time() - (60 * 60 * 24 * 120),
                '1y' => time() - (60 * 60 * 24 * 365),
                'expansion' => time() - (60 * 60 * 24 * 365 * 4),
                'alltime' => 0
            )
        );
    }

    public function get_all_keys(){
        $keys = $this->redis->keys('*');
        return $keys;
    }

    public function get_by_key($key, $minutes = 60){
        $unix_to = time();
        $unix_from = time() - ($minutes * 60);
        $result = $this->redis->executeRaw(['TS.RANGE', 'Spriggan_2156', $unix_from, $unix_to]);
    }

    public function get_world_scores($world_name, $start_time = null, $end_time = null){
        $this->load->model('Item_model', 'Item');
        $start = time();
        $result = $this->redis->executeRaw(['KEYS', '*']);
        $i = 0;

        if (is_null($start_time))
            $start_time = time() - (60*60*24*7); // 1 week back

        if (is_null($end_time))
            $end_time = time(); // now

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
            
            $sales = $this->redis->executeRaw(['TS.RANGE', $value, $start_time, $end_time]);

            foreach($sales as $sale){
                $number_of_sales++;
                $profits += $sale[1]->getPayload();
            }

            $item = $this->Item->get($item_id);
            if(empty($item->name) || is_null($item->name)){
                pretty_dump($item);die();
            }
            $final_results[$name]['id'] = $item_id;
            $final_results[$name]['volume'] = $number_of_sales;
            $final_results[$name]['score'] = $profits * $number_of_sales;
            $final_results[$name]['world'] = $world;
        }


        $end = time();
        arsort($final_results);
        pretty_dump($final_results);
        pretty_dump($end - $start);
        
    }

    public function get_dc_scores($dc_name, $start_time = null, $end_time = null){

        $redis_time = intval($this->redis->executeRaw(['TIME'])[0]);
        $earliest_timestamp = 0;
        $total_number_of_sales = 0;

        if(is_null($start_time))
            $start_time = $redis_time - (60*60*24*7); // 1 day back

        if(is_null($end_time))
            $end_time = $redis_time; // now


        $this->load->model('Item_model', 'Item');
        $start = time();
        $result = $this->redis->executeRaw(['KEYS', '*']);
        $i = 0;

        //Prepare final results array
        $final_results = array();
        $result = array();
        $worlds_in_dc = get_worlds_in_dc($dc_name, $this->config->item('ffxiv_worlds'));
        $total_sales = 0;


        foreach($worlds_in_dc as $world_in_dc){
            foreach($this->redis->executeRaw(['KEYS', "*".$world_in_dc."*"]) as $entry){
                array_push($result, $entry);
            }
        }
        
        foreach($result as $key=>$value){
            //Score is the profit * number of sales
            $profits = 0;
            $number_of_sales = 0;
            $current_entry_score = 0;

            //Get values for indexing
            //pretty_dump($value);
            $key_parts = explode('_', $value);
            $world = $key_parts[0];
            $item_id = $key_parts[1];

            if(in_array($world, $worlds_in_dc) === false){
                continue;
            }

            $name = $this->Item->get($item_id)->name;
        
            $sales = $this->redis->executeRaw(['TS.RANGE', $value, $start_time, $end_time]);
            

            foreach($sales as $sale){
                $total_sales++;
                $number_of_sales++;
                if($earliest_timestamp == 0 || $sale[0] < $earliest_timestamp){
                    $earliest_timestamp = $sale[0];
                }
                $profits += $sale[1]->getPayload();
                $current_entry_score = $profits * $number_of_sales;
            }

            $item = $this->Item->get($item_id);
            if(empty($item->name) || is_null($item->name)){
                pretty_dump($item);
            }

            if($current_entry_score > 0){
                if(isset($final_results[$name])){
                    $final_results[$name]['score'] = $final_results[$name]['score'] + $current_entry_score;
                    $final_results[$name]['world_data_used'] = $final_results[$name]['world_data_used'] .= ', '.$world;
                    $final_results[$name]['volume'] = $final_results[$name]['volume'] + $number_of_sales;
                }else{
                    $final_results[$name]['id'] = intval($item_id);
                    $final_results[$name]['volume'] = $number_of_sales;
                    $final_results[$name]['score'] = $current_entry_score;
                    $final_results[$name]['world_data_used'] = $world;
                }
            }
        }
        $end = time();
        arsort($final_results);
        return $final_results;
    
    }
}