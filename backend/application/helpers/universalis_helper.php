<?php
defined('BASEPATH') OR exit('No direct script access allowed');

function universalis_get_mb_data($dc_or_server, $item_id){
    //GET request to garlandtools.org
    if(gettype($item_id) == "array"){
        $item_id = implode(",", $item_id);
    }
    $base_url = 'https://universalis.app/api/v2/';
    $url = $base_url . $dc_or_server . '/' . $item_id;
    $ch = curl_init();
    curl_setopt($ch, CURLOPT_URL, $url);
    curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
    $output = curl_exec($ch);
    curl_close($ch);
    $mb_data = json_decode($output, true);

    return $mb_data;

}

function universalis_get_marketable_item_ids(){
    $retries = 0;
    $max_retries = 500;
    $httpcode = 0;

    while($httpcode != 200 && $retries < $max_retries){
        $ch = curl_init();
        curl_setopt($ch, CURLOPT_URL, 'https://universalis.app/api/v2/marketable');
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
        $output = curl_exec($ch);
        $httpcode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);
        $retries++;
        if($httpcode != 200){
            logger("UNIVERSALIS_API", "Universalis API returned HTTP code " . $httpcode . " on attempt " . $retries . " of " . $max_retries);
        }
        sleep(0.1);
    }

    if($httpcode != 200){
        logger('UNIVERSALIS_API', 'Error getting marketable item ids from universalis.app. KILLING SCRIPT');
        die();
        return false;
    }

    logger("UNIVERSALIS_API", $httpcode . " GET SUCCESS");

    $marketable_item_ids = json_decode($output, true);

    return $marketable_item_ids;
}
