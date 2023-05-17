<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class GilFlux extends MY_Controller{

	public function __construct(){
		parent::__construct();
		$this->load->helper('url');
		$this->load->model('Scylla/Scylla_Item_model', 'Scylla_Items');
		$this->load->model('Scylla/Scylla_World_model', 'Scylla_Worlds');
		$this->load->model('Scylla/Scylla_Gilflux_Ranking_model', 'Scylla_gilflux_ranking');
	}

	public function gilflux(){

	}


    public function index($target_location, $craft = null, $limit = null, $page = null){

		$target_type = "world";

		$worlds = $this->Scylla_Worlds->get();

		$locations = [];

		foreach($worlds as $world){
			$locations[$world["region"]][$world["datacenter"]][] = $world["name"];
		}


		foreach($locations as $region => $datacenter_data){
			if(strtolower($target_location) == strtolower($region)){
				$target_type = "region";
			}else{
				foreach($locations[$region] as $datacenter => $worlds){
					if(strtolower($target_location) == strtolower($datacenter)){
						$target_type = "datacenter";
					}
				}
			}
		}

		if($target_type == "world"){

			//Get ID
			$world = $this->Scylla_Worlds->get_by_name($target_location);
			$target_location = $world[0]["id"];
			$gilflux_ranking = $this->Scylla_gilflux_ranking->get_by_world($target_location);

		}else if($target_type == "datacenter"){

			$gilflux_ranking = $this->Scylla_gilflux_ranking->get_by_datacenter($target_location);

		}else if($target_type == "region"){

			$gilflux_ranking = $this->Scylla_gilflux_ranking->get_by_region($target_location);

		}

		if($craft){
			//Get all crafted item ids
			$craftable_item_ids = $this->Scylla_Items->get_craftable_items(TRUE);

			foreach($gilflux_ranking as $gilflux_ranking_item_id => $gilflux_ranking_item){
				if(!in_array($gilflux_ranking_item["item_id"], $craftable_item_ids)){
					unset($gilflux_ranking[$gilflux_ranking_item_id]);
				}
			}
		}

		array_multisort(array_column($gilflux_ranking, "ranking_1h"), SORT_DESC, $gilflux_ranking);

		pretty_dump($gilflux_ranking);
		



	}

}