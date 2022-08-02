<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Item_tracking_model  extends CI_model{

    function __construct(){
		//parent::__construct();	
		$this->load->database();
        $this->table = 'tracked_items';
	}

    public function apiGet($item_id = null){

        $this->db->select('item_id');
        
        if(!is_null($item_id)){
            $this->db->where('item_id', $item_id);
        }

        $results = $this->db->get($this->table)->result();

        if($results){
            return array(
                'status' => 'true',
                'data' => $results
            );
        }else{
            return array(
                'status' => 'false',
                'message' => 'No data found'
            );
        }

    }

    public function apiPost($item_id){

        if(is_null($item_id) || !is_numeric($item_id)){
            return ['status' => 'false', 'message' => 'Invalid item id'];
        }

        if(count($this->db->select('*')->from($this->table)->where('item_id', $item_id)->get()->result_array()) == 0){
            $this->db->insert($this->table, array('item_id' => $item_id));
        }else{
            return ['status' => 'true', 'message' => 'Item_id ' . $item_id . ' was already being tracked'];
        }

        if($this->db->affected_rows() > 0){
            return ['status' => 'true', 'message' => 'Item_id ' . $item_id . ' is now being tracked'];
        }else{
            return ['status' => 'false', 'message' => 'Item_id ' . $item_id . ' could not be tracked'];
        }



    }

    public function apiDelete($item_id){

        if(is_null($item_id) || !is_numeric($item_id)){
            return ['status' => 'false', 'message' => 'Invalid item id'];
        }

        if(count($this->db->where('item_id', $item_id)->get($this->table)->result_array()) > 0){
            $this->db->where('item_id', $item_id)->delete($this->table);
        }else{
            return ['status' => 'true', 'message' => 'Item_id ' . $item_id . ' was already not being tracked'];
        }

        if($this->db->affected_rows() > 0){
            return ['status' => 'true', 'message' => 'Item_id ' . $item_id . ' is now untracked'];
        }else{
            return ['status' => 'false', 'message' => 'Could not stop tracking item_id ' . $item_id];
        }

    }
}