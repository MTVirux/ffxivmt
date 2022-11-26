<?php
defined('BASEPATH') OR exit('No direct script access allowed');

require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/RestController.php';
require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/Format.php';
use chriskacerguis\RestServer\RestController;

class Redis extends RestController {


    public function __construct() {
        parent::__construct();
        Header('Access-Control-Allow-Origin: *'); //for allow any domain, insecure
        Header('Access-Control-Allow-Headers: *'); //for allow any headers, insecure
        Header('Access-Control-Allow-Methods: GET,POST,PUT,DELETE'); //method allowed
        $this->load->model('Redis/Redis_listings_model', 'Listings');
        //$this->load->library('redis');
    }

    public function keys_get($world = null, $item_id = null){
        $req = $this->get();
        
        if(!empty($req['world'])){
            $world = $req['world'];
        }
        
        if(!empty($req['item_id'])){
            $item_id = $req['item_id'];
        }
        
        $this->Listings->get_keys($world, $item_id);

        if($data['success'] == 'true'){
            $this->response([
                            'status' => 'true',
                            'data' => json_encode($data['data'])
                        ], 200);
        }else{
            $this->response([
                            'status' => 'false',
                            'message' => $data['message']
                        ], 404);
        }
    }

    public function recent_get($world_id = null, $limit = null){
        $req = $this->get();
        
        if(!empty($req['limit'])){
            $limit = $req['limit'];
        }

        if(!empty($req['world'])){
            $world_id = $req['world'];
        }
        
        $data = $this->Listings->recent_apiGet($world_id, $limit);

        if($data['success'] == 'true'){
            $this->response([
                            'status' => 'true',
                            'data' => json_encode($data['data'])
                        ], 200);
        }else{
            $this->response([
                            'status' => 'false',
                            'message' => $data['message']
                        ], 404);
        }
    }
}

