<html>

    <h1>USAGE:</h1>

    <?=base_url/test/get_world_scores?>?<b>world_name</b>="Spriggan"</b><br>
    world_name -> world_name takes the name of any world in the game and preforms a search on all sales data of that world.
    Test: <a href="<?=base_url/test/get_world_scores?>?world_name="Spriggan"></a>

    <?=base_url/test/get_world_scores?>?<b>dc_name</b>="Chaos"</b><br>
    dc_name -> dc_name takes the name of any dc in the game and preforms a search on all sales data of all worlds of that dc.<br>
    Test: <a href="<?=base_url/test/get_dc_scores?>?dc_name="Chaos"></a>

</html>