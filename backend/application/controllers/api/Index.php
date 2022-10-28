<?php
defined ('BASEPATH') OR exit('No direct script access allowed');

require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/RestController.php';
require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/Format.php';
use chriskacerguis\RestServer\RestController;

class Index extends RestController{
        
        function __construct() {
            parent::__construct();
            
            Header('Access-Control-Allow-Origin: *'); //for allow any domain, insecure
            Header('Access-Control-Allow-Headers: *'); //for allow any headers, insecure
            Header('Access-Control-Allow-Methods: GET,POST,PUT,DELETE'); //method allowed
            echo json_encode('ok');

        }
        
        function post_index(){
            Header('Access-Control-Allow-Origin: *'); //for allow any domain, insecure
            Header('Access-Control-Allow-Headers: *'); //for allow any headers, insecure
            Header('Access-Control-Allow-Methods: GET,POST,PUT,DELETE'); //method allowed
            echo json_encode('ok');
            $this->response(array('message' => 'Welcome to the mtvirux.app API'), 200);
        }

        function test(){
            $this->response(array('message' => 'Welcome to the mtvirux.app API'), 200);
        }
}
