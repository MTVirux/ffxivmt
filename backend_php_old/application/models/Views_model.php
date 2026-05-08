<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Views_model extends CI_Model{

    public function __construct(){
        parent::__construct();
		$this->load->database();
    }

    public function get($table_name, $limit = null, $page = 0){


        //Select everything from the view
        $this->db->select('*');

        //From the view
        $this->db->from($table_name);

        //Limit the results
        if(!is_null($limit)){
            $this->db->limit($limit, $page);
        }

        //Get the results
        $query = $this->db->get();

        //Return the results
        return $query->result_array();

    }

}