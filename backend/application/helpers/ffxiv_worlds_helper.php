<?php
defined('BASEPATH') OR exit('No direct script access allowed');

function get_world_name($world_id){
    $this->load->config('worlds');
    $worlds = $this->config->item('ffxiv_worlds');

    return $worlds[$world_id];

}

function get_world_id($world_id){
    $this->load->config('worlds');
    $worlds = $this->config->item('ffxiv_worlds');

    foreach ($worlds as $key => $value){
        if($value == $world_id){
            return $key;
        }
    }
}


function get_item_name($item_id){
    $this->load->config('worlds');
    //Curl to get item name
    $ch = curl_init();
    curl_setopt($ch, CURLOPT_URL, "https://xivapi.com/item/".$item_id."?columns=Name");
    $result = curl_exec($ch);
    $result = json_decode($result, true);
    curl_close($ch);
    return $result['name'];
}


function get_item_id(){
    
}