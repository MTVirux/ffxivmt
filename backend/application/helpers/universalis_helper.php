<?php
defined('BASEPATH') OR exit('No direct script access allowed');

function universalis_get_mb_data($dc_or_server, $item_id){
    
    if(gettype($item_id) == "array"){
        $item_id = implode(",", $item_id);
    }
    
    $base_url = 'https://universalis.app/api/v2/';
    $url = $base_url . $dc_or_server . '/' . $item_id;


    $mb_data = json_decode(universalis_run_request($url), true);

    return mb_data;

}

function universalis_get_marketable_item_ids(){

    $url = 'https://universalis.app/api/v2/marketable';

    $marketable_item_ids = json_decode(universalis_run_request($url), true);

    return $marketable_item_ids;
}

function universalis_get_item_sales_data($item_ids, $worldDcRegion, $time_ago_to_query = null, $entriesToReturn = null, $stats_within = null, $entries_within = null){


    $preset_time_intervals = [
                                "90 days"   =>    7776000000,   //milliseconds_in_90_days,
                                "30 days"   =>    2592000000,   //milliseconds_in_30_days,
                                "15 days"   =>    1296000000,   //milliseconds_in_15_days,
                                "7 days"    =>    604800000,    //milliseconds_in_7_days,
                                "1 day"     =>    86400000,     //milliseconds_in_1_day,
                            ];
    
    if(!is_null($time_ago_to_query)){
        if(array_key_exists($time_ago_to_query, $preset_time_intervals)){
            $time_ago_to_query = $presest_time_intervals[$time_ago_to_query];
        }
    }

    if(is_null($stats_within)){
        $stats_within = $preset_time_intervals["90 days"];
    }

    if(is_null($entries_within)){
        $entries_within = $preset_time_intervals["90 days"];
    }

    if(is_null($entriesToReturn)){
        $entriesToReturn = 999999;
    }

    if(is_null($time_ago_to_query)){
        $time_ago_to_query = $preset_time_intervals["90 days"];
    }

    if(gettype($item_ids) == "array"){
        $item_ids = implode(",", $item_id);
    }

    $base_url = 'https://universalis.app/api/v2/history';
    $url = $base_url;
    $url .= '/' . $worldDcRegion;
    $url .= '/' . $item_ids;
    $url .= '?entriesToReturn=' . $entriesToReturn;
    $url .= '&statsWithin=' . $stats_within;
    $url .= '&entriesWithin=' . $entries_within;

    
    $reponse = universalis_run_request($url);

    $universalis_sales_data = json_decode($reponse, true);
    return $universalis_sales_data;

}

function universalis_get_worlds_enpoint(){
    //CURL https://universalis.app/api/v2/worlds

    $response = universalis_run_request('https://universalis.app/api/v2/worlds');

    $enpoint_world_array = json_decode($response, true);
    $preapred_world_array = [];
    foreach($enpoint_world_array as $endpoint_world){
        $preapred_world_array[$endpoint_world["id"]] = $endpoint_world["name"];
    }

    return $preapred_world_array;
}

function universalis_get_dcs_enpoint(){
    //CURL https://universalis.app/api/v2/data-centers

    $response = universalis_run_request('https://universalis.app/api/v2/data-centers');

    return json_decode($response, true);
}


function universalis_get_all_worlds(){
    $endpoint_worlds = universalis_get_worlds_enpoint();
    $endpoint_dcs = universalis_get_dcs_enpoint();


    $worlds_array = [];

    foreach($endpoint_dcs as $endpoint_dc){
        $datacenter = $endpoint_dc['name'];
        $region = $endpoint_dc['region'];
        foreach($endpoint_dc['worlds'] as $world_id){
            $worlds_array[] = [
                'id' => $world_id,
                'name' => $endpoint_worlds[$world_id],
                'datacenter' => $datacenter,
                'region' => $region
            ];
        }
    }

    return $worlds_array;
}





function universalis_run_request($url, $max_retries = 500, $retry_sleep_time = 0.1){

    $retries = 0;
    $httpcode = 0;
    $request_uid = uniqid();

    logger("UNIVERSALIS_API", json_encode(array("request_uid" => $request_uid, "response_code" => $httpcode, "message" => "Running URL Request", "url" => $url)));
    while($httpcode != 200 && $retries < $max_retries){
        $ch = curl_init();
        curl_setopt($ch, CURLOPT_URL, $url);
        curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
        $output = curl_exec($ch);
        $httpcode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        curl_close($ch);
        $retries++;
        if($httpcode != 200){
            logger("UNIVERSALIS_API", json_encode(array("request_uid" => $request_uid, "response_code" => $httpcode, "message" => "Retry: " . $retries . " / " . $max_retries)));
            sleep($retry_sleep_time);
        }
        sleep($retry_sleep_time);
    }

    if($httpcode != 200 || empty($output)){
        logger("UNIVERSALIS_API", json_encode(array("request_uid" => $request_uid, "response_code" => $httpcode, "message" => "Max retries reached. ABORTING")));
        return false;
    }

    logger("UNIVERSALIS_API", json_encode(array("request_uid" => $request_uid, "response_code" => $httpcode, "message" => "SUCCESS")));

    return $output;

}