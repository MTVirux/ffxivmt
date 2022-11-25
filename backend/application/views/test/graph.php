

      <div id="page-title" class="row" style="text-align:center; margin-top:3vh;margin-bottom:3vh;">
        <h1 class="auto">24h Top Gil Flux [<?=$world?>] [<?=date('Y-m-d H:i:s', time() - 24*60*60) . ' ~ '.date('Y-m-d H:i:s', time())?>]</h1>
      </div>    
      <div class="separator" style="height:1vh"></div> 
      <div class="accordion" id="accordionExample">
        <div class="accordion-item">
          <h2 class="accordion-header" id="headingOne">
            <button class="accordion-button collapsed" type="button" data-bs-toggle="collapse" data-bs-target="#chartTab" aria-expanded="false" aria-controls="chartTab">
              Chart
            </button>
          </h2>
          <div id="chartTab" class="accordion-collapse collapse" aria-labelledby="headingOne">
            <div class="accordion-body">
              <div class="flex row center" style="flex-direction:column; align-content:center">
                <canvas id="myChart" style="max-height:70vh;max-width:70vw;"></canvas>
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
        </div>
        <div class="accordion-item">
          <h2 class="accordion-header" id="headingTwo">
            <button class="accordion-button" type="button" data-bs-toggle="collapse" data-bs-target="#TableTab" aria-expanded="true" aria-controls="TableTab">
              Table
            </button>
          </h2>
          <div id="TableTab" class="accordion-collapse collapse show" aria-labelledby="headingTwo">
            <div class="accordion-body">
              <div class="container-fluid flex" style="max-height:65vh;overflow:hidden; overflow-y:scroll">
                  <?php echo $data?>
              </div>
            </div>
          </div>
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


  document.addEventListener("DOMContentLoaded", function(){
// make it as accordion for smaller screens
if (window.innerWidth < 992) {

  // close all inner dropdowns when parent is closed
  document.querySelectorAll('.navbar .dropdown').forEach(function(everydropdown){
    everydropdown.addEventListener('hidden.bs.dropdown', function () {
      // after dropdown is hidden, then find all submenus
        this.querySelectorAll('.submenu').forEach(function(everysubmenu){
          // hide every submenu as well
          everysubmenu.style.display = 'none';
        });
    })
  });

  document.querySelectorAll('.dropdown-menu a').forEach(function(element){
    element.addEventListener('click', function (e) {
        let nextEl = this.nextElementSibling;
        if(nextEl && nextEl.classList.contains('submenu')) {	
          // prevent opening link if link needs to open dropdown
          e.preventDefault();
          if(nextEl.style.display == 'block'){
            nextEl.style.display = 'none';
          } else {
            nextEl.style.display = 'block';
          }

        }
    });
  })
}
// end if innerWidth
}); 
</script>

