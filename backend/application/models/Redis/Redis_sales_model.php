<?php
defined('BASEPATH') OR exit('No direct script access allowed');
require APPPATH.'core/MY_Redis_Model.php';


class Redis_listings_model extends MY_Redis_model{
    
    function __construct() {
        parent::__construct();
        $this->load->helper('ffxiv_worlds');
        $this->redis->select($this->config->item('redis_sales_db'));
    }

    function test(){
    }
}