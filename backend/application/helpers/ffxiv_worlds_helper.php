<?php
defined('BASEPATH') OR exit('No direct script access allowed');

    function get_world_name($world_id, $world_data){
        return $world_data[$world_id]["name"];

    }

    function get_world_id($world_name, $world_data){
        foreach ($world_data as $key => $value){
            if($value["name"] == $world_name){
                return $key;
            }
        }
    }

    function get_world_dc($world_name, $world_data){
        foreach ($world_data as $key => $value){
            if($value["name"] == $world_name){
                return $world_data[$key]['datacenter'];
            }
        }
    }

    function get_world_region($world_name, $world_data){
        foreach ($world_data as $key => $value){
            if($value["name"] == $world_name){
                return $world_data[$key]['region'];
            }
        }
    }

    function get_world_info($world_name, $world_data){
        return $world_data[get_world_id($world_name, $world_data)];
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

    function get_worlds_in_region($region_name, $world_data){
        $worlds_in_region = array();
        foreach ($world_data as $key => $value){
            if(strtolower($value["region"]) == strtolower($region_name)){
                $worlds_in_region[] = $value["name"];
            }
        }
        return $worlds_in_region;
    }



    function get_worlds_to_use($worlds_to_use, $dcs_to_use, $regions_to_use, $world_data){
        if(empty($worlds_to_use) || is_null($worlds_to_use))
            $worlds_to_use = array();

        foreach($dcs_to_use as $dc){
            $worlds_to_use = array_merge($worlds_to_use, get_worlds_in_dc($dc, $world_data));
        }

        foreach($regions_to_use as $region){
            $worlds_to_use = array_merge($worlds_to_use, get_worlds_in_region($region, $world_data));
        }

        return $worlds_to_use;
    }
