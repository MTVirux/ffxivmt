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
