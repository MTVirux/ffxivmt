<?php
defined('BASEPATH') OR exit('No direct script access allowed');
require_once APPPATH.'vendor/uri2x/php-cql/src/Cassandra.php';

class MY_Scylla_Model extends CI_Model{

    protected $scylla;

    public function __construct() {
        parent::__construct();

        $scylla = new CassandraNative\Cassandra();

        $host = 'ffmt_scylla';
        $user = '';
        $pass = '';
        $dbname = 'sales';
        $port = 9042;

        pretty_dump($scylla->connect($host, $user, $pass, $dbname, $port));
        
    }
}

