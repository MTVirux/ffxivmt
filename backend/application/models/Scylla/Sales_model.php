<?php
defined ('BASEPATH') OR exit('No direct script access allowed');
require APPPATH.'core/MY_Scylla_Model.php';


class Sales_model extends MY_Scylla_Model{
    
    function __construct() {
        parent::__construct();
        echo 'sales_model';
    }
    

    public function test(){
        echo 'sales_model_test';
    }
}