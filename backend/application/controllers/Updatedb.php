<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Updatedb extends MY_Controller {

	public function __construct(){
		parent::__construct();
		$this->load->helper('url');
		ini_set('max_execution_time', 0);
		ini_set('memory_limit', '2048M');
	}


	public function index(){
		$this->update_items();
		$this->update_craft_recipes(true, 0, 999999999);
		$this->update_marketability();
		$this->update_worlds();
		$this->update_sales();
	}

	public function update_marketability(){
		$this->load->model('Scylla/Item_model', 'Scylla_items');
		$marketable_items_ids = universalis_get_marketable_item_ids();

		$marketable_item_count = count($marketable_items_ids);
		$updated_items = 0;

		foreach($marketable_items_ids as $marketable_item_id){

			if($this->Scylla_items->update(array("id" => $marketable_item_id, "marketable" => true))){
				$updated_items++;
				logger("SCYLLA_DB" , json_encode(array("message" => "Item marketability updated", "item_id" => $marketable_item_id)));
			}else{	
				logger("SCYLLA_DB" , json_encode(array("message" => "[ERROR] Failed to update item marketability", "item_id" => $marketable_item_id)));
				die();
			}
		}

		logger("SCYLLA_DB" , json_encode(array("message" => "Item marketability updated", "updated_items" => $updated_items, "total_items" => $marketable_item_count)));

	}

	
	public function update_items(){

		$csv = $this->parse_csv();

		foreach($csv as $item){

			$organized_item = $item;

			//pretty_dump($organized_item);die();

			if(!isset($item['name'])){
				var_dump("Culprit: " . $item['id']);
				logger("SCYLLA_DB", json_encode(array("message" => "Culprit: " . $item['id'], "error")));
				pretty_dump($item);
				die();
			}
			
			$this->load->model('Scylla/Item_model', 'Scylla_items');
			if($this->Scylla_items->add($item)){
				logger("SCYLLA_DB" , json_encode(array("message" => "Item added to database", "item_id" => $item["id"], "item_name" => $item['name'])));
			}else{
				logger("SCYLLA_DB" , json_encode(array("message" => "[ERROR] Failed to add item to DB", "item_id" => $item["id"])));
				die();
			}
		}
	}

	public function update_craft_recipes(){

		$this->load->model('Scylla/Item_model', 'Scylla_items');
		$all_ids = $this->Scylla_items->get_all_ids();
		$ids_to_request = array();

		$total_items = 0;
		$items_with_crafting = 0;
		
		foreach($all_ids as $id){

			$total_items++;

			$ids_to_request[] = $id;
			if(count($ids_to_request) == 100 || $id == 39000){

				$json = file_get_contents('https://www.garlandtools.org/db/doc/item/en/3/' . implode(',', $ids_to_request). '.json');
				$json_decoded = json_decode($json, true);
				$ids_to_request = array();

				foreach($json_decoded as $item){
					if(isset($item["obj"]["item"]["craft"])){

						$items_with_crafting++;

						$craftingComplexity = array();
						$current_item = $this->Scylla_items->get($item["id"])[0];

						foreach($item["obj"]["item"]["craft"] as $key=>$recipe){
							$craftingComplexity[$key] = $recipe["complexity"];
						};

						unset($item["obj"]["item"]["craft"][0]["complexity"]);
						$current_item["craftable"]	=	!empty($item["obj"]["item"]["craft"][0]) ? true : false;

						if($this->Scylla_items->update($current_item)){
							logger("SCYLLA_DB" , "Item recipe updated: " . $current_item["id"] . ' - ' . $current_item['name']);
						}
					}
				}
			}
		}

		logger("SCYLLA_DB" , json_encode(array("message" => "Item craftability updated", "craftable_items" => $items_with_crafting)));

	}

	public function update_sales($reverse = true, $start_at_id= 0, $end_at_id = 999999999){

		$this->load->model('Scylla/Sale_model', 'Sale_model');
		$this->load->model('Scylla/World_model', 'World_model');
		$this->load->model('Scylla/Item_model', 'Item_model');

		$item_names = $this->Item_model->get_name();

		//Get marketable items from universalis
		$marketable_items = boolval($reverse) ? array_reverse(universalis_get_marketable_item_ids()) : universalis_get_marketable_item_ids();
		foreach($marketable_items as $key=>$item){
			if($item < $start_at_id || $item > $end_at_id){
				unset($marketable_items[$key]);
			}
		}

		
		//Split into chunks of 100
		$chunks = array_chunk($marketable_items, 10);
		
		//Make each chunk a string separated by commas
		$chunks = array_map(function($chunk){
			return implode(',', $chunk);
		}, $chunks);

		$regions = $this->World_model->get_regions();

		$total_sales_entries_fulfilled = 0;
		$requests_fullfilled = 0;
		$total_request_count = count($chunks) * count($regions);

		foreach($chunks as $chunk){

			foreach($regions as $region){
				$consolidated_sales_data = array();

				$sales_data = universalis_get_item_sales_data($chunk, $region);
				$requests_fullfilled++;

				foreach($sales_data["items"] as $sales){

					foreach($sales["entries"] as $sale_entry){

						$consolidated_sales_data[] = array(
							"hq" => $sale_entry["hq"],
							"unit_price" => $sale_entry["pricePerUnit"],
							"quantity" => $sale_entry["quantity"],
							"buyer_name" => $sale_entry["buyerName"],
							"on_mannequin" => $sale_entry["onMannequin"],
							"sale_time" => $sale_entry["timestamp"],
							"world_name" => $sale_entry["worldName"],
							"world_id" => $sale_entry["worldID"],
							"total" => $sale_entry["pricePerUnit"] * $sale_entry["quantity"],
							"item_id" => $sales["itemID"],
							"item_name" => $item_names[$sales["itemID"]]
						);

					}


				}

				$this->Sale_model->add_sale($consolidated_sales_data);
			
				logger("UNIVERSALIS_API", "Total fulfilled updated sales requests: " . $requests_fullfilled . "/" . $total_request_count);
				$total_sales_entries_fulfilled = $total_sales_entries_fulfilled + count($consolidated_sales_data);
				logger("UNIVERSALIS_API", "Completed " . $requests_fullfilled . " of " . $total_request_count . " requests  currently @ " . $total_sales_entries_fulfilled . " sales entries");

			}

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
				'id'                    =>	intval(		$item[array_key_first($item)]	),
				'name'                  =>	strval(		$item[10-$element_offset]		),
				'description'           =>	strval(		$item[9-$element_offset]		),
				'can_be_hq'             =>	boolval(	$item[28-$element_offset]		),
				'always_collectible'    =>	strval(		$item[39-$element_offset]		) == "True" ? true : false,
				'stack_size'            =>	intval(		$item[21-$element_offset]		),
				'item_level'            =>	intval(		$item[12-$element_offset]		),
				'icon_image'            =>	intval(		$item[11-$element_offset]		),
				'rarity'                =>	intval(		$item[13-$element_offset]		),
				'filter_group'          =>	intval(		$item[14-$element_offset]		),
				'item_ui_category'      =>	intval(		$item[16-$element_offset]		),
				'item_search_category'  =>	intval(		$item[17-$element_offset]		),
				'equip_slot_category'   =>	intval(		$item[18-$element_offset]		),
				'unique'                =>	boolval(	$item[22-$element_offset]		),
				'untradable'            =>	boolval(	$item[23-$element_offset]		),
				'disposable'            =>	boolval(	$item[24-$element_offset]		),
				'dyable'                =>	boolval(	$item[29-$element_offset]		),
				'aetherial_reductible'  =>	boolval(	$item[40-$element_offset]		),
				'materia_slot_count'    =>	intval(		$item[87-$element_offset]		),
				'advanced_melding'      =>	boolval(	$item[88-$element_offset]		),
				'craftable'       		=> 	false, //Fields got from Garland DB just here to appease the php-cql lib gods
				'marketable'       		=> 	false, //Fields got from Garland DB just here to appease the php-cql lib gods
			);
		
			// Print the data for this item for debugging purposes

			$final_item_data[] = $item_data;
		}
		
		// Close the file
		fclose($file_handle);

		return $final_item_data;
		
	}

	public function update_worlds(){

		$this->load->model('Scylla/World_model', 'Scylla_worlds');
		$worlds = universalis_get_all_worlds();

		$total_worlds = count($worlds);
		$worlds_added = 0;

		foreach($worlds as $world){
			if($this->Scylla_worlds->add($world)){
				$worlds_added++;
			}
		}

		logger("SCYLLA_DB", json_encode(array("message" => "Added " . $worlds_added . " of " . $total_worlds . " worlds")));

	}
}