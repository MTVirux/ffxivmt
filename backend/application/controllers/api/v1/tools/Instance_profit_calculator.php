<?php
defined ('BASEPATH') OR exit('No direct script access allowed');

require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/RestController.php';
require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/Format.php';
use chriskacerguis\RestServer\RestController;

class Instance_profit_calculator extends RestController{
    
    function __construct() {
        parent::__construct();
        
        Header('Access-Control-Allow-Origin: *'); //for allow any domain, insecure
        Header('Access-Control-Allow-Headers: *'); //for allow any headers, insecure
        Header('Access-Control-Allow-Methods: GET, POST'); //method allowed

    }
    

    //TODO: TEST THIS
    public function index_get(){

        if(empty($_GET["location"])){
            $this->response([
                'status' => false,
                'message' => 'No location provided'
            ], 400);
            return;
        }else{
            $location = $_GET["location"];
        }

        $this->load->model("Scylla/Scylla_Item_model", "Scylla_Items");
        $all_instances = garland_db_get_instances();
        $marketable_item_ids = $this->Scylla_Items->get_marketable_ids();
        $mb_data = array();

        $valid_instances = array();

        $accepted_instance_types = ["Dungeons", "Trials", "Raids"];

        foreach($all_instances["browse"] as $instance_key => $instance){

            if(!in_array($instance["t"], $accepted_instance_types)){
                continue;
            }

            $valid_instances[$instance["i"]]["id"] = $instance["i"];
            $valid_instances[$instance["i"]]["name"] = $instance["n"];
            $valid_instances[$instance["i"]]["min_lvl"] = $instance["min_lvl"];
            $valid_instances[$instance["i"]]["max_lvl"] = $instance["max_lvl"];
            $valid_instances[$instance["i"]]["type"] = $instance["t"];
        }

        foreach($valid_instances as $valid_instance){
            $valid_instance["items"] = [];
            foreach(garland_db_get_instances($valid_instance["id"])["partials"] as $loot_index => $loot){
                if(in_array($loot["id"], $marketable_item_ids)){
                    $valid_instance["items"][$loot["id"]] = [];
                    if(!array_key_exists($loot["id"], $mb_data)){
                        $mb_data[$loot["id"]] = universalis_get_mb_data($location, $loot["id"]);
                    }
                    $valid_instance["items"][$loot["id"]]["minPrice"] = $mb_data[$loot["id"]]["minPrice"];
                    $valid_instance["items"][$loot["id"]]["regularSaleVelocity"] = $mb_data[$loot["id"]]["regularSaleVelocity"];
                }
            };
        }
        echo json_encode($valid_instances);die();
    }
}