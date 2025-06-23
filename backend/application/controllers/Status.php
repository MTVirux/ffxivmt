<?php
defined('BASEPATH') or exit('No direct script access allowed');

class Status extends MY_Controller {

    public function __construct(){

        parent::__construct();
        $this->load->model('Scylla/Scylla_Status_model');

    }

    public function index(){

        #Check cache and return if it exists
        $cache_key = 'scylla_status';
        $cached_data = $this->cache->get($cache_key);
        if ($cached_data) {
            $data = array(
                'status' => $cached_data,
                'code' => 200
            );
            echo json_encode($data);
            http_response_code($data['code']);
            return;
        }

        #check if scylla is up if there was no cache
        if ($this->Scylla_Status_model->get_scylla($this->config->item('sql_databases'))) {
            $data = array(
                'status' => 'Scylla is up',
                'code' => 200
            );
        } else {
            $data = array(
                'status' => 'Scylla is down',
                'code' => 500
            );
        }

        echo "" . json_encode($data) . "";
        http_response_code($data['code']);

        #Set cache for 10 seconds
        $this->cache->save($cache_key, $data['status'], 10);
        return;

    }
}