<?php
defined ('BASEPATH') OR exit('No direct script access allowed');
require APPPATH.'core/MY_Elastic_Model.php';


class Elastic_Item_model extends MY_Elastic_Model{
    
    function __construct() {
        parent::__construct();
    }

    public function add($item){

        if(gettype($item) != 'array'){
            echo "item passed to add is not an array";
            return false;
        }
            
        $params = [
            'index' => 'items',
            'id' => $item["id"],
            'body' => ["name" => $item["name"]],
        ];

        $response = $this->elastic->index($params);
        return $response;
    }

    public function get($complete_or_partial_item){

        if(gettype($complete_or_partial_item) == 'string'){
            $item["name"] = $complete_or_partial_item;
        }
        if(gettype($complete_or_partial_item) == 'integer'){
            $item["id"] = $complete_or_partial_item;
        }
        if(gettype($complete_or_partial_item) == 'array'){
            $item = $complete_or_partial_item;
        }
        

        $params = [
            'index' => 'items',
            'body' => [
                'query' => [
                    'match' => $item
                ]
            ]
        ];


        $response = $this->elastic->search($params);

        return $response;
    }
}