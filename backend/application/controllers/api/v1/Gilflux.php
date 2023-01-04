<?php
defined('BASEPATH') OR exit('No direct script access allowed');

require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/RestController.php';
require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/Format.php';
use chriskacerguis\RestServer\RestController;

class Gilflux extends RestController{

    function __construct(){
        parent::__construct();            
		Header('Access-Control-Allow-Origin: *'); //for allow any domain, insecure
		Header('Access-Control-Allow-Headers: *'); //for allow any headers, insecure
		Header('Access-Control-Allow-Methods: GET,POST,PUT,DELETE'); //method allowed
    }

	public function index_get(){

		if(is_null($_GET["region"]) || empty($_GET["region"])){
			$this->response([
				'status' => false,
				'controller' => 'Scores/gilflux',
				'message' => 'No region provided'
			], 200 );
		}else{
			$region = $_GET["region"];
		}

		if(strtolower($_GET["is_craft"]) == 'true'){
			$is_craft = true;
		}else{
			$is_craft = false;
		}

		$this->load->model('Views_model');

		if($is_craft){
			$table_name = $region.'_Craft_Daily';
		}else{
			$table_name = $region.'_Daily';
		}

		$data = $this->Views_model->get($table_name);


		$this->response([
			'status' => true,
			'info' => $region.' Scores '. ($is_craft ? '(Craftable Only)' : '(All Items)'),
			'data' => $data
		], 200 );
	}

}