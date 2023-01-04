<?php
defined ('BASEPATH') OR exit('No direct script access allowed');
require_once APPPATH.'core/MY_Scylla_Model.php';


class Sale_model extends MY_Scylla_Model{
    
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

            $stmt = $this->scylla->prepare("INSERT INTO sales (buyer_name, hq, on_mannequin, quantity, sale_time, world_id, item_id, world_name, unit_price, item_name, total) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)");
            $batch->add_prepared($stmt, $sale_data_to_insert);
            $batch_statement_count ++;

            //Every 1000 sales insert the batch
            $parsed_sales++;
            if($batch_statement_count == 1000){
                $result = $this->scylla->batch($batch, 1); //5 is consistency across all clusters
                $batch = new CassandraNative\BatchStatement($this->scylla, 1);
                if($result[0]["result"] != "success"){
                    logger('SCYLLA_DB', "Error inserting sale into ScyllaDB: " . $result[0]["result"]);
                    die();
                }else{
                    logger('SCYLLA_DB', "Inserted " . $parsed_sales . " out of " . $total_sales_to_parse . " sales in " . (microtime(true) - $start_time) . " seconds");
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
                logger('SCYLLA_DB', "Inserted " . $parsed_sales . " out of " . $total_sales_to_parse . " sales in " . (microtime(true) - $start_time) . " seconds");
            }
        }

        logger('SCYLLA_SALES', "Inserted " . $parsed_sales . " sales in " . microtime(true) - $start_time . " seconds");
    }

    public function search_buyer($buyer_name, $world = ""){
        $this->load->helper('ffxiv_worlds');
        $ALLOW_FILTERING_REQUIRED = false;
        
        //Get ID if world is not empty string
        if(empty($world)){
            $world_id = "";
        }else{
            $world_id = get_world_id($world, $this->config->item('ffxiv_worlds'));
        }

        //Start query
        $cql_query = "SELECT * FROM sales";

        //If we have a buyer name, add it to the query
        if(!empty($buyer_name)){
            $cql_query .= " WHERE buyer_name = ?";
        }

        //If we have a world_id, add it to the query
        if(!empty($world_id)){
            $cql_query .= " AND world_id = ?";
            $ALLOW_FILTERING_REQUIRED = true;
        }

        if($ALLOW_FILTERING_REQUIRED){
            $cql_query .= " ALLOW FILTERING";
        }

        $stmt = $this->scylla->prepare($cql_query);

        if(empty($world_id)){
            $result = $this->scylla->execute($stmt, array("buyer_name" => $buyer_name));
        }else{
            $result = $this->scylla->execute($stmt, array("buyer_name" => $buyer_name, "world_id" => $world_id));
        }
        return $result;
    }

    
}