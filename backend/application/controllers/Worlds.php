<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Worlds extends MY_Controller {


    public function __construct(){
		parent::__construct();
		$this->load->helper('url');
		$this->load->model('World_model', 'Worlds');
	}

	public function index(){

	}

	public function get_all_worlds(){
		$worlds = $this->Worlds->get();
		echo json_encode($worlds);
	}


	public function api_get_worlds_per_region($region){
		echo json_encode($this->Worlds->get_by_region($region));
	}

	public function api_get_worlds_per_server($server){
		echo json_encode($this->Worlds->get_by_server($server));
	}
}