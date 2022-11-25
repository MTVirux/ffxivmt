<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class GilFlux extends MY_Controller{

	public function __construct(){
		parent::__construct();
		$this->load->helper('url');
		$this->load->model('/Redis/Redis_timeseries_model', 'Redis_ts');
	}

	public function gilflux(){

	}


    public function index($world, $craft = null, $limit = null, $page = null){

		if(is_null($limit)) $limit = 50;
		if(is_null($page)) $page = 0;

		$this->load->model('Views_model', 'Views');
		$this->load->library('table');

		if(!empty($world)){
			$world = ucfirst($world);
		}else{
			echo 'No world name provided';
			return;
		}

		if(!empty($craft)){
			$table_name = $world . '_Craft_Daily';
		}else{
			$table_name = $world . '_Daily';
		}


		do{

			$refresh_grace_period_seconds = 60*5; # 5 minutes

			$current_results = ($this->Views->get($table_name, $limit, $page));
			$current_results_ids = array_column($current_results, 'item_id');

			//update the item score for each of the results
			foreach($current_results as $current_result){
				//if timestamp shows updated_at is older than 30 minutes, update the score
				if(strtotime($current_result['updated_at']) < (time() - $refresh_grace_period_seconds)){
					$this->Redis_ts->calc_item_score($current_result['item_id'], $world);
				}
			}

			$updated_results = ($this->Views->get($table_name, $limit, $page));
			$updated_results_ids = array_column($updated_results, 'item_id');

		}while($current_results_ids != $updated_results_ids);

		$results = $updated_results;

		if(!empty($results) && !is_null($results) && count($results) > 0){

			//Add Universalis Link
			foreach($results as &$row){
				$row["universalis_market_link"] = "<a target='_blank' href='https://universalis.app/market/". $row["item_id"]. "'>Link</a>";
			}
			
			
			//Change Headers to Readable Format
			$headers_to_change = [
				"name" 				=> "Name",
				"item_id" 			=> "Item ID",
				"updated_at" 		=> "Updated At",
				"latest_sale" 		=> "Most Recent Sale",
				"universalis_market_link" 	=> "Universalis Market Link" 
			];

			$encoded_results = json_encode($results);

			foreach($headers_to_change as $key => $value){
				$encoded_results = str_replace($key, $value, $encoded_results);
			}
			$human_results = json_decode($encoded_results);

			$this->table->set_heading(array_keys((array)$human_results[0]));
			$this->table->set_template(array('table_open' => '<table class="table table-striped table-bordered table-hover">'));
			foreach($results as $row){
				$this->table->add_row(array_values((array)$row));
			}

			//add bootstrap
			$data['data'] = $this->table->generate();
			$data['raw_data'] = $results;
			$data['world'] = $world;
			$this->load_view_template('test/graph', $data);

			//pretty_dump($results);	
			return $results;
		}

		return;
	}

}