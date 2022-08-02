<?php
defined('BASEPATH') OR exit('No direct script access allowed');

require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/RestController.php';
require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/Format.php';
use chriskacerguis\RestServer\RestController;

class Item_tracking extends RestController {

    public function __construct() {
        parent::__construct();
        $this->load->model('Item_Tracking_model', 'Item_Tracking');
    }

    public function index_get($item = null) {
        $req = $this->get();

        if(!empty($req['item'])){
            $item = $req['item'];
        }

        $data = $this->Item_Tracking->apiGet($item);

        $result_array = [];
        foreach ($data['data'] as $result) {
            $result_array[] = $result->item_id;
        }

        $data['data'] = $result_array;

        if($data['status'] == 'true'){
            $this->response([
                'status' => true,
                'data' => json_encode($data['data']),
            ], 200);
        }else{
            $this->response([
                'status' => true,
                'message' => $data['message']
            ], 400);        
        }
    }

    public function index_post($item = null){

        $req = $this->post();

        if(!empty($req['item'])){
            $item = $req['item'];
        }

        $data = $this->Item_Tracking->apiPost($item);

        if($data['status'] == 'true'){
            $this->response(array('status' => $data['status'], 'message' => $data['message']), 200);
        }else{
            $this->response(array('status' => $data['status'], 'message' => $data['message']), 400);
        }
        

    }

    public function index_delete($item = null){
        $req = $this->delete();
        
        if(!empty($req['item'])){
            $item = $req['item'];
        }
        
        $data = $this->Item_Tracking->apiDelete($item);
        
        if($data['status'] == 'true'){
            $this->response(array('status' => $data['status'], 'message' => $data['message']), 200);
        }else{
            $this->response(array('status' => $data['status'], 'message' => $data['message']), 400);
        }
    }

}