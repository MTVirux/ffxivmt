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
