<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class MY_Controller extends CI_Controller{

    public function __construct(){
        parent::__construct();
        $this->load->helper('url');
    }

    public function load_view_template($view, $data = null){
        $this->load->view('common/header');
        $this->load->view('common/navbar');
        $this->load->view($view, $data);
        $this->load->view('common/footer');
    }

}