<?php
defined ('BASEPATH') OR exit('No direct script access allowed');

require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/RestController.php';
require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/Format.php';
use chriskacerguis\RestServer\RestController;

class Currency_profit_calculator extends RestController{

    function __construct() {
        parent::__construct();
		Header('Access-Control-Allow-Origin: *'); //for allow any domain, insecure
		Header('Access-Control-Allow-Headers: *'); //for allow any headers, insecure
		Header('Access-Control-Allow-Methods: GET'); //method allowed
	}


	public function index_get(){
		$this->load->model("Scylla/Scylla_Item_model", "Scylla_Item");
		$this->load->model("Elastic/Elastic_Item_model", "Elastic_Item");
		if(!array_key_exists("currency_id", $_GET) || !array_key_exists("location", $_GET) || !array_key_exists("request_id", $_GET)){
            echo "Please provide the following parameters: currency_id, location, request_id";
			return;
		}

		if(empty($_GET["currency_id"])){
			echo "POST_ERROR: No currency id provided.";
			return;
		}else{
			$currency_id = intval($this->Elastic_Item->get($_GET["currency_id"])["hits"]["hits"][0]["_id"]);
		}

		if(empty($_GET["location"])){
			echo "POST_ERROR: No location provided.";
			return;
		}else{
			$worldDcRegion = $_GET["location"];
		}

		if(empty($_GET["request_id"])){
			$request_id = bin2hex(random_bytes(16));
			return;
		}else{
			$request_id = $_GET["request_id"];
		}

		$item_name = $this->Scylla_Item->get($currency_id)[0]["name"];

		if($final_data = $this->cache->get('currency_efficiency_calculator_'.$currency_id.'_'.$worldDcRegion)){
			echo "here";die();
			echo json_encode(array(
				"status" => "success", 
				"data" => $final_data, 
				"request_id" => $request_id, 
				"item_name" => $this->Scylla_Item->get($currency_id)[0]["name"], 
				"item_id" => $currency_id, 
				"location" => $worldDcRegion)
			);
			logger("API_INFO", "api/v1/item_product_profit_calculator --- GET REQUEST [".$request_id."] for ".$currency_id." on ".$location." handled through cache");
			return $final_data;
		}

		if(!$item_data = $this->cache->get('garland_db_get_items_'.$currency_id)){
			$item_data = garland_db_get_items($currency_id);
		}

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
		$marketable_ids = $this->Scylla_Item->get_marketable_ids();
		foreach($keys as $index => $key){
			if(!in_array($key, $marketable_ids)){
				unset($keys[$index]);
			}
		}


		//Split the keys into arrays of 50 elements
		$keys_array = array_chunk($keys, 50);

		//Var to be populated with all the API results
		$full_mb_data = [];

		$total_gil_moved = 0;

		foreach($keys_array as $key_array){
			//Implode
			$keys_to_send = implode(",", $key_array);
			//Get the data from the API
			$mb_data = universalis_get_mb_data($worldDcRegion, $keys_to_send);
			//Add the data to the full data array
			foreach($mb_data["items"] as $item_id => $item_data){
				
				$extensive_stack_array = [];

				//Pre-calcs
				foreach($item_data["stackSizeHistogram"] as $stack_size => $stack_size_occurences){
					for($i = 0; $i < $stack_size_occurences; $i++){
						$extensive_stack_array[] = $stack_size;
					}
				}

				//Final Calcs
				$final_data[$item_id]["minPrice"] = round(floatval($item_data["minPrice"]),2);
				$final_data[$item_id]["regularSaleVelocity"] = round($item_data["regularSaleVelocity"],2);
				$final_data[$item_id]["medianStackSize"] = array_key_exists(intval(count($extensive_stack_array)/2), $extensive_stack_array) ? $extensive_stack_array[intval(count($extensive_stack_array) / 2)] : 0;
				$final_data[$item_id]["dailyMarketCap"] = round($final_data[$item_id]["regularSaleVelocity"] * $final_data[$item_id]["medianStackSize"] * $final_data[$item_id]["minPrice"],0);
				$final_data[$item_id]["mtvirux_score"] = round((floatval($final_data[$item_id]["minPrice"]) * floatval($final_data[$item_id]["regularSaleVelocity"])) / floatval($final_data[$item_id]["price"]),2);
			}
		}

		foreach($final_data as $item_id => $item_data){
			if(!isset($item_data["mtvirux_score"])){
				unset($final_data[$item_id]);
			}
		}

		foreach($final_data as $item_id => $item_data){
			$total_gil_moved += $item_data["dailyMarketCap"];
		}

		foreach($final_data as $item_id => $item_data){
			$final_data[$item_id]["dailyMarketCapPercent"] = round(($item_data["dailyMarketCap"] / $total_gil_moved) * 100,2);
		}

		foreach($final_data as $item_id => $item_data){
			$final_data[$item_id]["mtvirux_score"] = round($final_data[$item_id]["mtvirux_score"] * $final_data[$item_id]["dailyMarketCapPercent"],2);
		}

		//Sort final data by mtvirux score
		$mtvirux_scores = array_column($final_data, 'mtvirux_score');
		array_multisort($mtvirux_scores, SORT_DESC, $final_data);

		echo json_encode(array(
			"status" => "success", 
			"data" => $final_data, 
			"request_id" => $request_id, 
			"item_name" => $item_name, 
			"item_id" => $currency_id, 
			"location" => $worldDcRegion)
		);
		return $final_data;

        logger("API_INFO", "api/v1/Currency_profit_calculator --- GET REQUEST [".$request_id."] for [".$currency_id."]".$item_name." on ".$location." received");
		
	}

		
}
