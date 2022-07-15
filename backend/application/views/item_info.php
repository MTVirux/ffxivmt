<html>

    <head>
        <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/css/bootstrap.min.css" rel="stylesheet"
            integrity="sha384-1BmE4kWBq78iYhFldvKuhfTAU6auU8tT94WrHftjDbrCEXSU1oBoqyl2QvZ6jIW3" crossorigin="anonymous">
        <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/js/bootstrap.bundle.min.js"
            integrity="sha384-ka7Sk0Gln4gmtz2MlQnikT1wXgYsOg+OMhuP+IlRH9sENBO0LRn5q+8nbTov4+1p" crossorigin="anonymous">
        </script>
        <script src="https://code.jquery.com/jquery-3.6.0.min.js" integrity="sha256-/xUj+3OJU5yExlq6GSYGSHk7tPXikynS7ogEvDej/m4=" crossorigin="anonymous"></script>
    </head>

    <?php 
        $retainers = array();
        foreach ($retainer_array as $server){
            foreach($server as $world){
                foreach($world as $character){
                    foreach($character as $retainer)
                    {
                        $retainers[] = $retainer;
                    }
                }
            }
        }
    ?>

    <body>
        <div class="container">
            <div class="row">
                <div class="row">
                    <div class="col-lg-12">
                            <h2><?=$item->name?></h2>
                    </div>
                </div>
            </div>
            <div class="row">
                <div class="col-lg-12">
                    <table class="table">
                        <thead>
                            <?php foreach($item as $key=>$value){
                            if($key != 'prices'){
                                echo '<th>';
                                echo $key;
                                echo '</th>';
                            }
                        }?>
                        </thead>
                        <tr>
                            <?php foreach($item as $key=>$value){
                                if($key != 'prices'){
                                    echo '<td>';
                                    echo $value;
                                    echo '</td>';
                                }
                            }?>
                        </tr>
                    </table>
            </div> <!-- END COL -->
                        </div>
            
            
                <div class="row">
                <div class="col-lg-6">

                    <table class="table">
                        <thead>
                            <th>Worldname</th>
                            <th>Price</th>
                            <th>Quantity</th>
                            <th>Last Updated</th>
                            <th>Retainer</th>
                        </thead>
                        <?php foreach($item->prices->listings as $listing){?>
                            <?php 

                            if(key_exists($listing->worldName, $retainer_array["Chaos"])){
                                if (!in_array($listing->retainerName ,$retainer_array["Chaos"][$listing->worldName])){
                                        continue;
                                    }
                            }
                            ?>
                        <tr
                            data-world = "<?=$listing->worldName?>"
                            data-price = "<?=$listing->pricePerUnit?>"
                            data-quantity = "<?=$listing->quantity?>"
                            data-lastupdate = "<?=$listing->lastReviewTime?>"
                            data-retainer =  "<?=$listing->retainerName?>"
                            >
                            <td><?=$listing->worldName?></td>
                            <td><?=$listing->pricePerUnit?></td>
                            <td><?=$listing->quantity?></td>
                            <td><?=$listing->lastReviewTime?></td>
                            <td><?=$listing->retainerName?></td>
                        </tr>
                        <?php }?>
                    </table>
                    


                    ?>

                </div>
                
                <div class="col-lg-6">
                <table class="table">
                        <thead>
                            <th>Worldname</th>
                            <th>Price</th>
                            <th>Quantity</th>
                            <th>Last Updated</th>
                            <th>Retainer</th>
                        </thead>
                        <?php foreach($item->prices->listings as $listing){?>
                            <?php 
                                if (!in_array($listing->retainerName, $retainers)){
                                    continue;
                                }
                            ?>
                        <tr
                            data-world = "<?=$listing->worldName?>"
                            data-price = "<?=$listing->pricePerUnit?>"
                            data-quantity = "<?=$listing->quantity?>"
                            data-lastupdate = "<?=$listing->lastReviewTime?>"
                            data-retainer =  "<?=$listing->retainerName?>"
                            >
                            <td><?=$listing->worldName?></td>
                            <td><?=$listing->pricePerUnit?></td>
                            <td><?=$listing->quantity?></td>
                            <td><?=$listing->lastReviewTime?></td>
                            <td><?=$listing->retainerName?></td>
                        </tr>
                        <?php }?>
                    </table>
                </div>
            </div> <!--END ROW-->
        </div>
    </body>

</html>

<script>

$(document).ready(function () {

    $('')

});



</script>
