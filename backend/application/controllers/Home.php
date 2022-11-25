<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Home extends MY_Controller {

	public function __construct(){
		parent::__construct();
		$this->load->model('Item_model', 'Items');
		$this->load->helper('url');
		$this->load->model('Retainer_model', 'Retainers');

		
	}

	public function index()
	{
		$this->load_view_template('home');

	}

	function search(){
        $item = ($this->Items->get($_POST['item_id'])[0]);
		$item->prices = $this->get_universallis_prices($item->id);
        $data['item'] = $item;
		$data['retainer_array'] = $this->get_retainers();
        $this->load_view_template('item_info', $data);
    }


	function materias(){
		//Get items that match "materia" in their name from DB
		$items = $this->Items->get_by_name(array("Craftsman's", "Materia", "X"));
		//foreach item populate prices
		foreach($items as $item){
			$item->prices = $this->get_universallis_prices($item->id);
		}
		//pretty print itemn
		echo '<pre>';
		print_r($items);
		echo '</pre>';
	}

	function get_universallis_prices($item_id){
		//get item price from universallis api
		$url = "https://universalis.app/api/v2/chaos/".$item_id."?entries=10&noGst=1";
		$ch = curl_init();
		curl_setopt($ch, CURLOPT_URL, $url);
		curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
		curl_setopt($ch, CURLOPT_CUSTOMREQUEST, "GET");
		$headers = array();
		curl_setopt($ch, CURLOPT_HTTPHEADER, $headers);
		$result = curl_exec($ch);
		//parse json
		$result = json_decode($result);
		//unix timestamp to date
		$result->lastUploadTime = date('Y-m-d', $result->lastUploadTime);
		foreach($result->listings as $listing){
			$listing->lastReviewTime = date('Y-m-d', $listing->lastReviewTime);
		}

		return $result;
	}

	function get_retainers($filter = null){

		//Get filtered retainers from DB
		$filtered_retainers = $this->Retainers->get($filter);
		return $filtered_retainers;

	}
}