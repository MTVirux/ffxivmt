<?php
defined('BASEPATH') OR exit('No direct script access allowed');
include_once APPPATH.'core/MY_Redis_Model.php';


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

    public function global_item_score_update(){
        //Load configs
        $worlds = $this->config->item('ffxiv_worlds');
        $datacenters = $this->config->item('ffxiv_datacenters');
        //Load craftable items
        $craftable_items_ids = $this->Item->get_craftable_items_ids();


        //Update item scores
        foreach( $craftable_items_ids as $item_id){
            if(is_null($this->Item->get($item_id)->craftingRecipe)){
                pretty_dump($this->Item->get($item_id)->craftingRecipe);
            }

            $this->calc_item_score($item_id);
        }
        return true;
    }

    public function calc_item_score($item_id, $world = null, $unix_to = null, $unix_from = null){

        //If NULL set to now
        if(is_null($unix_to)){
            $unix_to = time();
        }

        //If NULL set to 1 day ago
        if(is_null($unix_from)){
            $unix_from = time() - (60 * 60 * 24);
        }

        //If null set to all worlds
        if(is_null($world)){
            $redis_item_keys = $this->redis->keys('*_'.$item_id);
        }else{
            $redis_item_keys = $this->redis->keys($world.'_'.$item_id);
        }
        
        $worlds_to_use = get_worlds_to_use( $this->config->item('worlds_to_use'), 
                                            $this->config->item('dcs_to_use'), 
                                            $this->config->item('regions_to_use'), 
                                            $this->config->item('ffxiv_worlds')
                                        );

        $redis_item_keys_count = count($redis_item_keys);

        if($redis_item_keys_count == 0){
            logger('ITEM_SCORE', 'Cleaning entry due to no keys: ' . $item_id);
            if(!is_null($world)){
                $this->Item_score->remove($item_id, $world);
            }else{
                $this->Item_score->remove($item_id);
            }
            return;
        }

        logger('ITEM_SCORE', '['. $item_id .'] -> # of keys: ' . $redis_item_keys_count); 

        foreach($redis_item_keys as $redis_item_key){
            
            $split_key = explode('_', $redis_item_key);
            
            $world = $split_key[0];

            if(in_array($world, $worlds_to_use)){
                $dc = get_world_dc($world, $this->config->item('ffxiv_worlds'));
                $region = get_world_region($world, $this->config->item('ffxiv_worlds'));
                $redis_entry = $this->redis->executeRaw(['TS.GET', $redis_item_key]);
                logger('REDIS_RECORD', 'Record found for '. $item_id . ' @ ' . date('Y-m-d H:i:s', $redis_entry[0]), 'redis_time_keeper');

                $item_score_entry['item_id'] = $item_id;
                $item_score_entry['world'] = $world;
                $item_score_entry['datacenter'] = $dc;
                $item_score_entry['region'] = $region;
                $item_score_entry['name'] = $this->Item->get($item_id)->name;
                $item_score_entry['craftComplexityWeightUsed'] = $this->config->item('craft_complexity_weight');
                $item_score_entry['updated_at'] = date('Y-m-d H:i:s');
                $item_score_entry['latest_sale'] = date('Y-m-d H:i:s' ,$redis_entry[0]);
                $item_score_entry['craft'] = boolval(is_null($this->Item->get($item_id)->craftingRecipe)) ? 0 : 1;
                $total_price = 0;
                         

                foreach($this->get_times() as $col => $unix_from){
                    $total_price = 0;
                    $item_score_entry[$col] = $this->redis->executeRaw(['TS.RANGE', $redis_item_key, $unix_from, $unix_to]);
                    //logger('INFO', '['. $item_id .']['.$col.']['.$item_score_entry['world'].'] - ['.$item_score_entry['name'].'] -> # of hits: ' . count($item_score_entry[$col]));          
                    if(count($item_score_entry[$col]) == 0){
                        $item_score_entry[$col] = 0;
                        //logger('INFO', '['. $item_id .']['.$col.']['.$item_score_entry['world'].'] - ['.$item_score_entry['name'].'] -> Setting to 0 and skipping to next time period');

                    }else{

                        foreach($item_score_entry[$col] as $entry){
                            $total_price += $entry[1]->getPayload();
                        }

                        $item_score_entry[$col] = $total_price;
                        //logger('ITEM_SCORE_2', '['. $item_id .']['.$col.']['.$item_score_entry['world'].'] - ['.$item_score_entry['name'].'] -> ' . $item_score_entry[$col]);

                    }

                }

                $this->Item_score->update($item_score_entry);
                unset($item_score_entry);
                unset($total_price);
            }
        }
        return $this->Item_score->get($item_id);
    }

    //Calculate item score
    function calculate_item_score($item_id , $total_price){
    

        //Load craft complexity values
        $craft_complexity_weight = $this->config->item('craft_complexity_weight');
        $craft_complexity = $this->Item->get_craft_complexity($item_id);

        //Load Item model
        $this->load->model('Item_model', 'Item');



        if($craft_complexity_weight == 0 || $craft_complexity == 0 || is_null($craft_complexity)){
            logger('ERROR', "[$item_id: ".$item_id."]Error on craft_complexity_weight(".$craft_complexity_weight.") or craft_complexity(".$craft_complexity.") during calculation of item score");
            logger('ERROR', $total_price);
            die();

        } else {

            $total_score = $total_price;
        }

        //Actually calculate the score

        return $total_score;

    }


    public function transpose_sales_to_ts(){
        $this->redis->select(1);
        $keys = $this->redis->keys('*');
        $total_keys = count($keys);
        $transposed_key_count = 0;
        //pretty_dump($keys);
        echo 'Transposing...';

        foreach($keys as $key){
            $this->redis->select(1);
            $key_result = json_decode($this->redis->executeRaw(['JSON.GET', $key]));
            $this->redis->select(2);
            foreach($key_result as $result){
                $this->redis->select(2);
                if($this->redis->executeRaw(['TS.ADD', $key, intval($result->timestamp), floatval($result->total)])){
                    logger('TRANSPOSE_SALES_TO_TS', '['.$transposed_key_count.' / '.$total_keys.'] Transposed ['.$key.'] -> '.$result->timestamp.' -> '.$result->total . ' Successfully');
                }else{
                    logger('ERROR', 'Failed to Transpose ['.$key.'] -> '.$result->timestamp.' -> '.$result->total);
                    pretty_dump('Failed to Transpose ['.$key.'] -> '.$result->timestamp.' -> '.$result->total);die();
                }
            }
            $this->redis->select(1);
            $transposed_key_count = $transposed_key_count + 1;
        }
        pretty_dump('Transposed:' . $transposed_key_count . ' / ' . $total_keys . ' keys');
    }
    
}