<?php
class Item_model extends CI_Model {

	function __construct(){
		//parent::__construct();	
		$this->load->database();
	}

	public function prep_for_update(){
		$this->db->query('DROP TABLE IF EXISTS items');
		$this->db->query("CREATE TABLE IF NOT EXISTS `items` (
			`id` int(11) NOT NULL,
			`name` varchar(1024) NOT NULL,
			`description` varchar(2048) DEFAULT NULL,
			`canBeHQ` tinyint(4) DEFAULT NULL,
			`alwaysCollectible` varchar(45) DEFAULT NULL,
			`stackSize` float DEFAULT NULL,
			`itemLevel` int(11) DEFAULT NULL,
			`iconImage` int(11) DEFAULT NULL,
			`rarity` int(11) DEFAULT NULL,
			`filterGroup` int(11) DEFAULT NULL,
			`itemUICategory` int(11) DEFAULT NULL,
			`equipSlotCategory` int(11) DEFAULT NULL,
			`unique` tinyint(4) DEFAULT NULL,
			`untradable` tinyint(4) DEFAULT NULL,
			`dyable` tinyint(4) DEFAULT NULL,
			`aetherialReductible` tinyint(4) DEFAULT NULL,
			`materiaSlotCount` int(11) DEFAULT NULL,
			`itemSearchCategory` int(11) DEFAULT NULL,
			`disposable` tinyint(4) DEFAULT NULL,
			`advancedMelding` tinyint(4) DEFAULT NULL
			)") or die(mysql_error());

			$this->db->query("ALTER TABLE `items`
			ADD PRIMARY KEY (`id`),
			ADD UNIQUE KEY `id_UNIQUE` (`id`);
			") or die(mysql_error());
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
