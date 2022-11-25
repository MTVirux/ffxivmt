<?php
defined('BASEPATH') or exit('No direct script access allowed');

class Status extends MY_Controller {

    public function __construct(){

        parent::__construct();
        $this->load->model('Status_model');

    }

    public function index(){

        $sql_databases = ['ffxiv_db'];

        $data = [];

        foreach($sql_databases as $sql_database){
            $data['SQL'][$sql_database] = $this->Status_model->get_sql($sql_databases);
        }

        $data['Redis'] = $this->Status_model->get_redis();

        pretty_dump($data);die();
        
        //$this->load_view_template'status', $data);
        return true;
    }
}