<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Updatedb extends MY_Controller {

	public function __construct(){
		parent::__construct();
		$this->load->model('Item_model', 'Items');
		$this->load->helper('url');
		ini_set('max_execution_time', 9000);
		ini_set('memory_limit', '2048M');
	}


	public function index(){
		$this->update_items();
		$this->update_craft_recipes_from_garland_db();
	}

	
	public function update_items()
	{

		$csv = $this->parse_csv();

		logger("ITEM_DB", "Prepping DB for update: " . count($csv) . " items");
		$this->Items->prep_for_update();
		logger("ITEM_DB", "DB ready to recieve update");

		foreach($csv as $item){

			$organized_item = $item;

			//pretty_dump($organized_item);die();

			if(!isset($organized_item['name'])){
				var_dump("Culprit: " . $organized_item['id']);
				logger("ITEM_DB_UPDATE", "Culprit: " . $organized_item['id'], "error");
				pretty_dump($item);
				die();
			}
			
			if($this->Items->add($organized_item)){
				logger("ITEM_DB", json_encode(array("job"=>"Item Entry Update", "item_id" => $organized_item["id"], "item_name" => $organized_item["name"])));
			}
		}

		logger("ITEM_DB", json_encode(array("job" => "Item Entry Update [DONE]")));
	}

	public function verify_item_entries_against_garland_db(){

		//Get an array with numbers from 1 to 39000
		$all_item_ids = range(0, 39000);

		foreach($all_item_ids as $item_id){

			if(in_array($item_id, $this->config->item('preapproved_item_ids'))){
				logger("ITEM_DB", json_encode(array("job" => "Item DB Verification", "message"=>"Skipping item: " . $item_id . " - Preaproved")));
				continue;
			}

			$local_item_data = $this->Items->get($item_id);

			//If local item data name includes Dated, then it's a dated item and we don't need to verify it
			if(strpos($local_item_data->name, 'Dated') !== false){
				logger("ITEM_DB", json_encode(array("job" => "Item DB Verification", "message"=>"Skipping item: " . $item_id . " - Because it is a dated item")));
				continue;
			}
			
			//If local item data has no name, then it's an unused item slot and we don't need to verify it
			if($local_item_data->name == ""){
				logger("ITEM_DB", json_encode(array("job" => "Item DB Verification", "message"=>"Skipping item: " . $item_id . " - Because it is an unused item slot")));
				continue;
			}

			$garland_item_data = garland_db_get_items($item_id);

			//Ignore items with no entry in Garland DB
			if($garland_item_data === False && !empty($local_item_data->name)){
				logger("ITEM_DB_VERIFICATION", "Item: " . $item_id . " has no entry in Garland DB. Skipping.");
				logger("ITEM_DB_VERIFICATION", "Local was name " . $local_item_data->name);
				logger("ITEM_DB_VERIFICATION", "DID LOCAL EXIST: " . !empty($local_item_data->name));
				//pretty_dump($garland_item_data);
				pretty_dump($local_item_data);
				die();
			}
			

			//Adjust garland item data
			$garland_item_data["item"]["icon"] = str_replace("t/", "", $garland_item_data["item"]["icon"]);



			if($garland_item_data["item"]["name"] != $local_item_data->name){
				logger("[ERROR]ITEM_DB", "Name:" . $garland_item_data["item"]["name"] . " != " . $local_item_data["name"] . " for item id: " . $item_id);
				pretty_dump($garland_item_data);
				pretty_dump($local_item_data);
				die();
			}
			//if($garland_item_data["item"]["description"] != $local_item_data->description){
			//	logger("[ERROR]ITEM_DB", "Description:" . $garland_item_data["item"]["description"] . " != " . $local_item_data->description . " for item id: " . $item_id);
			//	pretty_dump($garland_item_data);
			//	pretty_dump($local_item_data);
			//	die();
			//}

			if(intval($garland_item_data["item"]["icon"]) != intval($local_item_data->iconImage)){
				logger("[ERROR]ITEM_DB", "Icon:" . $garland_item_data["item"]["icon"] . " != " . $local_item_data->iconImage . " for item id: " . $item_id);
				pretty_dump($garland_item_data);
				pretty_dump($local_item_data);
				die();
			}

			logger("ITEM_DB", json_encode(array("job" => "Item DB Verification", "item_id" => $item_id, "status" => 1, "message" => "SUCCESS")));
		}

		logger("ITEM_DB", json_encode(array("job" => "Item DB Verification[DONE]"));

	}

	public function update_craft_recipes_from_garland_db(){

		$all_ids = $this->Items->get_all_ids();
		$ids_to_request = array();
		
		foreach($all_ids as $id){
			$ids_to_request[] = $id->id;
			if(count($ids_to_request) == 100 || $id->id == 39000){
				$json = file_get_contents('https://www.garlandtools.org/db/doc/item/en/3/' . implode(',', $ids_to_request). '.json');
				$json_decoded = json_decode($json, true);
				$ids_to_request = array();
				foreach($json_decoded as $item){
					if(isset($item["obj"]["item"]["craft"])){
						$craftingComplexity = array();
						$current_item = $this->Items->get($item["id"]);
						$current_item->craftingRecipe = json_encode($item["obj"]["item"]["craft"]);
						foreach($item["obj"]["item"]["craft"] as $key=>$recipe){
							$craftingComplexity[$key] = json_encode($recipe["complexity"]);
						};
						$current_item->craftingComplexity = json_encode($craftingComplexity);
						$new_item = $this->Items->update($current_item);
						logger("ITEM_DB", json_encode(array("job"=>"GarlandDB Craft Update", "item_id" => $item["id"], "craft_complexity" => $recipe["complexity"])));
					}
				}
			}
		}
	}

	public function update_sales_from_universalis($reverse = false, $start_at_id= null, $end_at_id = null){

		$this->load->model('Redis/Redis_sales_model', 'Redis_sales');

		//Get marketable items from universalis
		$marketable_items = universalis_get_marketable_item_ids();

		//Remove all between the values of start and end ids
		if($start_at_id != null && $end_at_id != null){
			foreach($marketable_items as $key=>$item_id){
				if($item_id < $start_at_id){
					unset($marketable_items[$key]);
				}
				if($item_id > $end_at_id){
					unset($marketable_items[$key]);
				}
			}
		}

		//Reverse if requested
		if(is_null($reverse) || $reverse == true){
			$marketable_items = array_reverse($marketable_items);
		}

		
		//Split into chunks of 100
		$chunks = array_chunk($marketable_items, 50);
		
		//Make each chunk a string separated by commas
		$chunks = array_map(function($chunk){
			return implode(',', $chunk);
		}, $chunks);

		$worlds_to_use = $this->config->item('worlds_to_use');
		$regions_to_use = $this->config->item('regions_to_use');
		$dcs_to_use = $this->config->item('dcs_to_use');

		$total_count_requests = (count($chunks) * count($worlds_to_use)) + (count($chunks) * count($regions_to_use)) + (count($chunks) * count($dcs_to_use));
		$count_requests = 0;

		$total_sales_entries_fulfilled = 0;

		foreach($chunks as $chunk){
			//first and last id string
			$first_id = explode(',', $chunk)[0];
			$last_id = explode(',', $chunk)[count(explode(',', $chunk)) - 1];
			$id_range = '[' . $first_id . '-' . $last_id . ']';
			$consolidated_sales_data = array();


			//Handle Region requests and consolidate data
			foreach($regions_to_use as $region_to_use){
				$sales_data = universalis_get_item_sales_data($chunk, $region_to_use);
				$count_requests++;
				
				logger("UNIVERSALIS_API", "Parsing sales data for " . $id_range . " @ " . $region_to_use);
				foreach($sales_data["items"] as $item_id => $item_entry){
					foreach($item_entry["entries"] as $sale_entry){
						$sale_entry["itemID"] = $item_id;
						$sale_entry["total"] = $sale_entry["quantity"] * $sale_entry["pricePerUnit"];
						$consolidated_sales_data[] = $sale_entry;
					}
				}
				sleep(0.1);
			}

			//Handle Datacenter requests and consolidate sales data
			foreach($dcs_to_use as $dc_to_use){
				$sales_data = universalis_get_item_sales_data($chunk, $dc_to_use);
				$count_requests++;
				foreach($sales_data["items"] as $item_id => $item_entry){
					foreach($item_entry["entries"] as $sale_entry){
						
						$sale_entry["itemID"] = $item_id;
						$sale_entry["total"] = $sale_entry["quantity"] * $sale_entry["pricePerUnit"];
						$consolidated_sales_data[] = $sale_entry;

					}
				}
				sleep(0.1);
			}

			//Handle World Request and consolidate sales data
			foreach($worlds_to_use as $world_to_use){
				$sales_data = universalis_get_item_sales_data($chunk, $world_to_use);
				$count_requests++;
				foreach($sales_data["items"] as $item_id => $item_entry){

					$worldID = $item_entry["worldID"];
					$worldName = get_world_name($worldID, $this->config->item('ffxiv_worlds'));
					foreach($item_entry["entries"] as $sale_entry){
						$sale_entry["worldID"] = $worldID;
						$sale_entry["worldName"] = $worldName;
						$sale_entry["total"] = $sale_entry["quantity"] * $sale_entry["pricePerUnit"];
						$sale_entry["itemID"] = $item_id;

						$consolidated_sales_data[] = $sale_entry;
					}
				}
				sleep(0.1);
			}

			$this->Redis_sales->add_sale($consolidated_sales_data);
			logger("UNIVERSALIS_API", "Total fulfilled updated sales requests: " . $count_requests . "/" . $total_count_requests);
			$total_sales_entries_fulfilled = $total_sales_entries_fulfilled + count($consolidated_sales_data);
			logger("UNIVERSALIS_API", "Completed " . $count_requests . " of " . $total_count_requests . " requests  currently @ " . $total_sales_entries_fulfilled . " sales entries");
			$consolidated_sales_data = array();
		}

		return true;
	}
	

	function parse_csv() {

		// Set the file to be read
		$file = 'https://raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/Item.csv';
		
		// Open the file for reading
		$file_handle = fopen($file, 'r');
		
		// Set the delimiter to be a comma
		$delimiter = ',';
		
		// Read the first line of the file, which contains the field names
		$field_names = fgetcsv($file_handle, 0, $delimiter);
		fgetcsv($file_handle, 0, $delimiter);
		fgetcsv($file_handle, 0, $delimiter);
		fgetcsv($file_handle, 0, $delimiter);
		
		$final_item_data = [];

		// Loop through each line of the file
		while (($line = fgetcsv($file_handle, 0, $delimiter)) !== false) {
			// Create an array to store the data for this line
			$item = array();
		
			// Loop through each field in this line
			for ($i = 0; $i < 92; $i++) {
				// Check if the field is a string, and if so, remove the double quotes from the beginning and end
				if (is_string($line[$i])) {
					$line[$i] = trim($line[$i], '"');
				}
		
				// Add the data for this field to the item array, using the field name as the key
				$item[$field_names[$i]] = $line[$i];
			}
		
			// Fit the data for this item into the desired schema
			$element_offset = 1;

			$item_data = array(
				"id" => 					intval(		$item[array_key_first($item)]	),
				"name" => 								$item[10-$element_offset],
				"description" => 						$item[9-$element_offset],
				"canBeHQ" => 				boolval(	$item[28-$element_offset]						),
				"alwaysCollectible" => 					$item[39-$element_offset],
				"stackSize" => 				intval(		$item[21-$element_offset]						),
				"itemLevel" => 				intval(		$item[12-$element_offset]						),
				"iconImage" => 				intval(		$item[11-$element_offset]						),
				"rarity" => 				boolval(	$item[13-$element_offset]						),
				"filterGroup" => 			boolval(	$item[14-$element_offset]						),
				"itemUICategory" => 		boolval(	$item[16-$element_offset]						),
				"itemSearchCategory" => 	boolval(	$item[17-$element_offset]						),
				"equipSlotCategory" => 		boolval(	$item[18-$element_offset]						),
				"unique" => 				boolval(	$item[22-$element_offset]						),
				"untradable" => 						$item[23-$element_offset],
				"disposable" => 			boolval(	$item[24-$element_offset]						),
				"dyable" => 				boolval(	$item[29-$element_offset]						),
				"aetherialReductible" =>	boolval(	$item[40-$element_offset]						),
				"materiaSlotCount" => 		boolval(	$item[87-$element_offset]						),
				"advancedMelding" => 		boolval(	$item[88-$element_offset]						),
			);
		
			// Print the data for this item for debugging purposes

			$final_item_data[] = $item_data;
		}
		
		// Close the file
		fclose($file_handle);

		return $final_item_data;
		
	}
}