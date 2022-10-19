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

	public function python_update(){
		if(empty($_POST['item_id']) || is_null($_POST['item_id'])){
			echo "No item_id provided";
			return;
		}
		if(empty($_POST['world_name']) || is_null($_POST['world_name'])){
			echo "No world name provided";
			return;
		}
		logger("PYTHON_UPDATE", "item_id: ".$_POST['item_id']." world_name: ".$_POST['world_name']);

		$item_id = $_POST['item_id'];
		$world = $_POST['world_name'];

		//logger('PYTHON_UPDATE', "Python update for item " . $item_id . " on world " . $world, 'python_update');
		$this->load->config('worlds');
		$worlds_to_use = $this->worlds_to_use();

		//Check if we're tracking the world
		if(in_array($world, $worlds_to_use)){
			$this->Redis_ts->calc_item_score($item_id, $world);
			echo $world.'_'.$item_id. ' updated successfully';
			return true;
		}
		return false;
		
	}

	public function update_item_scores(){
		set_time_limit(600);
		$this->Redis_ts->global_item_score_update();
	}

	public function test_craft() {
		$this->load->model('Item_model', 'Item');
		$craftable_items = $this->Item->get_craftable_items();
		pretty_dump($craftable_items);
	}

	public function worlds_to_use(){
		return (get_worlds_to_use(	
										$this->config->item('worlds_to_use'), 
										$this->config->item('dcs_to_use'), 
										$this->config->item('regions_to_use'), 
										$this->config->item('ffxiv_worlds')
									)
								);
	}

	public function transpose_sales_to_ts(){
		set_time_limit(600);
		$this->Redis_ts->transpose_sales_to_ts();
	}

	public function get_table($world, $craft = null, $limit = null, $page = null){

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
			$this->table->set_heading(array_keys((array)$results[0]));
			$this->table->set_template(array('table_open' => '<table class="table table-striped table-bordered table-hover">'));
			foreach($results as $row){
				$this->table->add_row(array_values((array)$row));
			}

			//add bootstrap
			$data['data'] = $this->table->generate();
			$this->load->view('basic_bootstrap', $data);
		}

		return;
	}
}
