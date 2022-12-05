<?php
defined('BASEPATH') OR exit('No direct script access allowed');

function garland_db_get_items($item_id){

    $item_id_array = explode(',', $item_id);
    //Get the first and last item id
    $first_item_id = $item_id_array[0];
    $last_item_id = end($item_id_array);
    
    if($first_item_id == $last_item_id){
        $range_string = '[' . $first_item_id . ']';
    }else{
        $range_string = '[' . $first_item_id . '-' . $last_item_id . ']';
    }

    logger("GARLAND_DB", "Getting item data from Garland DB for items: " . $range_string);

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
