<?php
defined ('BASEPATH') OR exit('No direct script access allowed');
require_once APPPATH.'core/MY_Scylla_Model.php';


class World_model extends MY_Scylla_Model{
    
    function __construct() {
        parent::__construct();
    }

    public function add($world){
        $cql = "INSERT INTO worlds (id, name, datacenter, region) VALUES (?, ?, ?, ?)";

        $stmt = $this->scylla->prepare($cql);
        $result = $this->scylla->execute($stmt, $world);

        if($result[0]["result"] == "success"){
            return true;
        }else{
            return false;
        }

    }

    public function delete_all(){

        $cql = "TRUNCATE worlds";
        $result = $this->scylla->query($cql);

        if($result[0]["result"] == "success"){
            return true;
        }else{
            return false;
        }
        
    }

    public function get_by_region($region){
        $cql = 'SELECT * FROM worlds WHERE region = ?';
        $stmt = $this->scylla->prepare($cql);
        $result = $this->scylla->execute($stmt, ['region' => $region]);

        return $result;
    }

    public function get_by_datacenter($datacenter){
        $cql = 'SELECT * FROM worlds WHERE datacenter = ?';
        $stmt = $this->scylla->prepare($cql);
        $result = $this->scylla->execute($stmt, ['datacenter' => $datacenter]);

        return $result;
    }

    public function get($id = null){

        if (empty($id) || is_null($id) || !is_numeric($id)) {
            $cql = 'SELECT * FROM worlds';
            $result = $this->scylla->query($cql);
        }else{
            $cql = 'SELECT * FROM worlds WHERE id = ?';
            $stmt = $this->scylla->prepare($cql);
            $result = $this->scylla->execute($stmt, ['id' => $id]);
        }
        

        return $result;
    }

    public function get_by_name($world_name){
        $cql = 'SELECT * FROM worlds WHERE name = ?';
        $stmt = $this->scylla->prepare($cql);
        $result = $this->scylla->execute($stmt, ['name' => $world_name]);

        return $result;
    }

    public function get_regions(){
        $cql = 'SELECT region FROM worlds';
        $results = $this->scylla->query($cql);

        foreach($results as $result){
            $regions[] = $result['region'];
        }

        return array_unique($regions);
    }

    public function get_datacenters(){
        $cql = 'SELECT datacenter FROM worlds';
        $result = $this->scylla->query($cql);

        return $result;
    }


}