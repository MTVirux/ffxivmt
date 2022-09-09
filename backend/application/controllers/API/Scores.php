<?php
defined('BASEPATH') OR exit('No direct script access allowed');

require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/RestController.php';
require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/Format.php';
use chriskacerguis\RestServer\RestController;

class Scores extends RestController{

    function __construct(){
        parent::__construct();
        $this->load->model('Scores_model', 'Scores');
        $this->load->model('redis/redis_timeseries_model', 'redis_ts');
    }


    public function get_world_scores($world_name="Spriggan", $start_time = null, $end_time = null){

		if($start_time == null)
			$start_time = time() - (60*60*24*7); // 1 week back
		
		if($end_time == null)
			$end_time = time(); // now

		if(!empty($_GET['world_name']))
			$world_name = $_GET['world_name'];
		
		$this->Redis_ts->get_world_scores($world_name);
	}

	public function get_dc_scores($dc_name="Chaos", $start_time = null, $end_time = null){

		if(is_null($start_time))
			$start_time = time() - (60*60*24); // 1 week back

		if(is_null($end_time))
			$end_time = time(); // now

		if(!empty($_GET['dc_name']))
			$dc_name = $_GET['dc_name'];
		
		$this->Redis_ts->get_dc_scores($dc_name, $start_time, $end_time);
	}


}