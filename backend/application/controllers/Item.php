<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Item extends MY_Controller {

	public function __construct(){
		
		parent::__construct();
		$this->load->helper('url');
		$this->load->model('Scylla/Scylla_Item_model', 'Scylla_Items');
		$this->load->model('Scylla/Sale_model', 'Scylla_Sales');
		
	}

	public function index($item_id = null)
    {

        $item = $this->Scylla_Items->get($item_id)[0];
		$sales = $this->Scylla_Sales->get_by_item($item_id);


        foreach(array_keys($item) as $key){
            $data["item"][$key] = $item[$key];
        }

		$this->load_view_template('item', $data);

	}


}