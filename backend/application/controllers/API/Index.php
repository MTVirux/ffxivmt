<?php
defined ('BASEPATH') OR exit('No direct script access allowed');

require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/RestController.php';
require APPPATH.'vendor/chriskacerguis/codeigniter-restserver/src/Format.php';
use chriskacerguis\RestServer\RestController;

class Index extends RestController{
        
        function __construct() {
            parent::__construct();
        }
        
        function index(){
            $this->response(array('message' => 'Welcome to the mtvirux.app API'), 200);
        }

        function test(){
            $this->response(array('message' => 'Welcome to the mtvirux.app API'), 200);
        }
}
