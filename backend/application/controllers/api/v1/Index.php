<?php
defined ('BASEPATH') OR exit('No direct script access allowed');

require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/RestController.php';
require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/Format.php';
use chriskacerguis\RestServer\RestController;

class index extends RestController{
        
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
        
        public function index_post()
        {
            echo "POST_request";
        }
        
        public function index_put()
        {
            echo "PUT_request";
        }
        
        public function index_patch()
        {
            echo "PATCH_request";
        }
        
        public function index_delete()
        {
            echo "DELETE_request";
        }
    }