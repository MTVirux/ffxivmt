<?php
defined ('BASEPATH') OR exit('No direct script access allowed');
require_once APPPATH.'core/MY_Scylla_Model.php';


class Scylla_Gilflux_model extends MY_Scylla_Model{
    
    function __construct() {
        parent::__construct();
    }
    

    public function test(){
        echo 'sale_model_test';
    }


    public function add_sale($sale){
        ini_set('memory_limit', '2048M');
        //Make whatever var we get into an array
        $sales_array = [];
        if(gettype($sale) == 'array'){
            $sales_array = $sale;
        }else{
            $sales_array[] = $sale;
        }

        if(count($sales_array) == 0){
            return;
        }

        $parsed_sales = 0;

        //Start time
        $start_time = microtime(true);

        $total_sales_to_parse = count($sales_array);

        $batch = new CassandraNative\BatchStatement($this->scylla, 1);
        $batch_statement_count = 0;


        foreach($sales_array as $sale_data_to_insert){

            $stmt = $this->scylla->prepare("INSERT INTO gilflux (item_id, item_name, world_id, world_name, datacenter, region, total, sale_time) VALUES (?, ?, ?, ?, ?, ?, ?, ?)");
            $batch->add_prepared($stmt, $sale_data_to_insert);
            $batch_statement_count ++;

            //Every 1000 sales insert the batch
            $parsed_sales++;
            if($batch_statement_count == 1000){
                $result = $this->scylla->batch($batch, 1); //5 is consistency across all clusters
                $batch = new CassandraNative\BatchStatement($this->scylla, 1);
                if($result[0]["result"] != "success"){
                    logger('SCYLLA_GILFLUX', "Error inserting sale into ScyllaDB: " . $result[0]["result"]);
                    die();
                }else{
                    logger('SCYLLA_GILFLUX', "Inserted " . $parsed_sales . " out of " . $total_sales_to_parse . " sales in " . (microtime(true) - $start_time) . " seconds");
                    $batch_statement_count = 0;
                }
            }
        }
        if($batch_statement_count != 0 && count($sales_array) > 0){
            //Insert the last batch
            $result = $this->scylla->batch($batch, 5);
            if($result[0]["result"] != "success"){
                logger('SCYLLA_SALES', "Error inserting sale into ScyllaDB: " . $result[0]["result"]);
                die();
            }else{
                logger('SCYLLA_GILFLUX', "Inserted " . $parsed_sales . " out of " . $total_sales_to_parse . " sales in " . (microtime(true) - $start_time) . " seconds");
            }
        }

        logger('SCYLLA_GILFLUX', "Inserted " . $parsed_sales . " sales in " . microtime(true) - $start_time . " seconds");
        return array("parsed_sales" => $parsed_sales, "time" => microtime(true) - $start_time);
    }

    public function get($item_id, $world = null, $from = null - 86400, $to = null){

        //If times are not set, set them from 24 hours ago to now
        if(is_null($from)){
            $from = time() - 86400;
        }

        if(is_null($to)){
            $to = time();
        }


        //If world is not set, get all worlds
        if(is_null($world)){
            $stmt = $this->scylla->prepare("SELECT * FROM sales WHERE item_id = ? AND sale_time >= ? AND sale_time <= ?");
            $result = $this->scylla->execute($stmt, array($item_id, $from, $to), 5);
        }else if (!is_null($world)){
            $stmt = $this->scylla->prepare("SELECT * FROM sales WHERE item_id = ? AND world_id = ? AND sale_time >= ? AND sale_time <= ?");
            $result = $this->scylla->execute($stmt, array($item_id, $world, $from, $to), 5);
        }

        return $result;
    }
    

    
}