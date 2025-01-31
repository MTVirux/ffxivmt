<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Updatedb extends MY_Controller {


	public function __construct(){
		parent::__construct();


		//Set the update id
		$db_update_id = uniqid();
		
		logger("DB_UPDATE_ACTIVATIONS", $db_update_id . "- DB Update Activated", $override_write = true);
		//Load secrets config
		$this->config->load('secrets');
		$config_key = $this->config->item('temp_key');

		$this->load->helper('url');

		if(empty($config_key) || is_null($config_key) || strlen($config_key) < 1 || !isset($config_key)){
			logger("DB_UPDATE_ACTIVATIONS", $db_update_id . "- AUTH ERROR - KEY IS INVALID", $override_write = true);
			die();
		}

		if($config_key !== $_POST["key"]){
			logger("DB_UPDATE_ACTIVATIONS", $db_update_id . "- AUTH ERROR - KEY IS INCORRECT", $override_write = true);
			die();
		}

		logger("DB_UPDATE_ACTIVATIONS", $db_update_id . "- AUTH SUCCESS", $override_write = true);

		ini_set('max_execution_time', 0);
		ini_set('memory_limit', '2048M');

		logger("DB_UPDATE_ACTIVATIONS", $db_update_id . "- DB UPDATE STARTED", $override_write = true);
	}


	public function index(){
		$this->update_worlds();
		$this->item_array = $this->parse_csv();
		$this->update_items();
		$this->update_elastic_items();
		$this->update_items_from_garland();
		//$this->update_shops_from_garland();
		$this->update_marketability();
		$this->fix_gilflux_ranking_missing_names();
	}

	public function update_marketability(){
		$this->load->model('Scylla/Scylla_Item_model', 'Scylla_items');
		$marketable_items_ids = universalis_get_marketable_item_ids();

		$marketable_item_count = count($marketable_items_ids);
		$updated_items = 0;

		foreach($marketable_items_ids as $marketable_item_id){

			if($this->Scylla_items->update(array("id" => $marketable_item_id, "marketable" => true))){
				$updated_items++;
				logger("SCYLLA_DB" , json_encode(array("message" => "Item marketability updated", "item_id" => $marketable_item_id)), $override_write = true);
			}else{	
				logger("SCYLLA_DB" , json_encode(array("message" => "[ERROR] Failed to update item marketability", "item_id" => $marketable_item_id)), $override_write = true);
				die();
			}
		}

		logger("SCYLLA_DB" , json_encode(array("message" => "Item marketability updated", "updated_items" => $updated_items, "total_items" => $marketable_item_count)), $override_write = true);

	}

	
	public function update_items(){

		if(isset($this->item_array)){
			$csv = $this->item_array;
		}else{
			$csv = $this->parse_csv();
		}

		$scylla = $this->load->model('Scylla/Scylla_Item_model', 'Scylla_items');

		foreach($csv as $item){

			$organized_item = $item;

			if(!isset($item['name'])){
				var_dump("Culprit: " . $item['id']);
				logger("SCYLLA_DB", json_encode(array("message" => "Culprit: " . $item['id'], "error")), $override_write = true);
				pretty_dump($item);
				die();
			}
			
			if($this->Scylla_items->add($item)){
				logger("SCYLLA_DB" , json_encode(array("message" => "Item added to Scylla", "item_id" => $item["id"], "item_name" => $item['name'])), $override_write = true);
			}else{
				logger("SCYLLA_DB" , json_encode(array("message" => "[ERROR] Failed to add item to Scylla", "item_id" => $item["id"])), $override_write = true);
				die();
			}
		}
	}

	public function update_elastic_items(){

		if(isset($this->item_array)){
			$csv = $this->item_array;
		}else{
			$csv = $this->parse_csv();
		}

		$this->load->model('Elastic/Elastic_Item_model', 'Elastic_items');

		foreach($csv as $item){

			$elastic_item = array("id" => $item["id"], "name" => $item["name"]);

			if(!isset($elastic_item['name'])){
				var_dump("Culprit: " . $elastic_item['id']);
				logger("ELASTIC_DB", json_encode(array("message" => "Culprit: " . $elastic_item['id'], "error")), $override_write = true);
				pretty_dump($elastic_item);
				die();
			}

			$response = $this->Elastic_items->add($elastic_item);
			
			if($response["result"] == "updated" || $response["result"] == "created"){
				logger("ELASTIC_DB" , json_encode(array("message" => "Item added to Elasticsearch", "item_id" => $elastic_item["id"], "item_name" => $elastic_item['name'])), $override_write = true);
			}else{
				logger("ELASTIC_DB" , json_encode(array("message" => "[ERROR] Failed to add item to Elasticsearch", "item_id" => $elastic_item["id"], "response" => json_encode($response))), $override_write = true);
				die();
			}
		}
	}

	public function update_items_from_garland(){

		$this->load->model('Scylla/Scylla_Item_model', 'Scylla_items');
		$this->load->model('Scylla/Scylla_Shop_model', 'Scylla_shops');
		$all_ids = $this->Scylla_items->get_all_ids();
		sort($all_ids);
		//get last entry from all_ids
		$last_id = end($all_ids);

		$ids_to_request = array();

		$total_items = 0;
		$items_with_crafting = 0;
		$shop_entries = 0;
		
		foreach($all_ids as $id){

			$total_items++;

			$ids_to_request[] = $id;
			if(count($ids_to_request) == 100 || $id == $last_id){

				$json = file_get_contents('https://www.garlandtools.org/db/doc/item/en/3/' . implode(',', $ids_to_request). '.json');
				$json_decoded = json_decode($json, true);
				$ids_to_request = array();

				foreach($json_decoded as $item){
					/**
					 * Update crafting recipes and craftable status
					 */
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
							logger("SCYLLA_DB" , "Item recipe updated: " . $current_item["id"] . ' - ' . $current_item['name'], $override_write = true);
						}
					}

			
				}
			}
		}

		logger("SCYLLA_DB" , json_encode(array("message" => "Item craftability updated", "craftable_items" => $items_with_crafting, "shop_entries" => $shop_entries, "total_items" => $total_items)), $override_write = true);

	}

	public function update_shops_from_garland(){

		$this->load->model("Scylla/Scylla_Shop_model", "Scylla_shops");
		$this->load->model("Scylla/Scylla_Item_model", "Scylla_items");

		//Get all NPCs from Garland
		$all_npcs = json_decode(file_get_contents('https://www.garlandtools.org/db/doc/browse/en/2/npc.json'),true)["browse"];

		$shops = [];
		$all_npc_ids = [];

		foreach($all_npcs as $npc){
			$all_npc_ids[] = $npc["i"];
		}


		//Break the ids into sets of 100
		$npc_id_chunks = array_chunk($all_npc_ids, 100);
		//$npc_id_chunks = array($npc_id_chunks[3]);


		foreach($npc_id_chunks as $npc_id_chunk){
			$npcs_from_chunk = json_decode(file_get_contents('https://www.garlandtools.org/db/doc/npc/en/2/' . implode(',', $npc_id_chunk) . '.json'),true);
			
			
			foreach($npcs_from_chunk as $npc){
				if(isset($npc["obj"]["npc"]["shops"])){

					$shop["npc_id"] = $npc["id"];
					$shop["npc_name"] = $npc["obj"]["npc"]["name"];

					foreach($npc["obj"]["npc"]["shops"] as $npc_shop_id => $npc_shop){


						$shop["shop_id"] = $npc_shop_id;
						$shop["shop_name"] = $npc_shop["name"];

						foreach($npc_shop["entries"] as $npc_shop_entry){

							if(gettype($npc_shop_entry) === "array"){
								$shop["item_id"] = $npc_shop_entry["item"][0]["id"];
								$shop["amount"] = $npc_shop_entry["item"][0]["amount"];
								$shop["currency_id"] = $npc_shop_entry["currency"][0]["id"];
								$shop["price"] = $npc_shop_entry["currency"][0]["amount"];
								
								if(!is_numeric($shop["currency_id"])){
									$shop["currency_name"] = $shop["currency_id"];
								}else{
									$shop["currency_name"] = $this->Scylla_items->get($shop["currency_id"])[0]["name"];
								}
								$shop["item_name"] = $this->Scylla_items->get($shop["item_id"])[0]["name"];

								$shops[] = $shop;
							}else if(gettype($npc_shop_entry) === "integer"){
								//$shop["item_id"] = $npc_shop_entry["item"][0]["id"];
								//$shop["amount"] = $npc_shop_entry["item"][0]["amount"];
								//$shop["currency_id"] = $npc_shop_entry["currency"][0]["id"];
								//$shop["price"] = $npc_shop_entry["currency"][0]["amount"];
								//logger("SCYLLA_DB" , json_encode(array("message" => "shop_record_updated", "shop_name" => $shop["shop_name"], "shop_id" => $shop["shop_id"], "npc_name" => $shop["npc_name"], "npc_id" => $shop["npc_id"], "item_id" => $shop["item_id"], "currency_id" => $shop["currency_id"], "price" => $shop["price"], "amount" => $shop["amount"])), $override_write = true);
								//$shops[] = $shop;
							}else{
								pretty_dump($npc_shop);die();
							}
						}

					}
				}
				foreach($shops as $shop_entry){
					$this->Scylla_shops->add_entry($shop_entry);
					logger("SCYLLA_DB" , json_encode(array("message" => "shop_record_updated", "shop_name" => $shop_entry["shop_name"], "shop_id" => $shop_entry["shop_id"], "npc_name" => $shop_entry["npc_name"], "npc_id" => $shop_entry["npc_id"], "item_id" => $shop_entry["item_id"], "item_name" => $shop_entry["item_name"], "currency_id" => $shop_entry["currency_id"], "currency_name" => $shop_entry["currency_name"], "price" => $shop_entry["price"], "amount" => $shop_entry["amount"])), $override_write = true);
				}
				$shops = [];

			}

		}
	}
	function parse_csv() {

		// Set the file sources
		$item_file_sources = array(
			'https://raw.githubusercontent.com/MTVirux/ffxiv-datamining/master/csv/Item.csv',
			'https://raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/Item.csv'
			// Add more sources as needed
		);
		
		// Array to store file sizes and source names
		$file_sizes = array();
		
		// Open each stream and get the size
		foreach ($item_file_sources as $item_file_source) {
			$stream = fopen($item_file_source, "r");
			
			if ($stream === false) {
				echo "Failed to open stream: $item_file_source\n";
				continue;
			}
		
			// Read the content to calculate size
			$content = stream_get_contents($stream);
			if ($content === false) {
				echo "Failed to read stream: $item_file_source\n";
				fclose($stream);
				continue;
			}
		
			$file_sizes[$item_file_source] = strlen($content);
		
			// Close the stream
			fclose($stream);
		}
		
		// Compare and display results
		if (count($file_sizes) > 0) {
			arsort($file_sizes); // Sort by size in descending order
		
			echo "File sizes (largest to smallest):\n" . "</br>";
			foreach ($file_sizes as $source => $size) {
				echo "$source: $size bytes\n" . "</br>" ;
			}
		
			// Determine the largest and smallest files
			$largest = array_key_first($file_sizes);
			$smallest = array_key_last($file_sizes);
		
			echo "</br>" . '--------------------------------' . "</br>" . "</br>";
			if ($largest === $smallest) {
				echo "All files are the same size.\n";
			} else {
				echo "Largest file: $largest (" . $file_sizes[$largest] . " bytes)\n" . "</br>";
				echo "Smallest file: $smallest (" . $file_sizes[$smallest] . " bytes)\n" . "</br>";
			}
		} else {
			echo "No file sizes could be determined.\n";
		}
		

		$file_handle = fopen($largest, 'r');
		
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
				'from_scrips'       	=> 	false, //Fields got from Garland DB just here to appease the php-cql lib gods
			);
		
			// Print the data for this item for debugging purposes

			$final_item_data[] = $item_data;
		}
		
		// Close the file
		fclose($file_handle);

		return $final_item_data;
		
	}

	public function update_worlds(){

		$this->load->model('Scylla/Scylla_World_model', 'Scylla_worlds');
		$worlds = universalis_get_all_worlds();

		$total_worlds = count($worlds);
		$worlds_added = 0;

		foreach($worlds as $world){
			if($this->Scylla_worlds->add($world)){
				$worlds_added++;
			}
		}

		logger("SCYLLA_DB", json_encode(array("message" => "Added " . $worlds_added . " of " . $total_worlds . " worlds")), $override_write = true);

	}

	public function fix_gilflux_ranking_missing_names(){
		$this->load->model('Scylla/Scylla_Gilflux_Ranking_model', "Gilflux_Ranking");
		$this->load->model('Scylla/Scylla_World_model', "Worlds");
		$this->load->model('Scylla/Scylla_Item_model', "Items");
		$worlds = $this->Worlds->get();

		while (true){
			try {
				logger("SCYLLA_DB", json_encode(array("message" => "Trying to identify a gilflux ranking with a missing name")), $override_write = true);
				$gilflux_missing_name_query = $this->Gilflux_Ranking->get_a_ranking_with_missing_name();
				var_dump($gilflux_missing_name_query);die();
				if(count($gilflux_missing_name_query) == 0){
					break;
				}else{
					$item_id_of_ranking_with_missing_names = $gilflux_missing_name_query[0]["item_id"];
				}
	
				$item_name = $this->Items->get($item_id_of_ranking_with_missing_names)[0]["name"];
	
				foreach($worlds as $world){
					logger("SCYLLA_DB", json_encode(array("message" => "Fixing Gilflux Ranking item name for item " . $item_name . " on " . $world["name"])), $override_write = true);
	
					$world_id = $world["id"];
					$this->Gilflux_Ranking->update_ranking($world_id, $item_id_of_ranking_with_missing_names);
				}
			} catch (\Throwable $th) {
				var_dump($th);
				sleep (10);
			}


		}
	}
}