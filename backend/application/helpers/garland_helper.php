<?php
defined('BASEPATH') OR exit('No direct script access allowed');

function garland_db_get_items($item_id){

    $item_id_array = [];

    //Is array
    if(gettype($item_id) == 'array'){
        $item_id_array = $item_id;
    }else if(count(explode(',', $item_id)) > 1){ //String of multiple ids
        $item_id_array = explode(',', $item_id);
    }else{ //Is single id String
        $item_id_array = [];
        $item_id_array[] = intval($item_id);
    }

    //Get first and last item id
    $first_item_id = $item_id_array[0];
    $last_item_id = end($item_id_array);
    
    //Create range string
    if($first_item_id == $last_item_id){
        $range_string = $first_item_id;
    }else{
        $range_string = $first_item_id . '-' . $last_item_id;
    }

    //Convert $item_id_array to comma separated string
    $item_id = implode(',', $item_id_array);

    logger("GARLAND_DB", "Getting item data from Garland DB for item(s): " . $range_string);

    //GET request to garlandtools.org
    $base_url = 'https://www.garlandtools.org/db/doc/item/en/3/';
    $url = $base_url . $item_id . '.json';
    $ch = curl_init();
    curl_setopt($ch, CURLOPT_URL, $url);
    curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
    $output = curl_exec($ch);
    $httpcode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
    curl_close($ch);
    $item = json_decode($output, true);

    if($httpcode == 200){
        logger("GARLAND_DB", "[SUCCESS] Getting item data from Garland DB for items: " . $range_string);
    }else{
        logger("GARLAND_DB", "[ERROR - " . $httpcode . "] Getting item data from Garland DB for items: " . $range_string);
        logger("GARLAND_DB", "ERROR_URL: " . $url);
        return False;
    }
    
    return $item;
}


function garland_db_get_instances($instance_id = null){

    if(is_null($instance_id) || empty($instance_id) || $instance_id == 'ALL'){

        $instance_id_array = [];
        $range_string = 'ALL';
        $base_url = 'https://www.garlandtools.org/db/doc/browse/en/2/instance';

    }else{ 
        if(is_string($instance_id)){
            $exploded_instance_id = explode(',', $instance_id);
            if(count($exploded_instance_id) > 1){ //String of multiple ids
                $instance_id_array = $exploded_instance_id;
            }
        }else if(is_numeric($instance_id)){ //Is single id String
            $instance_id_array = [];
            $instance_id_array[] = intval($instance_id);
        }else if(is_array($instance_id)){
            $instance_id_array = [$instance_id];
        }else{
            logger("GARLAND_DB", "[ERROR] Getting instance data from Garland DB for instances: " . $instance_id);
            logger("GARLAND_DB", "ERROR: Invalid instance_id type: " . gettype($instance_id));
            return False;
        }
        $base_url = 'https://www.garlandtools.org/db/doc/instance/en/2/';
        $url = $base_url . $instance_id . '.json';
    }

    logger("GARLAND_DB", "Getting instance data from Garland DB for instance(s): " . $instance_id);

    //GET request to garlandtools.org
    $url = $base_url . $instance_id . '.json';
    $ch = curl_init();
    curl_setopt($ch, CURLOPT_URL, $url);
    curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
    $output = curl_exec($ch);
    $httpcode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
    curl_close($ch);
    $instances = json_decode($output, true);

    if($httpcode == 200){
        logger("GARLAND_DB", "[SUCCESS] Getting instance data from Garland DB for instances: " . $instance_id);
    }else{
        logger("GARLAND_DB", "[ERROR - " . $httpcode . "] Getting item data from Garland DB for items: " . $instance_id);
        logger("GARLAND_DB", "ERROR_URL: " . $url);
        return False;
    }
    
    return $instances;
}

//TODO: Test garland_db_get_items_from_currency
function garland_db_get_items_from_currency($currency_id){

    $item_data = garland_db_get_items($currency_id);

    $shops = $item_data["item"]["tradeCurrency"];

    $final_data = [];

    foreach($shops as $shop_index => $shop){
        $listings = $item_data["item"]["tradeCurrency"][$shop_index]["listings"];
        //pretty_dump($shop_index);
        foreach($listings as $listing){
            //pretty_dump($listing);die();
            $final_data[$listing["item"][0]["id"]] = [
                "name" => $this->Scylla_Item->get($listing["item"][0]["id"])[0]["name"],
                "id" => $listing["item"][0]["id"],
                "price" => $listing["currency"][0]["amount"],
                "currency_id" => $listing["currency"][0]["id"],
                "currency_name" => $this->Scylla_Item->get($listing["currency"][0]["id"])[0]["name"],
            ];
        }
    }

    pretty_dump($final_data);die();

}