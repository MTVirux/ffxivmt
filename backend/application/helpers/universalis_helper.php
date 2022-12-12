<?php
defined('BASEPATH') OR exit('No direct script access allowed');

function universalis_get_mb_data($dc_or_server, $item_id){
    $retries = 0;
    $max_retries = 500;
    $httpcode = 0;

    //GET request to garlandtools.org
    if(gettype($item_id) == "array"){
        $item_id = implode(",", $item_id);
    }

    while($httpcode != 200 && $retries < $max_retries){
        $base_url = 'https://universalis.app/api/v2/';
        $url = $base_url . $dc_or_server . '/' . $item_id;
        $ch = curl_init();
        curl_setopt($ch, CURLOPT_URL, $url);
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
        $output = curl_exec($ch);
        $httpcode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);
        $mb_data = json_decode($output, true);
        if($httpcode != 200){
            logger("UNIVERSALIS_API", "Universalis API returned HTTP code " . $httpcode . " on attempt " . $retries . " of " . $max_retries);
            $retries++;
        }
        sleep(0.1);
    }

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

function universalis_get_item_sales_data($item_ids, $worldDcRegion, $entriesToReturn = null, $stats_within = null, $entries_within = null){
    //90 days in milliseconds
    $milliseconds_in_90_days = 7776000000;

    if(is_null($stats_within)){
        $stats_within = $milliseconds_in_90_days;
    }

    if(is_null($entries_within)){
        $entries_within = $milliseconds_in_90_days;
    }

    if(is_null($entriesToReturn)){
        $entriesToReturn = 999999;
    }

    $base_url = 'https://universalis.app/api/v2/history';
    $url = $base_url;
    $url .= '/' . $worldDcRegion;
    $url .= '/' . $item_ids;
    $url .= '?entriesToReturn=' . $entriesToReturn;
    $url .= '&statsWithin=' . $stats_within;
    $url .= '&entriesWithin=' . $entries_within;

    
    $retries = 0;
    $max_retries = 500;
    $httpcode = 0;

    while($httpcode != 200 && $retries < $max_retries){
        logger("UNIVERSALIS_API", "Getting item sales data from Universalis API: " . $url);
        $ch = curl_init();
        curl_setopt($ch, CURLOPT_URL, $url);
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
        $output = curl_exec($ch);
        $httpcode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);
        $retries++;
        if($httpcode != 200){
            logger("UNIVERSALIS_API", "Universalis API returned HTTP code " . $httpcode . " on attempt " . $retries . " of " . $max_retries);
            sleep(5);
        }
        sleep(0.1);
    }

    if($httpcode != 200 || empty($output)){
        logger("UNIVERSALIS_API", "Universalis API stopped responding after " . $max_retries . " attempts. KILLING SCRIPT.");
        die();
        return false;
    }

    logger("UNIVERSALIS_API", $httpcode . " GET SUCCESS");

    $universalis_sales_data = json_decode($output, true);
    return $universalis_sales_data;

}
