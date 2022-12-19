<?php
defined('BASEPATH') OR exit('No direct script access allowed');

class Portainer extends MY_Controller {

	public function __construct(){
		parent::__construct();
	}

    public function index()
    {
        redirect('https://mtvirux.app:9000');
    }

}