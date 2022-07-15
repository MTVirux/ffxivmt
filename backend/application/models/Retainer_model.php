<?php
class Retainer_model extends CI_Model {

	function __construct(){
		//parent::__construct();	
		$this->load->database();
		$this->table = "retainers";
	}

	public function add($retainer)
	{
		$this->db->insert($this->table, $retainer);
	}
	public function remove($id = null)
	{
		if(!empty($id)){

			$this->id = $id;

		}else if(!empty($_POST['id'])){

			$this->id = $_POST['id'];
		}else{
			echo 'Missing Param: id';
		}

		$this->db->delete($this->table, $this);
	}

	public function get_by_id($id = null){
		if(!empty($id)){

			$this->id = $id;

		}else if(!empty($_POST['id'])){

			$this->id = $_POST['id'];
		}else{
			echo 'Missing Param: id';
		}

		return $this->db->select('*')->from($this->table)->where('id', $id)->get()->result();
	}

	public function update($retainer)
	{
		$this->db->update($this->table, $retainer, array('id' => $retainer["id"]));
	}
	
	public function get_by_world($world_name){
		$this->db->select('*')->from($this->table)->where('world', $world_name);
		return $this->db->get()->result();
	}

	public function get_by_character($character_name){
		$this->db->select('*')->from($this->table)->where('character', $character_name);
		return $this->db->get()->result();
	}

	public function get($filter = null){

		$filter = strtolower($filter);

		if(!is_null($filter)){
			//Get distinct worlds
			$worlds_raw = $this->db->select('world')->distinct()->from($this->table)->get()->result();
			foreach($worlds_raw as $single_world){
				$worlds[] = strtolower($single_world->world);
			}

			//Get distinct characters
			$characters_raw = $this->db->select('character')->distinct()->from($this->table)->get()->result();
			foreach($characters_raw as $single_character){
				$characters[] = strtolower($single_character->character);
			}

			//Get distinct servers
			$servers_raw = $this->db->select('server')->distinct()->from($this->table)->get()->result();
			foreach($servers_raw as $single_server){
				$servers[] = strtolower($single_server->server);
			}

			//Get distinct names
			$names_raw = $this->db->select('name')->distinct()->from($this->table)->get()->result();
			foreach($names_raw as $single_name){
				$names[] = strtolower($single_name->name);
			}



			if(in_array($filter, $worlds)){
				$this->db->select('*')->from($this->table)->where('world', $filter);
			}else if(in_array($filter, $characters)){
				$this->db->select('*')->from($this->table)->where('character', $filter);
			}else if(in_array($filter, $servers)){
				$this->db->select('*')->from($this->table)->where('server', $filter);
			}else if(in_array($filter, $names)){
				$this->db->select('*')->from($this->table)->where('name', $filter);
			}else{
				$this->db->select('*')->from($this->table)->like('world', $filter)->or_like('character', $filter)->or_like('server', $filter)->or_like('name', $filter);
			}
			$results = $this->db->get()->result();
			$result_array_tree = $this->objectify_retainers($results);
			return $result_array_tree;
		}



			
	}


	public function objectify_retainers($retainer_query_results){

		$retainers = array();
		foreach($retainer_query_results as $retainer_query_result){
			$retainers[$retainer_query_result->server][$retainer_query_result->world][$retainer_query_result->character][] = $retainer_query_result->name;
		}
		return $retainers;
	}

}
?>
