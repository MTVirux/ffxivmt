<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class My_Elastic_Model extends CI_Model{    

    protected $elastic;

    public function __construct() {

        parent::__construct();
        $client = Elasticsearch\ClientBuilder::create()->setHosts(['ffmt_elastic:9200'])->build();
        $this->elastic = $client;
    }

}

