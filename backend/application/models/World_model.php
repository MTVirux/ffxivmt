<?php
class World_model extends CI_Model {

	function __construct(){
		//parent::__construct();	
		$this->load->database();
		$this->table = "world";
	}

	//add world
	public function add($world)
	{
		$this->db->insert($this->table, $world);
	}

	//remove world
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

	//get world by id
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

	//get world by name
	public function get_by_name($name = null){
		if(!empty($name)){

			$this->name = $name;

		}else if(!empty($_POST['name'])){

			$this->name = $_POST['name'];
		}else{
			echo 'Missing Param: name';
		}

		return $this->db->select('*')->from($this->table)->where('name', $name)->get()->result();
	}

	//get world by region
	public function get_by_region($region = null){
		if(!empty($region)){

			$this->region = $region;

		}else if(!empty($_POST['region'])){

			$this->region = $_POST['region'];
		}else{
			echo 'Missing Param: region';
		}

		return $this->db->select('*')->from($this->table)->where('region', $region)->get()->result();
	}

	//get world by server
	public function get_by_server($server = null){
		if(!empty($server)){

			$this->server = $server;

		}else if(!empty($_POST['server'])){

			$this->server = $_POST['server'];
		}else{
			echo 'Missing Param: server';
		}

		return $this->db->select('*')->from($this->table)->where('server', $server)->get()->result();
	}

}

?>
