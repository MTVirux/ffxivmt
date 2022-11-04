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
        <h1 class="auto">24h Top Gil Flux [<?=$world?>] [<?=date('Y-m-d H:i:s', time() - 24*60*60) . ' ~ '.date('Y-m-d H:i:s', time())?>]</h1>
      </div>
      <div class="container" style="max-width:98vw">
      <div class="row">
          <div class="col-md-7">
              <?php echo $data?>
          </div>
          <div class="col-md-5">
          <canvas id="myChart" style="max-height:90vh;max-width:90vw;"></canvas>
          <?php
              $names = [];
              for($i = 0 ; $i < 10; $i++){
                  $names[] = $raw_data[$i]['name'].' ('.$raw_data[$i]['item_id'].')';
                  $values[] = $raw_data[$i]['1d'];
              }
              $labels =  '"'.implode('","', $names).'"' . PHP_EOL;
              $datasets =  implode(",", $values) . PHP_EOL;
            ?>
          </div>
        </div>
      </div>
    </body>
</html>


<script>
  const labels = [
    <?php echo $labels; ?>
  ];

  const data = {
    labels: labels,
    datasets: [{
      label: 'Past 24 Hours Gold Volume (<?php echo $world; ?>)',
      backgroundColor: 'rgb(0, 0, 0)',
      borderColor: 'rgb(0, 0, 0)',
      data: [<?php echo $datasets; ?>],
    }]
  };

  const config = {
    type: 'bar',
    data: data,
    options: {
        title: "My chart title"
    }
  };

  const myChart = new Chart(
    document.getElementById('myChart'),
    config
  );
</script>
 