<?php
defined ('BASEPATH') OR exit('No direct script access allowed');

require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/RestController.php';
require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/Format.php';
use chriskacerguis\RestServer\RestController;

class Search_buyer extends RestController{
        
    function __construct() {
        parent::__construct();
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
        
        if(isset($_GET["world"])){
            if(!is_null($_GET['world']) && !empty($_GET['world'])){
                $this->load->model('Scylla/Scylla_World_model', 'Scylla_Worlds');
                $all_worlds = $this->Scylla_Worlds->get($_GET['world']);
                foreach($all_worlds as $world){
                    if ($world["name"] == $_GET['world']){
                        $purchase_world_id = $world["id"];
                    }
                }
            }
        }else{
            $purchase_world_id = "";
        }


        if(empty($_GET['buyer_name'])){
            $this->response([
                'status' => false,
                'message' => "POST request failed, please try again. Missing: buyer_name field",
            ], 400);
        }else{
            $buyer_name = $_GET['buyer_name'];
        }

        $buyer_history = $this->Scylla_sales->search_buyer($buyer_name, $purchase_world_id);

        $this->response([
            'status' => true,
            'data' => json_encode($buyer_history),
        ], 200);

    }
}
