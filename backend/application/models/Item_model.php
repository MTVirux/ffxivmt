<?php
class Item_model extends CI_Model {

	function __construct(){
		//parent::__construct();	
		$this->load->database();
	}

	public function add($item)
	{
		$this->db->insert('items',$item);
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

		$this->db->delete('entries', $this);
	}

	public function update($item)
	{
		$this->db->update('items', $item, array('id' => $item["id"]));
	}

	public function get($id = null){
		if(!empty($id)){

			$this->id = $id;

		}else if(!empty($_POST['id'])){

			$this->id = $_POST['id'];
		}else{
			echo 'Missing Param: id';
		}

		return $this->db->select('*')->from('items')->where('id', $id)->get()->result();
	}

	public function get_last_item(){
		return $this->db->select('*')->from('items')->order_by('id', 'DESC')->limit(1)->get()->result();
	}

	public function get_by_name($terms, $include_untradable = false){
		$this->db->select('*')
		->from('items')
		->order_by('id', 'DESC');
		if(!is_array($terms)){

			$this->db->like('name', $terms);

		}else{

			foreach($terms as $term){
				$this->db->like('name', $term);
			}
		}

		if(!$include_untradable){
			$this->db->where('untradable', 0);
		}
		

		return $this->db->get()->result();
	}

}
?>
