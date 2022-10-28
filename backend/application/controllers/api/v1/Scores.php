<?php
defined('BASEPATH') OR exit('No direct script access allowed');

require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/RestController.php';
require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/Format.php';
use chriskacerguis\RestServer\RestController;

class Scores extends RestController{

    function __construct(){
        parent::__construct();            
		Header('Access-Control-Allow-Origin: *'); //for allow any domain, insecure
		Header('Access-Control-Allow-Headers: *'); //for allow any headers, insecure
		Header('Access-Control-Allow-Methods: GET,POST,PUT,DELETE'); //method allowed
    }

	public function index_get(){
		$this->response([
			'status' => true,
			'controller' => 'Scores',
			'message' => $this->world_scores_get()
		], 200 );

	}

	public function world_scores_get($world = NULL, $is_craft = NULL){

		if(is_null($world) || empty($world)){
			$this->response([
				'status' => false,
				'controller' => 'Scores/world_scores_get',
				'message' => 'No world provided'
			], 200 );
		}

		if(is_null($is_craft) || empty($is_craft)){
			$is_craft = false;
		}else{
			$is_craft = true;
		}

		$this->load->model('Views_model');

		if($is_craft){
			$table_name = $world.'_Craft_Daily';
		}else{
			$table_name = $world.'_Daily';
		}

		$data = $this->Views_model->get($table_name);


		$this->response([
			'status' => true,
			'info' => $world.' Scores '. ($is_craft ? '(Craftable Only)' : 'Normal'),
			'data' => $data
		], 200 );
	}


	public function dc_scores_get($dc_name, $is_craft){
		
		if(is_null($dc_name) || empty($dc_name)){
			$this->response([
				'status' => false,
				'controller' => 'Scores/dc_scores_get',
				'message' => 'No dc_name provided'
			], 200 );
		}

		if(is_null($is_craft) || empty($is_craft)){
			$is_craft = false;
		}else{
			$is_craft = true;
		}

		$this->load->model('Views_model');

		if($is_craft){
			$table_name = $dc_name.'_Craft_Daily';
		}else{
			$table_name = $dc_name.'_Daily';
		}

		$data = $this->Views_model->get($table_name);
	}


}