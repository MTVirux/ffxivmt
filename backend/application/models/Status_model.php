<?php
defined('BASEPATH') OR exit('No direct script access allowed');
require APPPATH.'core/MY_Redis_Model.php';

class Status_model extends MY_Redis_Model{

    public function __construct(){
        parent::__construct();
		$this->load->database();
        $cmdGet = $this->redis->createCommand('CONFIG', array('GET', 'databases'));
        $cmdGetReply = $this->redis->executeCommand($cmdGet);
    }

    public function get_sql($sql_databases){

        $sql_db_status = [];
        

        foreach($sql_databases as $sql_database){

            //SET SQL DB STATUS
            if($this->db === false){
                $sql_db_status[$sql_database] = false;
            }else{
                $sql_db_status[$sql_database]['sql_tables'] = $this->sql_tables($sql_database);
            }

        }

        return $sql_db_status;


    }

    private function sql_tables($database){

        $sql_tables = [];

        $query_results = $this->db->query('Select TABLE_NAME as name from information_schema.TABLES WHERE TABLE_SCHEMA = "ffxiv_db"')->result();
        echo '<pre>'; 

        foreach($query_results as $result){
            $row_count = $this->db->query('select count(*) as row_count from ffxiv_db.'.$result->name)->result()[0]->row_count;
            
            if($row_count === false){
                $sql_tables[$result->name]['status'] = false;
                $sql_tables[$result->name]['row_count'] = false;
                continue;
            }else{
                $sql_tables[$result->name]['status'] = true;
                $sql_tables[$result->name]['row_count'] = $row_count;            
            }
        }

        return $sql_tables;
    }

    public function get_redis(){

        $redis_databases = [];

        $cmdGetDB = $this->redis->createCommand('CONFIG', array('GET', 'databases'));
        $redis_database_count = $this->redis->executeCommand($cmdGetDB);
        $redis_database_count = intval($redis_database_count['databases']);
        $redis_db_names = [
            $this->config->item('redis_listings_db') => 'redis_listings_db',
            $this->config->item('redis_sales_db') => 'redis_sales_db',
            $this->config->item('redis_listings_clean_db') => 'redis_listings_clean_db',
            $this->config->item('redis_sales_clean_db') => 'redis_sales_clean_db',
            $this->config->item('recent_db') => 'recent_db',
        ];
        for ($i = 0 ; $i < $redis_database_count ; $i++){
            $select_db = $this->redis->select($i);
            $key_count = count($this->redis->keys('*'));
            
            if(array_key_exists($i, $redis_db_names)){
                $redis_db_name = $redis_db_names[$i];
            }else{
                $redis_db_name = 'redis_db_'.$i;
            }

            if($key_count == 0 )
                continue;
            $redis_databases[$redis_db_name]['key_count'] = $key_count;
            

            $redis_databases[$redis_db_name]['id'] = $i;

            if($key_count > 0){
                $redis_databases[$redis_db_name]['status'] = true;
            }else{
                $redis_databases[$redis_db_name]['status'] = false;
            }

        }

        return $redis_databases;
    }
}