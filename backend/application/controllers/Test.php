<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Test extends CI_Controller {

	public function __construct(){
		parent::__construct();
		$this->load->helper('url');
		$this->load->model('/Redis/Redis_timeseries_model', 'Redis_ts');
		
	}

	public function index()
	{
		$this->load->view("test/usage");
	}

	public function world_scores($world_name="Spriggan", $start_time = null, $end_time = null){

		if($start_time == null)
			$start_time = time() - (60*60*24*7); // 1 day back
		
		if($end_time == null)
			$end_time = time(); // now

		if(!empty($_GET['world_name']))
			$world_name = $_GET['world_name'];
		
		$world_scores = $this->Redis_ts->get_world_scores($world_name);
		return $world_scores;
	}

	public function dc_scores($dc_name="Chaos", $start_time = null, $end_time = null){

		if($start_time == null)
			$start_time = time() - (60*60*24*7); // 1 day back
		
		if($end_time == null)
			$end_time = time(); // now

		if(!empty($_GET['dc_name']))
			$dc_name = $_GET['dc_name'];
		
		$dc_scores = $this->Redis_ts->get_dc_scores($dc_name, $start_time, $end_time);

		pretty_dump($dc_scores);
		return $dc_scores;
	}

	public function test() {
		$this->Redis_ts->test();
	}
}
