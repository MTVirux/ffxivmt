<?php
defined ('BASEPATH') OR exit('No direct script access allowed');

require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/RestController.php';
require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/Format.php';
use chriskacerguis\RestServer\RestController;

class Item_product_profit_calculator extends RestController{
    
    function __construct() {
        parent::__construct();
        
        Header('Access-Control-Allow-Origin: *'); //for allow any domain, insecure
        Header('Access-Control-Allow-Headers: *'); //for allow any headers, insecure
        Header('Access-Control-Allow-Methods: GET, POST'); //method allowed

    }
    
    public function index_get(){
        $search_term = $_GET["search_term"];
        if(is_null($search_term) || empty($search_term)){
            logger("API_ERROR", "api/v1/item_product_profit_calculator --- GET REQUEST FAILED: Missing: search_term field");
            return $this->response([
                'status' => false,
                'message' => "GET request failed, please try again. Missing: search_term field",
            ], 400);
        }

        $location = $_GET["location"];
        if(is_null($location) || empty($location)){
            logger("API_ERROR", "api/v1/item_product_profit_calculator --- GET REQUEST FAILED: Missing: location field");
            return $this->response([
                'status' => false,
                'message' => "GET request failed, please try again. Missing: location field",
            ], 400);
        }

        $request_id = $_GET["request_id"];
        if(is_null($request_id) || empty($request_id)){
            //Give it a random hex string
            $request_id = bin2hex(random_bytes(16));
        }

        logger("API_INFO", "api/v1/item_product_profit_calculator --- Request [".$request_id."] for ".$search_term." on ".$location." received");

        $search_term = str_replace("_", " ", $search_term);
        $search_term = str_replace("-", "'", $search_term);
        $this->load->library('table');

        $this->load->model("Item_model", "Item");
        $item_id = ($this->Item->get_by_name($search_term)[0]->id);
        $garland_item = garland_db_get_items($item_id);
        $garland_item_partials = $garland_item["partials"];
        $item_crafts = [];
        foreach($garland_item_partials as $partial){
            if($partial["type"] == "item"){
                $item_crafts[$partial["id"]]["name"] = $partial["obj"]["n"];
            }
        }

        //get all keys
        foreach($item_crafts as $key => $item_craft){
            $item_ids[] = $key;
        }

        $item_ids = implode(",", $item_ids);

        $mb_data = (universalis_get_mb_data($location, $item_ids));
        if(is_null($mb_data) || empty($mb_data)){
            unset($data);
            $data['status'] 	= "UNIVERSALIS API ERROR";
            $data['message']	= "There was an error with the Universalis API, please try again later.";
            $data['debug'] 		= var_dump($mb_data);
            logger("API_ERROR", "api/v1/item_product_profit_calculator --- GET REQUEST FAILED: Missing: mb_data field");
            echo json_encode($data);
            return;
        }
        $mb_treated_data = [];
        foreach($mb_data["items"] as $item_id => $mb_item_data){
            $mb_treated_data[$item_id]["minPrice"] = $mb_item_data["minPrice"];
            $mb_treated_data[$item_id]["regularSaleVelocity"] = $mb_item_data["regularSaleVelocity"];
        }

        //pretty_dump($mb_data);die();

        foreach($mb_treated_data as $item_id => $min_price){
            $product_name = $this->Item->get_item_name($item_id);
            $mb_treated_data[$product_name]["min_price"] = $mb_treated_data[$item_id]["minPrice"];
            $mb_treated_data[$product_name]["regularSaleVelocity"] = $mb_treated_data[$item_id]["regularSaleVelocity"];
            $mb_treated_data[$product_name]["ffmt_score"] = $mb_treated_data[$product_name]["min_price"] * $mb_treated_data[$product_name]["regularSaleVelocity"];
            $mb_treated_data[$product_name]["id"] = $item_id;
            $mb_treated_data[$product_name]["name"] = $product_name;
            unset($mb_treated_data[$item_id]);
        }

        $prices = array_column($mb_treated_data, 'ffmt_score');
        array_multisort($prices, SORT_DESC, $mb_treated_data);

        //pretty_dump($mb_treated_data);die();

        if(!empty($mb_treated_data) && !is_null($mb_treated_data) && count($mb_treated_data) > 0){

            //add bootstrap
            $data['status'] = "success";
            $data['item_name'] = $search_term;
            $data['item_id'] = $item_id;
            $data['location'] = $location;
            $data['request_id'] = $request_id;
            $data['data'] = $mb_treated_data;

            logger("API_INFO", "api/v1/item_product_profit_calculator --- Request [".$request_id."] for ".$product_name." on ".$location." SUCCESS");
            
            $this->response([
                'status' => true,
                'data' => $data
            ], 200 );

        }else{

            unset($data);
            $data['status'] 	= "Could not fetch MB Data from Universalis";
            $data['message']	= "Could not fetch MB Data from Universalis. Please try again later.";
            $this->response([
                'status' => false,
                'message' => $data
            ], 200 );
            logger("API_ERROR", "api/v1/item_product_profit_calculator --- Request [".$request_id."] for ".$product_name." on ".$location." FAIL");
            return;

        }
    }
}