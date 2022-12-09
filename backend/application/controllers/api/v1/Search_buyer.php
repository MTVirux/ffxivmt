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

    public function index_post(){
        $this->load->model('Redis/Redis_sales_model', 'Redis_sales');
        set_time_limit(300);
        
        if(!is_null($_POST['world']) && !empty($_POST['world'])){
            $world = $_POST['world'];
        }
        if(empty($_POST['buyer_name'])){
            $this->response([
                'status' => false,
                'message' => "POST request failed, please try again. Missing: buyer_name field",
            ], 400);
        }
        $buyer_name = str_replace('_', ' ', $_POST['buyer_name']);
        $buyer_history = $this->Redis_sales->search_buyer($buyer_name, $world);

        $this->response([
            'status' => true,
            'data' => json_encode($buyer_history),
        ], 200);

    }

    public function index_get($buyer_name, $world){
        $this->load->model('Redis/Redis_sales_model', 'Redis_sales');
        set_time_limit(300);
        if(empty($buyer_name)){
            $this->response([
                'status' => false,
                'message' => "GET request failed, please try again. Missing: buyer_name field",
            ], 400);
        }
        $buyer_name = str_replace('_', ' ', $buyer_name);
        $buyer_history = $this->Redis_sales->search_buyer($buyer_name, $world);

        pretty_print($buyer_history);die();

        $this->response([
            'status' => true,
            'data' => json_encode($buyer_history),
        ], 200);

    }
}
