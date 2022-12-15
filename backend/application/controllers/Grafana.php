<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Grafana extends MY_Controller {

	public function __construct(){
		parent::__construct();
	}

    public function index()
    {
        redirect('https://mtvirux.app:3123');
    }

}