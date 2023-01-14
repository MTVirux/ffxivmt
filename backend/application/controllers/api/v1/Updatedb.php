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

        $sales_data = json_decode($this->input->raw_input_stream, true);

        logger("SCYLLA_DB", json_encode(array("controller" => "api/v1/updatedb/python_request_post", "function" => "python_request_post", "post_size" => sizeof($sales_data["items"]))));
        
        if(empty($sales_data)){
            $this->response([
                'status' => false,
                'message' => 'POST WAS EMPTY'
            ], 400);
            return;
        }

        $consolidated_sales_data = array();

        $this->load->model('Scylla/Item_model', 'Items');
        $names_array = $this->Items->get_all_names();

        foreach($sales_data["items"] as $item_id => $sale_data){
            foreach($sale_data["entries"] as $sale){

                //pretty_dump($sale);die();

                
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
                $consolidated_sales_data[] = $sale;
            }
        }


        logger("SCYLLA_DB", json_encode(array("controller" => "api/v1/updatedb/python_request_post", "function" => "python_request_post", "post_size" => sizeof($sales_data))));

        $this->load->model('Scylla/Sale_model', 'Sales');

        $result = $this->Sales->add_sale($consolidated_sales_data);
        
        $this->response([
            'status' => true,
            'data' => $result
        ], 200);
    }
}