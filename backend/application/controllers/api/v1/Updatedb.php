<?php
defined ('BASEPATH') OR exit('No direct script access allowed');

require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/RestController.php';
require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/Format.php';
use chriskacerguis\RestServer\RestController;

class updatedb extends RestController{
        
    function __construct() {
        parent::__construct();
        
        Header('Access-Control-Allow-Origin: *'); //for allow any domain, insecure
        Header('Access-Control-Allow-Headers: *'); //for allow any headers, insecure
        Header('Access-Control-Allow-Methods: POST'); //method allowed
    }
    
    public function index_get()
    {   
        echo "GET_request";
    }   

    //TODO: Make it its own controller
    public function python_request_post(){

        set_time_limit(0);
        $sales_data = json_decode($this->input->raw_input_stream, true);
                
        if(empty($sales_data)){
            $this->response([
                'status' => false,
                'message' => 'POST WAS EMPTY'
            ], 400);
            return;
        }

        $consolidated_sales_data = array();
        $updated_items = array();

        $this->load->model('Scylla/Scylla_Item_model', 'Scylla_Items');
        $this->load->model('Scylla/Scylla_World_model', 'Scylla_Worlds');
        $names_array = $this->Scylla_Items->get_all_names();
        $worlds_info = $this->Scylla_Worlds->get();
        $worlds_formatted_info = [];
        foreach($worlds_info as $world_info){
            $worlds_formatted_info[$world_info["id"]] = $world_info;
        }

        //Set $world_id regardless if it is a world or region import
        if(array_key_exists("worldID", $sales_data)){ 
            $world_id = $sales_data["worldID"];
        }else if(array_key_exists("worldId", $sales_data["items"])){
            $world_id = $sales_data["items"][0]["worldId"];
        }
        if(array_key_exists("itemID", $sales_data)){ 
            $item_id = $sales_data["itemID"];
        }else if(array_key_exists("itemId", $sales_data["items"])){
            $item_id = $sales_data["items"][0]["itemId"];
        }
        

        foreach($sales_data["items"] as $item_id => $sale_data){
            foreach($sale_data["entries"] as $sale){

                $sale["worldID"] = $world_id;
                $sale["worldName"] = $worlds_formatted_info[$world_id]["name"];
                $updated_items[$item_id] = $world_id;


                //Regular Sale
                
                //Rename keys
                $sale["buyer_name"] = $sale["buyerName"];
                unset($sale["buyerName"]);
                $sale["on_mannequin"] = $sale["onMannequin"];
                unset($sale["onMannequin"]);
                $sale["world_name"] = $sale["worldName"];
                unset($sale["worldName"]);
                $sale["sale_time"] = $sale["timestamp"];
                unset($sale["timestamp"]);
                $sale["unit_price"] = $sale["pricePerUnit"];
                unset($sale["pricePerUnit"]);
                $sale["world_id"] = $sale["worldID"];
                unset($sale["worldID"]);

                $sale["hq"] = $sale["hq"] == 1 ? True : False;
                $sale["item_id"] = $item_id;
                $sale["total"] = $sale["quantity"] * $sale["unit_price"];
                $sale["item_name"] = $names_array[$item_id];
                $sale["sale_time"] = $sale["sale_time"]*1000;

                $sale["region"] = $worlds_formatted_info[$world_id]["region"];
                $sale["datacenter"] = $worlds_formatted_info[$world_id]["datacenter"];

                $consolidated_sales_data[] = $sale;

                
            }
        }


        logger("SCYLLA_DB", json_encode(array("controller" => "api/v1/updatedb/", "function" => "python_request_post")));

        $this->load->model('Scylla/Sale_model', 'Scylla_Sales');
    
        //Add sales
        $sale_result = $this->Scylla_Sales->add_sale($consolidated_sales_data);
        
        //Update rankings
        foreach($updated_items as $item_id => $world_id){
            $this->gilflux_ranking_update_get($world_id, $item_id);
        }


        $result = array("parsed_sales" => is_null($sale_result["parsed_sales"]) ? 0 : $sale_result["parsed_sales"], "time" => is_null($sale_result["time"]) ? 0 : $sale_result["time"]);
        
        $this->response([
            'status' => true,
            'data' => $result
        ], 200);
    }

    //TODO: Make it its own controller
    public function gilflux_ranking_update_get($world_id, $item_id){
        $this->load->model('Scylla/Scylla_Gilflux_Ranking_model', 'Scylla_Gilflux_Ranking');
        $this->Scylla_Gilflux_Ranking->update_ranking($world_id, $item_id);
        logger("SCYLLA_DB", json_encode(array("controller" => "api/v1/updatedb", "function" => "gilflux_ranking_update_get", "world_id" => $world_id, "item_id" => $item_id)));
    }
}