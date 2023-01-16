<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Test extends MY_Controller {

	public function __construct(){
		parent::__construct();
		$this->load->helper('url');
	}

	public function index()
	{

		Header('Access-Control-Allow-Origin: *'); //for allow any domain, insecure
		Header('Access-Control-Allow-Headers: *'); //for allow any headers, insecure
		Header('Access-Control-Allow-Methods: GET'); //method allowed
		echo json_encode('ok');
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

		logger("PYTHON_UPDATE", json_encode(array("item_id" => $_POST['item_id'], "world_name" => $_POST['world_name'])));

		$item_id = $_POST['item_id'];
		$world = $_POST['world_name'];

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





	public function currency_efficiency_calculator(){
		if(!array_key_exists("currency_id", $_POST) || !array_key_exists("location", $_POST) || !array_key_exists("request_id", $_POST)){
			$this->load_view_template('tools/currency_efficiency_calculator');
			return;
		}

		if(empty($_POST["currency_id"])){
			echo "POST_ERROR: No currency id provided.";
			return;
		}

		if(empty($_POST["location"])){
			echo "POST_ERROR: No location provided.";
			return;
		}

		if(empty($_POST["request_id"])){
			echo "POST_ERROR: No request_id provided.";
			return;
		}

		$this->load->model("Scylla/Scylla_Item_model", "Scylla_Item");
		$this->load->model("Elastic/Elastic_Item_model", "Elastic_Item");

		$currency_id = intval($this->Elastic_Item->get($_POST["currency_id"])["hits"]["hits"][0]["_id"]);
		$worldDcRegion = $_POST["location"];
		$request_id = $_POST["request_id"];

		$item_data = garland_db_get_items($currency_id);

		//pretty_dump($item_data);die();

		//pretty_dump(($item_data["item"]["tradeCurrency"][0]["listings"]));

		$shops = $item_data["item"]["tradeCurrency"];


		$final_data = [];

		foreach($shops as $shop_index => $shop){
			$listings = $item_data["item"]["tradeCurrency"][$shop_index]["listings"];
			//pretty_dump($shop_index);
			foreach($listings as $listing){
				//pretty_dump($listing);die();
				$final_data[$listing["item"][0]["id"]] = [
					"name" => $this->Scylla_Item->get($listing["item"][0]["id"])[0]["name"],
					"id" => $listing["item"][0]["id"],
					"price" => $listing["currency"][0]["amount"],
					"currency_id" => $listing["currency"][0]["id"],
					"currency_name" => $this->Scylla_Item->get($listing["currency"][0]["id"])[0]["name"],
				];
				//pretty_dump($final_data);die();
			}
		}

		//Grab all the array keys from $final_data
		$keys = array_keys($final_data);
		
		//Filter out untradable
		$marketable_ids = universalis_get_marketable_item_ids();

		foreach($keys as $index => $key){
			if(!in_array($key, $marketable_ids)){
				unset($keys[$index]);
			}
		}


		//Split the keys into arrays of 50 elements
		$keys_array = array_chunk($keys, 50);

		//Var to be populated with all the API results
		$full_mb_data = [];

		foreach($keys_array as $key_array){
			//Implode
			$keys_to_send = implode(",", $key_array);
			//Get the data from the API
			$mb_data = universalis_get_mb_data($worldDcRegion, $keys_to_send);
			//Add the data to the full data array

			foreach($mb_data["items"] as $item_id => $item_data){
				$final_data[$item_id]["minPrice"] = floatval($item_data["minPrice"]);
				$final_data[$item_id]["regularSaleVelocity"] = $item_data["regularSaleVelocity"];
				$final_data[$item_id]["mtvirux_score"] = floatval($final_data[$item_id]["minPrice"]) * floatval($final_data[$item_id]["regularSaleVelocity"]);
			}
		}

		foreach($final_data as $item_id => $item_data){
			if(!isset($item_data["mtvirux_score"])){
				unset($final_data[$item_id]);
			}
		}

		//Sort final data by mtvirux score
		$mtvirux_scores = array_column($final_data, 'mtvirux_score');
		array_multisort($mtvirux_scores, SORT_DESC, $final_data);

		echo json_encode(array(
			"status" => "success", 
			"data" => $final_data, 
			"request_id" => $request_id, 
			"item_name" => $this->Scylla_Item->get($currency_id)[0]["name"], 
			"item_id" => $currency_id, 
			"location" => $worldDcRegion)
		);
		return $final_data;

		
	}

}
