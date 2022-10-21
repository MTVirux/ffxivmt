<?php

defined('BASEPATH') OR exit('No direct script access allowed');

class Redis_funcs extends CI_Controller{

    public function __construct(){
        parent::__construct();
        $this->load->helper('url');
        $this->load->model('/Redis/Redis_timeseries_model', 'Redis_ts');
        $this->load->model('/Redis/Redis_sales_model', 'Redis_sales');
    }

    public function index(){
        $this->load->view("test/usage");
    }

    /********************/
    /*       SALES      */
    /********************/

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

    public function get_sales_volumes($scope = null, $input = null, $input2 = null){

        $valid_scopes = ['key', 'item', 'world', 'all'];

        if(empty($scope) && empty($input) && empty($input2) && empty($_POST['scope']) && empty($_POST['input']) && empty($_POST['input2'])){
            $this->load->view("redis/get_sales_volumes/usage");

            return;
        }

        if(empty($scope) && empty($_POST["scope"])){
            echo 'Must provide a scope. <br>';
            echo 'Valid scopes are:<br>';
            echo '- ' . implode('<br> - ', $valid_scopes);
            return;
        }

        if(!in_array($scope, $valid_scopes)){
            echo "Invalid scope provided";
            return;
        }

        if(strpos($scope, 'all') !== false){
            unset($input);
            unset($input2);
        }

        if(strpos($scope, 'key') !== false){
            if(empty($input)){
                echo "Must provide a key";
                return;
            }

            if(empty($input2)){
                echo "Must provide a value";
                return;
            }
        }



        $this->sales_model->get_sales_volumes();
    }



    /********************/
    /*    TIMESERIES    */
    /********************/
}