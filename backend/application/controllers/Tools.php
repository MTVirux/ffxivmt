<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Tools extends MY_Controller {

	public function __construct(){
		parent::__construct();
		$this->load->helper('url');
		$this->load->library('session');
	}

	public function item_product_profit_calculator(){
		$data['message'] = "Please enter a search term...";
		$data['session'] = $this->session->userdata();
		$this->load_view_template('tools/item_product_profit_calculator', $data);
		return;
	}

	public function currency_efficiency_calculator(){
		$data["session"] = $this->session->userdata();
		$this->load_view_template('tools/currency_efficiency_calculator', $data);
	}

	/*
	Redirects
	*/

	// Redirects currency_profit_calculator to currency_efficiency_calculator
	public function currency_profit_calculator(){
		currency_efficiency_calculator();
	}
}