<?php

namespace App\Controllers;

class RawFiles extends BaseController
{
    public function items(){
        return `https://raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/Item.csv`;
    }
}
