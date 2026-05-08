<?php
defined('BASEPATH') OR exit('No direct script access allowed');
require_once APPPATH.'core/MY_Scylla_Model.php';


class Scylla_Status_model extends MY_Scylla_Model{

    public function __construct(){
        parent::__construct();
    }

    public function get_scylla($sql_databases){

        #Check if Scylla is up
        $cql = 'SELECT * FROM system_schema.keyspaces';
        $stmt = $this->scylla->prepare($cql);
        $result = $this->scylla->execute($stmt, [], 5);
        if ($result) {
            return "true";
        } else {
            return "false";
        }

    }

}