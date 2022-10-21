<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Test2 extends CI_Controller {

	public function __construct(){
		parent::__construct();
		$this->load->helper('url');
		//$this->load->model('/Redis/Redis_timeseries_model', 'Redis_ts');
		$this->load->model('/Redis/Redis_sales_model', 'Redis_sales');
		
	}

	public function index()
	{
		$this->load->view("test/usage");
	}
	
	public function search_buyer($buyer_name){
		set_time_limit(300);
		if(empty($buyer_name)){
			echo "No buyer name provided";
			return;
		}
		$buyer_name = str_replace('_', ' ', $buyer_name);
		$this->Redis_sales->search_buyer($buyer_name);
	}

	public function get_sales_entries(){
		$this->Redis_sales->get_sales_entries();
	}

	public function get_sales_volume(){
		$this->Redis_sales->get_sales_volume();
	}

	public function get_sales_volumes(){
		$this->Redis_sales->get_sales_volumes();
	}
}
