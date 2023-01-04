<?php
defined ('BASEPATH') OR exit('No direct script access allowed');

require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/RestController.php';
require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/Format.php';
use chriskacerguis\RestServer\RestController;

class Search_buyer extends RestController{
        
    function __construct() {
        parent::__construct();
        Header('Access-Control-Allow-Origin: *'); //for allow any domain, insecure
        Header('Access-Control-Allow-Headers: *'); //for allow any headers, insecure
        Header('Access-Control-Allow-Methods: GET,POST,PUT,DELETE'); //method allowed
    }

    //public function index_get(){
    //    $this->response([
    //        'status' => false,
    //        'message' => "This endpoint only accepts POST requests",
    //    ], 200);
    //}

    public function index_get(){
        $this->load->model('Scylla/Sale_model', 'Scylla_sales');
        set_time_limit(300);
        
        if(!is_null($_GET['world']) && !empty($_GET['world'])){
            $world = $_GET['world'];
        }else{
            $world = "";
        }


        if(empty($_GET['buyer_name'])){
            $this->response([
                'status' => false,
                'message' => "POST request failed, please try again. Missing: buyer_name field",
            ], 400);
        }else{
            $buyer_name = $_GET['buyer_name'];
        }

        $buyer_history = $this->Scylla_sales->search_buyer($buyer_name, $world);

        $this->response([
            'status' => true,
            'data' => json_encode($buyer_history),
        ], 200);

    }
}
