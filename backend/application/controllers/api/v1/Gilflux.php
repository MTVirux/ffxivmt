<?php
defined('BASEPATH') OR exit('No direct script access allowed');

require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/RestController.php';
require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/Format.php';
use chriskacerguis\RestServer\RestController;

class Gilflux extends RestController{

    public function __construct(){
        parent::__construct();            
		Header('Access-Control-Allow-Origin: *'); //for allow any domain, insecure
		Header('Access-Control-Allow-Headers: *'); //for allow any headers, insecure
		Header('Access-Control-Allow-Methods: GET,POST,PUT,DELETE'); //method allowed
    }

	public function index_get($target_location = null , $craft = null){


		//Check if target location is provided
		if(is_null($target_location) || empty($target_location)){
			if(is_null($_GET['target_location'] || empty($_GET['target_location']))){
				$this->response(["status" => false, "message" => "No target location provided"], 400);
			}else{
				$target_location = $_GET['target_location'];
			}
		}

		


		//Check if requests wants craftable items only
		if(is_null($craft)){
			if(isset($_GET["crafted_only"]) && !empty($_GET["crafted_only"]) && ($_GET["crafted_only"] != "false") && ($_GET["crafted_only"] != "0")){
				$craft = TRUE;
			}else{
				$craft = FALSE;
			}
		}else if($craft == "false"){
			$craft = FALSE;
		}

		$cache_string = 'gilflux_ranking_'.$target_location;
		$cache_type = '_'.($craft ? 'crafted_only' : 'all');

		if($gilflux_ranking = $this->cache->get($cache_string.$cache_type)){
			$this->response([
				'status' => true,
				'message' => $cache_string . $cache_type . ' retrieved from cache',
				'data' => json_encode($gilflux_ranking),
				"gilflux_timeframe_in_ms" => json_encode($this->config->item('gilflux_timeframes_ms')),
				"request_id" => isset($_GET["request_id"]) ? $_GET["request_id"] : null
			], 200);
		}

		//Load models
		$this->load->model('Scylla/Scylla_Item_model', 'Scylla_Items');
		$this->load->model('Scylla/Scylla_World_model', 'Scylla_Worlds');
		$this->load->model('Scylla/Scylla_Gilflux_Ranking_model', 'Scylla_gilflux_ranking');

		//Defaults
		$target_type = "world";
		$worlds = $this->Scylla_Worlds->get();
		$locations = [];
		$gilflux_ranking = [];


		//Structure worlds by region and datacenter
		foreach($worlds as $world){
			$locations[$world["region"]][$world["datacenter"]][$world["id"]] = $world["name"];
		}
		//Assert target_location
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

		//Get gilflux ranking based on the location requested
		if($target_type == "world"){

			//Convert world to ID
			$world = $this->Scylla_Worlds->get_by_name($target_location);
			$target_location = $world[0]["id"];
			
			$gilflux_ranking = $this->Scylla_gilflux_ranking->get_by_world($target_location);


		}else if($target_type == "datacenter"){

			foreach($locations as $region_name => $region){

				foreach($region as $datacenter_name => $datacenter){

					if($datacenter_name == $target_location){

						foreach($datacenter as $world_id => $world_name){

							//Fetch world_gilflux if it isn't in cache from another request
							if(!($world_gilflux = $this->cache->get('gilflux_ranking_'.$world_name.'_all'))){
								$world_gilflux = $this->Scylla_gilflux_ranking->get_by_world($world_id);
							}

							//Merge with gilflux_ranking array
							$gilflux_ranking = array_merge($gilflux_ranking, $world_gilflux);

						}
					}
				}
			}
		

		}else if($target_type == "region"){

			foreach($locations as $region_name => $region){

				if($region_name == $target_location){

					foreach($region as $datacenter_name => $datacenter){

						if($datacenter_gilflux = ($this->cache->get('gilflux_ranking_'.$datacenter_name.'_all'))){
						
							$gilflux_ranking = array_merge($gilflux_ranking, $datacenter_gilflux);

						}else{

							foreach($datacenter as $world_id => $world_name){

								//Fetch world_gilflux if it isn't in cache from another request
								if(!($world_gilflux = $this->cache->get('gilflux_ranking_'.$world_name.'_all'))){
									$world_gilflux = $this->Scylla_gilflux_ranking->get_by_world($world_id);
								}

								//Merge with gilflux_ranking array
								$gilflux_ranking = array_merge($gilflux_ranking, $world_gilflux);

							}

						}
					}
				}
			}
		}
		
		$this->cache->save($cache_string.'_all', $gilflux_ranking, $this->config->item('cache_timers')['gilflux_ranking']);

		//Filter out non-craftable items based on request
		if($craft){
			//Get all crafted item ids
			$craftable_item_ids = $this->Scylla_Items->get_craftable_items(TRUE);
			foreach($gilflux_ranking as $gilflux_ranking_item_array_index => $gilflux_ranking_item){
				if(!in_array($gilflux_ranking_item["item_id"], $craftable_item_ids)){
					unset($gilflux_ranking[$gilflux_ranking_item_array_index]);
				}
			}
		}

		$this->cache->save($cache_string.'_crafted_only', $gilflux_ranking, $this->config->item('cache_timers')['gilflux_ranking']);


		$this->response([
			"status" => true,
			"message" => "Success",
			"data" => json_encode($gilflux_ranking),
			"gilflux_timeframe_in_ms" => json_encode($this->config->item('gilflux_timeframes_ms')),
			"request_id" => isset($_GET["request_id"]) ? $_GET["request_id"] : null
		]);

	}

	//private function remove_outdated_gilflux_ranking_times($gilflux_ranking){
	//
	//	$gilflux_timeframes_in_ms = $this->config->item('gilflux_timeframes_ms');
	//	$current_time_in_ms = time() * 1000;
	//	$total_entries_count = count($gilflux_ranking);
	//
	//	//Check if they're updated
	//	foreach($gilflux_ranking as $gilflux_ranking_item_key => $gilflux_ranking_item){
	//		$item_id = $gilflux_ranking_item["item_id"];
	//		$skip  = false;
	//
	//		foreach($gilflux_timeframes_in_ms as $caption => $gilflux_timeframe_in_ms){
	//			
	//			//If the last sale time is null, set it to the updated_at time
	//			if(!isset($gilflux_ranking_item["last_sale_time"]) || is_null($gilflux_ranking_item["last_sale_time"] || empty($gilflux_ranking_item["last_sale_time"]) || ($gilflux_ranking_item["last_sale_time"] == 0))){
	//				$gilflux_ranking_item["last_sale_time"] = $gilflux_ranking_item['updated_at'];
	//			}
	//
	//			//If the last sale time is older than the timeframe, set the ranking for that timeframe and all the ones after to 0
	//			if(($current_time_in_ms - $gilflux_ranking_item["last_sale_time"]) > $gilflux_timeframe_in_ms ){
	//				$gilflux_ranking[$gilflux_ranking_item_key]['ranking_'.$caption] = 0;
	//				//logger('debug', '['. ($current_time_in_ms - $gilflux_ranking_item["last_sale_time"]) . ' > ' .  ($current_time_in_ms - $gilflux_timeframe_in_ms) . '] Gilflux ranking for item '.$item_id.' is outdated on range of '.$caption.'.');
	//			}else{
	//				//logger('debug', 'Gilflux ranking for item '.$item_id.' on world '.$world_id.' is OK on range of '.$caption.'.');
	//			}
	//		}
	//		
	//	}
	//
	//	return $gilflux_ranking;
	//
	//}

}