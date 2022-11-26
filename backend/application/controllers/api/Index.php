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
            Header('Access-Control-Allow-Methods: GET,POST,PUT,DELETE'); //method allowed
        }

        function get_index(){
            $this->response(array('status' => true, 'message' => "API OK"), 200);
        }
        
        function post_index(){
            Header('Access-Control-Allow-Origin: *'); //for allow any domain, insecure
            Header('Access-Control-Allow-Headers: *'); //for allow any headers, insecure
            Header('Access-Control-Allow-Methods: GET,POST,PUT,DELETE'); //method allowed
            $this->response(array('message' => 'Welcome to the mtvirux.app API. POST request Recieved'), 200);
        }

        function test(){
            $this->response(array('message' => 'Welcome to the mtvirux.app API'), 200);
        }
}
