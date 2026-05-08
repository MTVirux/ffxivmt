<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Test extends MY_Controller {

	public function __construct(){
		parent::__construct();
		$this->load->helper('url');
	}

	public function index()
	{

		Header('Access-Control-Allow-Methods: GET'); //method allowed
		echo json_encode('ok');
	}

}
