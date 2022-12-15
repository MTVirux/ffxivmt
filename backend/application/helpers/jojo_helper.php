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
