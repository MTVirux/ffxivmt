<?php
defined ('BASEPATH') OR exit('No direct script access allowed');
require_once APPPATH.'core/MY_Scylla_Model.php';


class Scylla_Item_model extends MY_Scylla_Model{
    
    function __construct() {
        parent::__construct();
    }
    

    public function test(){
        echo 'item_model_test';
    }

    public function get($id = null){
        if(!empty($id) && !is_null($id) && is_numeric($id)){
            $cql = 'SELECT * FROM items WHERE id = ?';
            $stmt = $this->scylla->prepare($cql);
            $result = $this->scylla->execute($stmt, ['id' => $id]);
        }else{
            $cql = 'SELECT * FROM items';
            $result = $this->scylla->query($cql);
        }

        return $result;
    }

    public function update($item){
        $id = $item['id'];
        unset($item['id']);
        $cql = 'UPDATE items SET ' . implode(" = ?, " ,array_keys($item)) . ' = ? WHERE id = ?';
        $item['id'] = $id;
        $stmt = $this->scylla->prepare($cql);
        $result = $this->scylla->execute($stmt, $item, 5);
        if($result[0]["result"] == "success"){
            return true;
        }else{
            return false;
        }
    }

    public function add($item){
        
        $cql = "INSERT INTO items (". implode(", ", array_keys($item)) . ") VALUES (". implode(", ", array_fill(0, count($item), "?")) . ")";
        
        $stmt = $this->scylla->prepare($cql);
        $result = $this->scylla->execute($stmt, $item);

        if($result[0]["result"] == "success"){
            return true;
        }else{
            return false;
        }

    }

    function get_all_ids(){
        $result = $this->scylla->query('SELECT id FROM items');
        $ids = [];
        foreach($result as $row){
            $ids[] = $row['id'];
        }
        return $ids;
    }

    public function get_craftable_items(){
		$result = $this->scylla->query("Select * from items where crafted = true");

        //Sort $result by id value
        usort($result, function($a, $b) {
            return $a['id'] <=> $b['id'];
        });

        return $results;
	}

    public function get_name($id = null){
        if(!empty($id) && !is_null($id) && is_numeric($id)){
            $cql = 'SELECT id, name FROM items WHERE id = ?';
            $stmt = $this->scylla->prepare($cql);
            $result = $this->scylla->execute($stmt, ['id' => $id]);
        }else{
            $result = $this->scylla->query('SELECT id, name FROM items');
            $names = [];
        }
        foreach($result as $row){
            $names[$row["id"]] = $row['name'];
        }

        return $names;
    }

    public function get_all_names(){
        $result = $this->scylla->query('SELECT id, name FROM items');
        $names = [];
        foreach($result as $row){
            $names[$row["id"]] = $row['name'];
        }

        return $names;
    }

    
}