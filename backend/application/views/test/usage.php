<html>

    <h1>USAGE:</h1>
 
    <h1><?=base_url('/test/get_world_scores')?>?<b>world_name</b>="Spriggan"</b></h1><br>
    <b>world_name</b> -> world_name takes the name of any world in the game and preforms a search on all sales data of that world.
    Test: <a href="<?=base_url('/test/get_world_scores')?>?world_name="Spriggan"></a>

</br>
</br>

    <h1><?=base_url('/test/get_dc_scores')?>?<b>dc_name</b>="Chaos"</b></h1><br>
    <b>dc_name</b> - dc_name takes the name of any dc in the game and preforms a search on all sales data of all worlds of that dc.<br>
    Test: <a href="<?=base_url('/test/get_dc_scores')?>?dc_name="Chaos"></a>

</html>