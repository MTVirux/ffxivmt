<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Test extends CI_Controller {

	public function __construct(){
		parent::__construct();
		$this->load->helper('url');

		
	}

	public function index()
	{
        var_dump($_POST);

	}

	public function search_item(){
        var_dump($_POST);
    }
}
