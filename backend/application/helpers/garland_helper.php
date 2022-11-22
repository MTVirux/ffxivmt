<?php
defined('BASEPATH') OR exit('No direct script access allowed');

function garland_get_item($item_id){
    //GET request to garlandtools.org
    $base_url = 'https://www.garlandtools.org/db/doc/item/en/3/';
    $url = $base_url . $item_id . '.json';
    $ch = curl_init();
    curl_setopt($ch, CURLOPT_URL, $url);
    curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
    $output = curl_exec($ch);
    curl_close($ch);
    $item = json_decode($output, true);
    return $item;
}
