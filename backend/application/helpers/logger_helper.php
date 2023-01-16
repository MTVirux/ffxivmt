<?php
defined('BASEPATH') OR exit('No direct script access allowed');

function logger($channel, $message, $custom_file = null){

    if(!in_array($channel, config_item('custom_log_channels')) && !in_array('ALL', config_item('custom_log_channels'))){
        return;
    }

    //Add log timestamp
    
    //if message is a json make it an array
    if(is_json($message)){
        $decoded_message = json_decode($message);
    }else if(is_array($message)){
        $decoded_message = $message;
    }else if(is_string($message)){
        $decoded_message = ["message" => $message];
    }
    
    $decoded_message = array_merge(["log_timestamp" => date('Y-m-d H:i:s')], (array)$decoded_message);
    
    $message = json_encode($decoded_message);

    $file = fopen(APPPATH . 'logs/' . $channel . '.log', 'a');
    
    //write message to file
    fwrite($file, $message . PHP_EOL);

    if(ENVIRONMENT !== 'production'){
        $file = fopen(APPPATH . 'logs/' . 'ALL' . '.log', 'a');
        //write message to file
        fwrite($file, $message . PHP_EOL);
    }
    //close file
    fclose($file);
    
    return true;

}

function is_json($string) {
 json_decode($string);
 return (json_last_error() == JSON_ERROR_NONE);
}