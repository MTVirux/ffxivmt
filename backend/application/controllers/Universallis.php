<?php

namespace App\Controllers;

defined('BASEPATH') OR exit('No direct script access allowed');


class Universallis extends BaseController
{
    public function get_item($id){
        $items = file_get_contents('https://universalis.app/api/tax-rates');
        var_dump();die();
        return $item;
    }
}
