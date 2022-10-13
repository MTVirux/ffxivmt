<?php
class Item_score_model extends CI_Model {

	function __construct(){
		//parent::__construct();	
		$this->load->database();
		$this->table = "item_scores";
	}



    public function add($item){
        return $this->db->insert($this->table , $item);
    }

    public function remove($item_id = null, $world = null){
        
        if(empty($item_id)){
            echo 'No item id provided';
        }

        $this->db->where('item_id', $item_id);

        if(!is_null($world)){
            $this->db->where('world', $world);
        }

        return $this->db->delete($this->table);

    }

    public function get($id = null){
        if(!empty($id)){
            $this->db->where('item_id', $id);
        }

        $query = $this->db->get($this->table);
        return $query->result();
    }

    public function update($item){
        if(strpos(gettype($item),'array') !== false){

            //get item
            if(count($this->db->select('*')->where('item_id', $item["item_id"])->where('world', $item['world'])->from($this->table)->get()->result()) == 0){
                $add_success = $this->add($item);
                $add_id = $this->db->insert_id();
                if($add_success){
                    //logger('DEBUG', '[Array] Adding item score entry for: ['.$item["world"].'] '.$item["name"] . ' @ ' . $add_id);
                }else{
                    //logger('ERROR', '[Array] Failed to add item score entry for: ['.$item["world"].'] '.$item["name"] . ' @ ' . $add_id);
                    return false;
                }
            }else{
                $update_success = $this->db->where('item_id', $item["item_id"])->where('world', $item['world'])->update($this->table, $item);
                $update_id = $this->db->where('item_id', $item["item_id"])->where('world', $item['world'])->get($this->table)->result()[0]->entry_id;
                if($update_success == 1){
                    //logger('DEBUG', '[Array] Updating item score entry for: ['.$item["world"].'] '.$item["name"] . ' @ ' . $update_id);
                }else{
                    //logger('ERROR', '[Array] Failed to update item score entry for: ['.$item["world"].'] '.$item["name"] . ' @ ' . $update_id);
                    return false;
                }

            }

        }else{
            //get item
            if(count($this->db->where('item_id', $item->item_id)->where('world', $item->world)->from($this->table)->get()->result()) == 0){
                $add_success = $this->add($item);
                $add_id = $this->db->insert_id();
                if($add_success){
                    logger('DEBUG', '[Object] Adding item score entry for: ['.$item->world.'] '.$item->name . ' @ ' . $add_id);
                }else{
                    logger('ERROR', '[Object] Failed to add item score entry for: ['.$item->world.'] '.$item->name . ' @ ' . $add_id);
                    return false;
                }

            }else{
                $update_success = $this->db->where('item_id', $item->item_id)->where('world', $item->world)->from($this->table)->update($item);
                $update_id = $this->db->where('item_id', $item->item_id)->where('world', $item->world)->get($this->table)->result()[0]->entry_id;
                if($update_success == 1){
                    logger('DEBUG', '[Object] Updating item score entry for: ['.$item->world.'] '.$item->name . ' @ ' . $update_id);
                }else{
                    logger('ERROR', '[Object] Failed to update item score entry for: ['.$item->world.'] '.$item->name . ' @ ' . $update_id);
                    return false;
                } 
            }
        }
        return true;
    }
    
}