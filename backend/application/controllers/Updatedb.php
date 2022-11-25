<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Updatedb extends MY_Controller {

	public function __construct(){
		parent::__construct();
		$this->load->model('Item_model', 'Items');
		$this->load->helper('url');
		ini_set('max_execution_time', 9000);
	}

	public function no()
	{
		echo base_url('resources/item_dump.csv');

		echo '<pre>'; 
		echo 'Starting CSV parse...<br>';
		$csv = array_map('str_getcsv', file(base_url('resources/item_dump.csv')));

		$meta_headers = array_shift($csv);
		$columns = array_shift($csv);
		#$data_type = array_shift($csv);

		$this->Items->prep_for_update();
		echo "Prepped for update<br>";

		echo '<table>';
		 
		//assign last index of $csv to last_item_id
		$last_item_id = $csv[count($csv) - 1][0];

		echo 'last_item_id: ' . $last_item_id . '<br>';
		$i = 0;
		$start_from = 0;
		$entries_to_ignore = [];
		foreach($csv as $item){
			if($i <= $last_item_id && $i >= $start_from && !in_array($i, $entries_to_ignore)){
				$organized_item = array(
					"id" => $item[0],
					"name" => $item[10],
					"description" => $item[9],
					"canBeHQ" => $item[28],
					"alwaysCollectible" => $item[39],
					"stackSize" => $item[21],
					"itemLevel" => $item[12],
					"iconImage" => $item[11],
					"rarity" => $item[13],
					"filterGroup" => $item[14],
					"itemUICategory" => $item[16],
					"itemSearchCategory" => $item[17],
					"equipSlotCategory" => $item[18],
					"unique" => $item[22],
					"untradable" => $item[23],
					"disposable" => $item[24],
					"dyable" => $item[29],
					"aetherialReductible" => $item[40],
					"materiaSlotCount" => $item[87],
					"advancedMelding" => $item[88]
				);

				if(!isset($organized_item['name'])){
					var_dump("Culprit: " . $organized_item['id']);
					pretty_dump($item);
					die();
				}
				
				if($this->Items->add($organized_item)){
					logger("ITEM_DB_ENTRY", "item_id: " . $organized_item["id"]);
				}
			}
			$i = $i +1;
		}

		echo '</table>';
		echo 'DONE';

		$this->update_craft_recipes_from_garland_db();
		$this->update_marketability_from_universalis();
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
						logger("ITEM_CRAFTING_UPDATE", "item_id: " . $item["id"]);
					}
				}
			}
		}
	}

	function update_marketability_from_universalis(){
		$json = file_get_contents('https://universalis.app/api/v2/marketable');
		$json_decoded = json_decode($json, true);
		pretty_dump($json_decoded);

	}

	function echo_headers($meta_headers, $columns, $data_type){
		array_shift($meta_headers);
		echo '<tr>';
		foreach($meta_headers as $i){
			echo '<td>'; echo $i; echo '</td>';
		}
		echo '</tr>';
		echo '<tr>';
		foreach($columns as $i){
			echo '<td>'; echo $i; echo '</td>';
		}
		echo '</tr>';
		echo '<tr>';
		foreach($data_type as $i){
			echo '<td>'; echo $i; echo '</td>';
		}
		echo '</tr>';
		
		
		return;
	}
}
