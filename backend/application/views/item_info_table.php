<div class="row">
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
</div> <!-- END ROW -->