<?php
defined ('BASEPATH') OR exit('No direct script access allowed');

require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/RestController.php';
require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/Format.php';
use chriskacerguis\RestServer\RestController;

class worlds extends RestController{
        
        function __construct() {
            parent::__construct();
            
            Header('Access-Control-Allow-Origin: *'); //for allow any domain, insecure
            Header('Access-Control-Allow-Headers: *'); //for allow any headers, insecure
            Header('Access-Control-Allow-Methods: GET'); //method allowed
        }
        
        public function index_get()
        {   
            $this->load->model('Scylla/Scylla_World_model', 'Scylla_Worlds');

            if($world_structure_array = $this->cache->get('ffxiv_world_structure_array')){
                $this->response([
                    'status' => true,
                    'message' => 'Worlds retrieved from cache successfully',
                    'data' => $world_structure_array
                ], 200);
            }

            $worlds = $this->Scylla_Worlds->get();

            if(count($worlds) == 0){
                $this->response([
                    'status' => false,
                    'message' => 'No worlds found'
                ], 404);
                return;
            }

            $world_structure_array = array();
            foreach($worlds as $world){
                $world_structure_array[$world["region"]][$world["datacenter"]][$world["id"]] = $world["name"];
            }

            //Sort alphabetically
            ksort($world_structure_array);
            foreach($world_structure_array as $region => $datacenters){
                ksort($world_structure_array[$region]);
                foreach($datacenters as $datacenter => $worlds){
                    ksort($world_structure_array[$region][$datacenter]);
                }
            }

            $this->cache->save('ffxiv_world_structure_array', $world_structure_array, 300);
            

            $this->response([
                'status' => true,
                'message' => 'Worlds retrieved successfully',
                'data' => $world_structure_array
            ], 200);

        }   
    }