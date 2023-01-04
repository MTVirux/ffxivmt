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

	public function transpose_sales_to_ts(){
		set_time_limit(90000);
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

			//Add Universalis Link
			foreach($results as &$row){
				$row["Universalis Link"] = "<a target='_blank' href='https://universalis.app/market/". $row["item_id"]. "'>Link</a>";
			}
			
			
			//Change Headers to Readable Format
			$headers_to_change = [
				"name" => "Name",
				"item_id" => "Item ID",
				"updated_at" => "Updated At",
				"latest_sale" => "Most Recent Sale",
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
		}

		return;
	}

	public function force_update($item_id){
		if(empty($item_id)){
			echo "No item_id provided";
			return;
		}

		pretty_dump($this->Redis_ts->calc_item_score($item_id));
	}

	public function item_product_profit_calculator($item_name = null, $world_or_dc = "Chaos"){

		$item_name = str_replace("_", " ", $item_name);
		$item_name = str_replace("-", "'", $item_name);
		$this->load->library('table');

		$this->load->model("Item_model", "Item");
		$item_id = ($this->Item->get_by_name($item_name)[0]->id);
		$garland_item = garland_get_item($item_id);
		$garland_item_partials = $garland_item["partials"];
		$item_crafts = [];
		foreach($garland_item_partials as $partial){
			if($partial["type"] == "item"){
				$item_crafts[$partial["id"]]["name"] = $partial["obj"]["n"];
			}
		}

		//get all keys
		foreach($item_crafts as $key => $item_craft){
			$item_ids[] = $key;
		}

		$item_ids = implode(",", $item_ids);

		$mb_data = (universalis_get_mb_data('Chaos', $item_ids));
		$mb_treated_data = [];
		foreach($mb_data["items"] as $item_id => $mb_item_data){
			$mb_treated_data[$item_id]["minPrice"] = $mb_item_data["minPrice"];
			$mb_treated_data[$item_id]["regularSaleVelocity"] = $mb_item_data["regularSaleVelocity"];
		}

		//pretty_dump($mb_data);die();

		foreach($mb_treated_data as $item_id => $min_price){
			$product_name = $this->Item->get_item_name($item_id);
			$mb_treated_data[$product_name]["min_price"] = $mb_treated_data[$item_id]["minPrice"];
			$mb_treated_data[$product_name]["regularSaleVelocity"] = $mb_treated_data[$item_id]["regularSaleVelocity"];
			$mb_treated_data[$product_name]["mtvirux_score"] = $mb_treated_data[$product_name]["min_price"] * $mb_treated_data[$product_name]["regularSaleVelocity"];
			$mb_treated_data[$product_name]["id"] = $item_id;
			unset($mb_treated_data[$item_id]);
		}

		$prices = array_column($mb_treated_data, 'mtvirux_score');
		array_multisort($prices, SORT_DESC, $mb_treated_data);

		//pretty_dump($mb_treated_data);die();

		if(!empty($mb_treated_data) && !is_null($mb_treated_data) && count($mb_treated_data) > 0){
			$this->table->set_heading(["Item ID", "Name", "Price", "Universalis Sale Velocity", "MTVirux Score (price * USV)"]);
			$this->table->set_template(array('table_open' => '<table class="table table-striped table-bordered table-hover">'));
			foreach($mb_treated_data as $name=>$row){
				$this->table->add_row([$row["id"], $name, $row["min_price"], $row["regularSaleVelocity"], $row["mtvirux_score"]]);
			}

			//add bootstrap
			$data['item_name'] = $item_name;
			$data['data'] = $this->table->generate();
			$data['raw_data'] = $mb_treated_data;
			$data['world'] = $world_or_dc;
			$this->load_view_template('test/graph2', $data);
		}
	}

	function bicolor_gemstone_profit_calculator(){

		//if(empty($_POST)){
		//	$this->load_view_template('tools/bicolor_gemstone_profit_calculator');
		//	return;
		//}
		
		$this->load->model("Item_model", "Item");
		$item_data = garland_db_get_items(26807);

		$listings = $item_data["item"]["tradeCurrency"][0]["listings"];

		$final_data = [];

		foreach($listings as $listing){
			$final_data[$listing["item"][0]["id"]] = [
				"name" => $this->Item->get_item_name($listing["item"][0]["id"]),
				"id" => $listing["item"][0]["id"],
				"price" => $listing["currency"]["0"]["amount"],
				"currency_id" => $listing["currency"]["0"]["id"],
				"currency_name" => $this->Item->get_item_name($listing["currency"]["0"]["id"]),
		];
			
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
			$mb_data = universalis_get_mb_data('Chaos', $keys_to_send);
			//Add the data to the full data array

			foreach($mb_data["items"] as $item_id => $item_data){
				$final_data[$item_id]["minPrice"] = floatval($item_data["minPrice"]);
				$final_data[$item_id]["regularSaleVelocity"] = $item_data["regularSaleVelocity"];
				$final_data[$item_id]["mtvirux_score"] = floatval($final_data[$item_id]["minPrice"]) * floatval($final_data[$item_id]["regularSaleVelocity"]);
				$final_data[$item_id]["stef_score"] = floatval($final_data[$item_id]["minPrice"]) / floatval($final_data[$item_id]["price"]) * 280;
			}
		}

		foreach($final_data as $item_id => $item_data){
			if(!isset($item_data["mtvirux_score"])){
				unset($final_data[$item_id]);
			}
		}

		//Sort final data by mtvirux score
		$mtvirux_scores = array_column($final_data, 'stef_score');
		array_multisort($mtvirux_scores, SORT_DESC, $final_data);

		pretty_dump($final_data);die();

		
	}




	function currency_efficiency_calculator(){

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

		$this->load->model("Item_model", "Item");

		$currency_id = $this->Item->get_by_name($_POST["currency_id"])[0]->id;
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
					"name" => $this->Item->get_item_name($listing["item"][0]["id"]),
					"id" => $listing["item"][0]["id"],
					"price" => $listing["currency"][0]["amount"],
					"currency_id" => $listing["currency"][0]["id"],
					"currency_name" => $this->Item->get_item_name($listing["currency"][0]["id"]),
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
			"item_name" => $this->Item->get_item_name($currency_id), 
			"item_id" => $currency_id, 
			"location" => $worldDcRegion)
		);
		return $final_data;

		
	}

}
