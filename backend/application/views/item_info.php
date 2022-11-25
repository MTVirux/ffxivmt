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
    $use_retainers = true; 
    if(is_null($retainer_array) || empty($retainer_array)){
        $use_retainers = false;
    
    }
        if($use_retainers == true){
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
        }
    ?>

    <body>
        <div class="container">
            <div class="row"><br></div>
            <div class="row">
                <?php $this->load_view_template('search')?>
            </div>

            <div class="row"><br></div>

            <div class="accordion" id="accordionExample">
                <div class="accordion-item">
                    <h2 class="accordion-header" id="headingOne">
                    <button class="accordion-button" type="button" data-bs-toggle="collapse" data-bs-target="#collapseOne" aria-expanded="true" aria-controls="collapseOne">
                        <h2><?=$item->name?></h2>
                    </button>
                    </h2>
                    <div id="collapseOne" class="accordion-collapse collapse" aria-labelledby="headingOne" data-bs-parent="#accordionExample">
                        <div class="accordion-body">
                            <?php $this->load_view_template('item_info_table', true)?>
                        </div>
                    </div>
                </div>
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
                            foreach($retainer_array as $datacenterName => $worldName){
                                if(key_exists($listing->worldName, $datacenterName)){
                                    if (!in_array($listing->retainerName ,$retainer_array[$datacenterName][$listing->worldName])){
                                            continue;
                                        }
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
                <?php if($use_retainers): ?>
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
                <?php endif ?>
            </div> <!--END ROW-->
        </div>
    </body>

</html>

<script>

$(document).ready(function () {

    $('')

});



</script>
