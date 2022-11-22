<!DOCTYPE html>
<html lang="en">
<head>
  <script src="<?php echo base_url('resources/external/chart.js')?>"></script>
  <script src="https://cdn.jsdelivr.net/npm/@popperjs/core@2.11.6/dist/umd/popper.min.js" integrity="sha384-oBqDVmMz9ATKxIep9tiCxS/Z9fNfEXiDAYTujMAeBAsjFuCZSmKbSSUnQlmh/jp3" crossorigin="anonymous" defer></script>
  <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.2.2/dist/css/bootstrap.min.css" rel="stylesheet" integrity="sha384-Zenh87qX5JnK2Jl0vWa8Ck2rdkQ2Bzep5IDxbcnCeuOxjzrPF/et3URy9Bv1WTRi" crossorigin="anonymous">
  <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.2.2/dist/js/bootstrap.bundle.min.js" integrity="sha384-OERcA2EqjJCMA+/3y+gxIOqMEjwtxJY7qPCqsdltbNJuaOe923+mo//f6V8Qbsw3" crossorigin="anonymous"></script>
  <title>Chart</title>
</head>
    <body>
      <div id="page-title" class="row" style="text-align:center">
        <h1 class="auto">Item Product Profit Calculator [<?=$world?>] - [<?=$item_name?>]</h1>
      </div>
      <div class="container" style="max-width:98vw">
      <div class="row">
          <div class="col-md-7">
              <?php echo $data?>
          </div>
          <div class="col-md-5">
          <canvas id="myChart" style="max-height:90vh;max-width:90vw;"></canvas>
          </div>
        </div>
      </div>
    </body>
</html>