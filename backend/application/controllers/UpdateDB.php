<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class UpdateDB extends CI_Controller {

	public function __construct(){
		parent::__construct();
		$this->load->model('Item_model', 'Items');
		$this->load->helper('url');

		
	}

	public function index()
	{
		echo base_url('resources/item_dump.csv');

		echo '<pre>'; 
		echo 'Starting CSV parse...<br>';
		$csv = array_map('str_getcsv', file(base_url('resources/item_dump.csv')));

		$meta_headers = array_shift($csv);
		$columns = array_shift($csv);
		$data_type = array_shift($csv);

		echo '<table>';
		 

		$this->echo_headers($meta_headers, $columns, $data_type);

		$last_item_id = $this->Items->get_last_item()[0]->id;

		echo 'last_item_id: ' . $last_item_id . '<br>';

		$i = 0;
		foreach($csv as $item){
			if($i >= $last_item_id){
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
				if($i == $last_item_id){
					$this->Items->update($organized_item);
				}else{
					$this->Items->add($organized_item);
				}
			}
			$i = $i +1;
		}

		echo '</table>';
		echo 'DONE';

	}

	function get_price($id){
		echo '';
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
