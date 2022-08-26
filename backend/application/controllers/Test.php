<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Test extends CI_Controller {

	public function __construct(){
		parent::__construct();
		$this->load->helper('url');
		$this->load->model('/Redis/Redis_timeseries_model', 'Redis_ts');
		
	}

	public function index($world_name)
	{
		$this->load->view("test/usage");
	}

	public function world_scores($world_name="Spriggan"){
		if(!empty($_GET['world_name']))
			$world_name = $_GET['world_name'];
		
		$this->Redis_ts->get_world_scores($world_name);
	}

	public function dc_scores($dc_name="Chaos"){
		if(!empty($_GET['dc_name']))
			$dc_name = $_GET['dc_name'];
		
		$this->Redis_ts->get_dc_scores($dc_name);
	}

	public function search_item(){
        var_dump($_POST);
    }
}
