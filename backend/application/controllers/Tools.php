<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Tools extends MY_Controller {

	public function __construct(){
		parent::__construct();
		$this->load->helper('url');
	}

	public function item_product_profit_calculator(){
		$data['message'] = "Please enter a search term...";
		$this->load_view_template('tools/item_product_profit_calculator', $data);
		return;
	}
}