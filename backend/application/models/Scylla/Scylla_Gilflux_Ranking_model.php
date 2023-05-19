<?php
defined ('BASEPATH') OR exit('No direct script access allowed');
require_once APPPATH.'core/MY_Scylla_Model.php';
include_once APPPATH.'vendor/uri2x/php-cql/src/Cassandra.php';


class Scylla_Gilflux_Ranking_model extends MY_Scylla_Model{
    
    function __construct() {
        parent::__construct();

        define("DAY_IN_MS", 86400000);
        define("HOUR_IN_MS", 3600000);
    }
    

    function update_ranking($world_id, $item_id){

        //Get world info
        $stmt_world = $this->scylla->prepare("SELECT * FROM worlds WHERE id = ?");
        $result_world = $this->scylla->execute($stmt_world, array("id" => $world_id));
        $world_name = $result_world[0]["name"];
        $datacenter = $result_world[0]["datacenter"];
        $region = $result_world[0]["region"];
        $this->load->model('Scylla/Scylla_Item_model', 'Scylla_Items');
        $stmt_item = $this->scylla->prepare("SELECT name FROM items WHERE id = ?");
        $result_item = $this->scylla->execute($stmt_item, array("id" => $item_id));


        //1 hour
        $stmt_1h = $this->scylla->prepare("SELECT item_id, world_id, item_name, world_name, datacenter, region, CAST(SUM(total) AS BIGINT) as gilflux
        FROM gilflux 
        WHERE item_id = ? AND world_id = ? AND datacenter = ? AND region = ? AND sale_time >= ?
        GROUP BY item_id, world_id, datacenter, region
        ");

        $result_1h = $this->scylla->execute($stmt_1h, array("item_id" => $item_id, "world_id" => $world_id, "region" => $region, "datacenter" => $datacenter, "sale_time" => ((time()*1000) - (1* (HOUR_IN_MS)))));

        //3 hours
        $stmt_3h = $this->scylla->prepare("SELECT item_id, world_id, item_name, world_name, datacenter, region, CAST(SUM(total) AS BIGINT) as gilflux
        FROM gilflux 
        WHERE item_id = ? AND world_id = ? AND datacenter = ? AND region = ? AND sale_time >= ?
        GROUP BY item_id, world_id, datacenter, region
        ");

        $result_3h = $this->scylla->execute($stmt_3h, array("item_id" => $item_id, "world_id" => $world_id, "region" => $region, "datacenter" => $datacenter, "sale_time" => ((time()*1000) - (3 * (HOUR_IN_MS)))));

        //6 hours
        $stmt_6h = $this->scylla->prepare("SELECT item_id, world_id, item_name, world_name, datacenter, region, CAST(SUM(total) AS BIGINT) as gilflux
        FROM gilflux 
        WHERE item_id = ? AND world_id = ? AND datacenter = ? AND region = ? AND sale_time >= ?
        GROUP BY item_id, world_id, datacenter, region
        ");

        $result_6h = $this->scylla->execute($stmt_6h, array("item_id" => $item_id, "world_id" => $world_id, "region" => $region, "datacenter" => $datacenter, "sale_time" => ((time()*1000) - (6 * (HOUR_IN_MS)))));

        //12 hours
        $stmt_12h = $this->scylla->prepare("SELECT item_id, world_id, item_name, world_name, datacenter, region, SUM(total) as gilflux
        FROM gilflux 
        WHERE item_id = ? AND world_id = ? AND datacenter = ? AND region = ? AND sale_time >= ?
        GROUP BY item_id, world_id, datacenter, region
        ");

        $result_12h = $this->scylla->execute($stmt_12h, array("item_id" => $item_id, "world_id" => $world_id, "region" => $region, "datacenter" => $datacenter, "sale_time" => ((time()*1000) - (12 * (HOUR_IN_MS)))));

        //1 day
        $stmt_1d = $this->scylla->prepare("SELECT item_id, world_id, item_name, world_name, datacenter, region, CAST(SUM(total) AS BIGINT) as gilflux
        FROM gilflux 
        WHERE item_id = ? AND world_id = ? AND datacenter = ? AND region = ? AND sale_time >= ?
        GROUP BY item_id, world_id, datacenter, region
        ");

        $result_1d = $this->scylla->execute($stmt_1d, array("item_id" => $item_id, "world_id" => $world_id, "region" => $region, "datacenter" => $datacenter, "sale_time" => ((time()*1000) - (1 * (DAY_IN_MS)))));
        

        //3 days
        $stmt_3d = $this->scylla->prepare("SELECT item_id, world_id, item_name, world_name, datacenter, region, CAST(SUM(total) AS BIGINT) as gilflux
        FROM gilflux 
        WHERE item_id = ? AND world_id = ? AND datacenter = ? AND region = ? AND sale_time >= ?
        GROUP BY item_id, world_id, datacenter, region
        ");

        $result_3d = $this->scylla->execute($stmt_3d, array("item_id" => $item_id, "world_id" => $world_id, "region" => $region, "datacenter" => $datacenter, "sale_time" => ((time()*1000) - (3 * (DAY_IN_MS)))));

        //7 days
        $stmt_7d = $this->scylla->prepare("SELECT item_id, world_id, item_name, world_name, datacenter, region, CAST(SUM(total) AS BIGINT) as gilflux
        FROM gilflux 
        WHERE item_id = ? AND world_id = ? AND datacenter = ? AND region = ? AND sale_time >= ?
        GROUP BY item_id, world_id, datacenter, region
        ");

        $result_7d = $this->scylla->execute($stmt_7d, array("item_id" => $item_id, "world_id" => $world_id, "region" => $region, "datacenter" => $datacenter, "sale_time" => ((time()*1000) - (7 * (DAY_IN_MS)))));


        //Input the rankings onto the table
        $final_ranking = array(
            "item_id" => $item_id,
            "world_id" => $world_id,
            "datacenter" => $datacenter,
            "region" => $region,
            "item_name" => $result_item[0]["name"],
            "world_name" => $world_name,
            "ranking_alltime" => 0, 
            "ranking_1h"    =>  isset($result_1h[0]["gilflux"])     ?     $result_1h[0]["gilflux"] : 0,
            "ranking_3h"    =>  isset($result_3h[0]["gilflux"])     ?     $result_3h[0]["gilflux"] : 0,
            "ranking_6h"    =>  isset($result_6h[0]["gilflux"])     ?     $result_6h[0]["gilflux"] : 0,
            "ranking_12h"   =>  isset($result_12h[0]["gilflux"])    ?     $result_12h[0]["gilflux"] : 0,
            "ranking_1d"   =>  isset($result_1d[0]["gilflux"])      ?     $result_1d[0]["gilflux"] : 0,
            "ranking_3d"    =>  isset($result_3d[0]["gilflux"])     ?     $result_3d[0]["gilflux"] : 0,
            "ranking_7d"    =>  isset($result_7d[0]["gilflux"])     ?     $result_7d[0]["gilflux"] : 0,
            "updated_at"    =>  time()*1000
        );

        $stmt_insert = $this->scylla->prepare("
        INSERT INTO gilflux_ranking (item_id, world_id, datacenter, region, item_name, world_name, ranking_alltime, ranking_1h, ranking_3h, ranking_6h, ranking_12h, ranking_1d, ranking_3d, ranking_7d, updated_at)
        VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)" );

        $this->scylla->execute($stmt_insert, $final_ranking);

        logger("SCYLLA_DB", json_encode(array("controller" => "api/v1/updatedb/", "function" => "gilflux_ranking_update", "world_name" => $world_name, "item_name" => $result_1d[0]["item_name"], "world_id" => $world_id, "item_id" => $item_id)));

    }

    function get_by_world($world_id){

        $stmt = $this->scylla->prepare(
        "SELECT 
        item_id,
        world_id,
        datacenter,
        region,
        item_name,
        world_name,
        ranking_alltime,
        ranking_1h,
        ranking_3h,
        ranking_6h,
        ranking_12h,
        ranking_1d,
        ranking_3d,
        ranking_7d,
        updated_at
        FROM gilflux_ranking 
        WHERE world_id = ?
        ");

        $result = $this->scylla->execute($stmt, array("world_id" => $world_id));

        return $result;
    }


    function get_by_datacenter($datacenter_name){

        $stmt = $this->scylla->prepare(
            "SELECT 
            item_id,
            world_id,
            datacenter,
            region,
            item_name,
            world_name,
            ranking_alltime,
            ranking_1h,
            ranking_3h,
            ranking_6h,
            ranking_12h,
            ranking_1d,
            ranking_3d,
            ranking_7d,
            updated_at
            FROM gilflux_ranking 
            WHERE datacenter = ?
            ");
        $result = $this->scylla->execute($stmt, array("datacenter" => $datacenter_name));

        return $result;
    }

    function get_by_region($region_name){

        $stmt = $this->scylla->prepare(
            "SELECT 
            item_id,
            world_id,
            datacenter,
            region,
            item_name,
            world_name,
            ranking_alltime,
            ranking_1h,
            ranking_3h,
            ranking_6h,
            ranking_12h,
            ranking_1d,
            ranking_3d,
            ranking_7d,
            updated_at
            FROM gilflux_ranking 
            WHERE region = ?
            ");
        $result = $this->scylla->execute($stmt, array("region" => $region_name));

        return $result;
    }
}
