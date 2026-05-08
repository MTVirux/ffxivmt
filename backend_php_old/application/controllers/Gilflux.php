<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class GilFlux extends MY_Controller{

	public function __construct(){
		parent::__construct();
		$this->load->helper('url');
		$this->load->model('Scylla/Scylla_Item_model', 'Scylla_Items');
		$this->load->model('Scylla/Scylla_World_model', 'Scylla_Worlds');
		$this->load->model('Scylla/Scylla_Gilflux_Ranking_model', 'Scylla_gilflux_ranking');
	}

	public function gilflux(){

	}


    public function index(){

		$this->load_view_template('gilflux');

	}

}