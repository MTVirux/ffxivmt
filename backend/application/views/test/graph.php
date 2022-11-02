<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta http-equiv="X-UA-Compatible" content="IE=edge">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <script src="<?php echo base_url('resources/external/chart.js')?>"></script>
    <title>Chart</title>
</head>
    <body>
        <div>
            <h1>24h Top Gil Flux [<?=$world?>] [<?=date('Y-m-d H:i:s', time() - 24*60*60) . ' ~ '.date('Y-m-d H:i:s', time())?>]</h1>
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
        <?php echo $data?>
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
 