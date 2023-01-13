<?php
defined('BASEPATH') OR exit('No direct script access allowed');

include_once APPPATH.'vendor/elasticsearch/elasticsearch/src/ClientBuilder.php';

class My_Elastic_Model extends CI_Model{    

    protected $elastic;

    public function __construct() {

        parent::__construct();

        $data = array("name" => "Gil");
        var_dump($this->elasticsearch->add("items", 1, $data));


    }

}

