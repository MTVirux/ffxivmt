<?php
defined('BASEPATH') OR exit('No direct script access allowed');

function logger($channel, $message, $custom_file = null){

    if(!in_array($channel, config_item('custom_log_channels')) && !in_array('ALL', config_item('custom_log_channels'))){
        return;
    }

    $file = fopen(APPPATH . 'logs/' . $channel . '.log', 'a');
    
    //write message to file
    fwrite($file, $message . PHP_EOL);

    //close file
    fclose($file);
    
    return true;

}