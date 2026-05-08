<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Index extends MY_Controller{

    function __construct(){
        parent::__construct();
        $this->load->helper('ffxiv_worlds');
    }

    public function get_current_timestamp(){
        $time = time();
        echo $time;
        return $time;
    }

    public function index(){
		$this->load_view_template('home');
    }

}