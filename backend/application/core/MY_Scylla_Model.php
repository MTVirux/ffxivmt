<?php
defined('BASEPATH') OR exit('No direct script access allowed');
include_once APPPATH.'vendor/uri2x/php-cql/src/Cassandra.php';

class MY_Scylla_Model extends CI_Model{

    protected $scylla;

    public function __construct() {
        parent::__construct();
        $this->load->config('scylla');

        $this->scylla = new CassandraNative\Cassandra();
        $scylla_config = $this->config->item('scylla');
        
        $host = $scylla_config['database']['host'];
        $user = $scylla_config['database']['user'];
        $pass = $scylla_config['database']['pass'];
        $dbname = $scylla_config['database']['dbname'];
        $port = $scylla_config['database']['port'];

        $this->scylla->connect($host, $user, $pass, $dbname, $port);
        return $this->scylla;
        
    }
}

