<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Home extends MY_Controller {

	public function __construct(){
		parent::__construct();
		$this->load->helper('url');
		
	}

	public function index()
	{
		
		$this->load_view_template('home');

	}

	public function redirect_to_git(){
		$this->load->helper('url');
		redirect($this->config->item('github_link'), 'refresh');
	}
}