<?php
defined('BASEPATH') OR exit('No direct script access allowed');

function get_world_name($world_id){
    $this->load->config('worlds');
    $worlds = $this->config->item('ffxiv_worlds');

    return $worlds[$world_id];

}

function get_world_id($world_name){
    $this->load->config('worlds');
    $worlds = $this->config->item('ffxiv_worlds');

    foreach ($worlds as $key => $value){
        if($value["name"] == $world_name){
            return $key;
        }
    }
}

function get_worlds_in_dc($dc_name, $world_data){
    $worlds_in_dc = array();
    foreach ($world_data as $key => $value){
        if($value["datacenter"] == $dc_name){
            $worlds_in_dc[] = $value["name"];
        }
    }
    return $worlds_in_dc;
}


function get_item_id(){
    
}