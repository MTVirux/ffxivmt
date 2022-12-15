<?php
defined('BASEPATH') OR exit('No direct script access allowed');

function pretty_dump($var = null){
    if(is_null($var))
        return;
    echo '<pre>';
    var_dump($var);
    echo '</pre>';
}

function pretty_print($var = null){
    pretty_dump($var);
}

function logger($channel, $message, $custom_file = null){

    if(!in_array($channel, config_item('custom_log_channels')) && !in_array('ALL', config_item('custom_log_channels'))){
        return;
    }
    
    //open or create file in log folder
    if(is_null($custom_file)){
        
        $file = fopen(APPPATH . 'logs/' . 'log' . '-' . date('Y-m-d') .'.log', 'a');

    }else{
        $file = fopen(APPPATH . 'logs/' . $custom_file . '-' . date('Y-m-d') .'.log', 'a');
    }
    
    //write message to file
    fwrite($file, $channel . ' - ' . $message . PHP_EOL);

    //close file
    fclose($file);
    
    return true;

}