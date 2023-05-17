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

    public function python_request_post(){

        set_time_limit(0);
        $sales_data = json_decode($this->input->raw_input_stream, true);

        logger("SCYLLA_DB", json_encode(array("controller" => "api/v1/updatedb/python_request_post", "function" => "python_request_post", "post_size" => count($sales_data["items"]))));
        
        if(empty($sales_data)){
            $this->response([
                'status' => false,
                'message' => 'POST WAS EMPTY'
            ], 400);
            return;
        }

        $consolidated_sales_data = array();
        $consolidated_gilflux_data = array();

        $this->load->model('Scylla/Scylla_Item_model', 'Scylla_Items');
        $this->load->model('Scylla/Scylla_World_model', 'Scylla_Worlds');
        $names_array = $this->Scylla_Items->get_all_names();
        $worlds_info = $this->Scylla_Worlds->get();
        $worlds_formatted_info = [];
        foreach($worlds_info as $world_info){
            $worlds_formatted_info[$world_info["id"]] = $world_info;
        }

        foreach($sales_data["items"] as $item_id => $sale_data){
            foreach($sale_data["entries"] as $sale){

                //Regular Sale
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
                
                $sale["item_id"] = $item_id;
                $sale["total"] = $sale["quantity"] * $sale["unit_price"];
                $sale["item_name"] = $names_array[$item_id];
                $sale["sale_time"] = $sale["sale_time"]*1000;
                $consolidated_sales_data[] = $sale;

                //Gilflux Sale

                //Ignore sales that are on mannequins
                if($sale["on_mannequin"] == False){
                    $gilflux_sale["item_id"] = $item_id;
                    $gilflux_sale["item_name"] = $names_array[$item_id];
                    $gilflux_sale["world_id"] = $sale["world_id"];
                    $gilflux_sale["world_name"] = $sale["world_name"];
                    $gilflux_sale["region"] = $worlds_formatted_info[$world_id]["region"];
                    $gilflux_sale["datacenter"] = $worlds_formatted_info[$world_id]["datacenter"];
                    $gilflux_sale["sale_time"] = $sale["sale_time"];
                    $gilflux_sale["total"] = $sale["total"];
                    $consolidated_gilflux_data[] = $gilflux_sale;
                }

                
            }
        }


        logger("SCYLLA_DB", json_encode(array("controller" => "api/v1/updatedb/python_request_post", "function" => "python_request_post", "post_size" => count($sales_data["items"]))));

        $this->load->model('Scylla/Sale_model', 'Scylla_Sales');
        $this->load->model('Scylla/Scylla_Gilflux_model', 'Scylla_Gilflux');
    
        $sale_result = $this->Scylla_Sales->add_sale($consolidated_sales_data);
        $gilflux_result = $this->Scylla_Gilflux->add_sale($consolidated_gilflux_data);

        $result = array("parsed_sales" => $sale_result["parsed_sales"], "parsed_gilflux" => $gilflux_result["parsed_sales"], "time" => $sale_result["time"] + $gilflux_result["time"]);
        
        $this->response([
            'status' => true,
            'data' => $result
        ], 200);
    }
}