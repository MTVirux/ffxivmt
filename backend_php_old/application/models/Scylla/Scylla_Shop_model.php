<?php
defined ('BASEPATH') OR exit('No direct script access allowed');
require_once APPPATH.'core/MY_Scylla_Model.php';


class Scylla_Shop_model extends MY_Scylla_Model{
    
    function __construct() {
        parent::__construct();
    }
    

    public function test(){
        echo 'shop_model_test';
    }


    public function add_entry($shop_entry){


        $cql = "INSERT INTO shops (". implode(", ", array_keys($shop_entry)) . ") VALUES (". implode(", ", array_fill(0, count($shop_entry), "?")) . ")";
        $stmt = $this->scylla->prepare($cql);
        $result = $this->scylla->execute($stmt, $shop_entry);
        if($result[0]["result"] == "success"){
            return true;
        }else{
            return false;
        }
    }

    public function get($param_array = null){
        if(!is_null($param_array)){
            $keys = array_keys($param_array);
            $cql = 'SELECT * FROM shops WHERE ' . implode(" = ? AND ", $keys) . ' = ?';
            $stmt = $this->scylla->prepare($cql, $param_array);
        }else{
            $cql = 'SELECT * FROM shops';
            $result = $this->scylla->query($cql);
        }
        return $result;
    }

    public function get_by_item_id($item_id){
        $cql = 'SELECT * FROM shops WHERE item_id = ?';
        $stmt = $this->scylla->prepare($cql);
        $result = $this->scylla->execute($stmt, ['item_id' => $item_id]);
        return $result;
    }

    public function get_by_shop_id($shop_id){
        $cql = 'SELECT * FROM shops WHERE shop_id = ?';
        $stmt = $this->scylla->prepare($cql);
        $result = $this->scylla->execute($stmt, ['shop_id' => $shop_id]);
        return $result;
    }

    public function get_by_shop_name($shop_name){
        $cql = 'SELECT * FROM shops WHERE shop_name = ?';
        $stmt = $this->scylla->prepare($cql);
        $result = $this->scylla->execute($stmt, ['shop_name' => $shop_name]);
        return $result;
    }

    public function get_by_item_name($item_name){
        $cql = 'SELECT * FROM shops WHERE item_name = ?';
        $stmt = $this->scylla->prepare($cql);
        $result = $this->scylla->execute($stmt, ['item_name' => $item_name]);
        return $result;
    }


    
}