
        <div id="page-title" class="row" style="text-align:center">
        <h1 class="auto">Currency Profit Calculator</h1>
        </div>
        <div class="container" style="max-width:98vw">
            <div class="row">
                <div class="row">
                    <div class="col-11">
                        <div class="input-group mb-3">
                            <span class="input-group-text" id="basic-addon1">Currency:</span>
                            <select class="form-select" aria-label="Default select example" id="currency_select">
                                <option value="none" selected disabled>Select a currency:</option>

                                <option value="none"disabled>PvE:</option>
                                <option value="Allagan Tomestone of Causality">Allagan Tomestone of Causality (Green and White)</option>
                                <!--<option value="Allagan Tomestone of Comedy">Allagan Tomestone of Comedy (Red and Green)</option>-->
                                <option value="Allagan Tomestone of Poetics">Allagan Tomestone of Poetics</option>

                                <option value="none"disabled>Disciples of Hand/Land:</option>
                                <option value="Purple Crafters' Scrip">Orange Crafters' Scrip</option>
                                <option value="White Crafters' Scrip">Purple Crafters' Scrip</option>
                                <option value="Purple Gatherers' Scrip">Orange Gatherers' Scrip</option>
                                <option value="White Gatherers' Scrip">Purple Gatherers' Scrip</option>
                                <option value="Skybuilders' Scrip">Skybuilders' Scrip</option>

                                <option value="none"disabled>The Hunt:</option>
                                <option value="Allied Seal">Allied Seal</option>
                                <option value="Centurio Seal">Centurio Seal</option>
                                <option value="Sack of Nuts">Sack of Nuts</option>

                                <option value="none"disabled>PvP:</option>
                                <option value="Wolf Marks">Wolf Marks</option>
                                <option value="Trophy Crystal">Trophy Crystal</option>
                                
                                <option value="none"disabled>Others:</option>
                                <option value="Bicolor Gemstone">Bicolor Gemstone</option>
                                <option value="Storm Seal">Storm Seal</option>
                                <option value="MGP">MGP</option>
                            </select>

                            <select class="form-select" aria-label="Default select example" id="location_select">
                                <option value="none" selected disabled>Select a location for which profits will be calculated:</option>
                            </select>
                        </div>
                    </div>
                    <div class="col-1">
                        <button id="submit_request_button">Submit</button>
                    </div>
                </div>

                <div class="row message-row">
                    <?php echo isset($message) ? $message : ''?>
                </div>
                <div class="row content">
                    <div class="accordion" id="item_product_profit_accordion">

                    </div>
                </div>
            </div>
        </div>
    <script>

            $(document).ready(function () {
                //randomize_placeholder_text();
                //animate_loading_text();
                createWorldOptions($("#location_select")[0]);
                ident_currency_options()
            });

            function ident_currency_options(){
                $('#currency_select').find('option').each(function() {
                    if ($(this).is(':disabled') == false && $(this).value !== "none") {
                        $(this).text("\u00a0\u00a0\u00a0\u00a0" + $(this).text());
                    }
                });
            }

            function ident_world_options(){
                $('option.datacenter').each(function() {
                    if ($(this).val() !== "none") {
                        $(this).text("\u00a0\u00a0\u00a0\u00a0" + $(this).text());
                    }
                });

                $('option.world').each(function() {
                    if ($(this).val() !== "none") {
                        $(this).text("\u00a0\u00a0\u00a0\u00a0\u00a0\u00a0\u00a0\u00a0" + $(this).text());
                    }
                });

            }

            $("#submit_request_button").click(function () {

                if($("#currency_select").val() == "none" || $("#currency_select").val() == null){
                    alert("Please select a currency");
                    return;
                }

                if($("#location_select").val() == "none" || $("#location_select").val() == null){
                    alert("Please select a location");
                    return;
                }


                $(".message-row").html("");                

                var currency_id = $("#currency_select").val();
                var currency_name = $("#currency_select option:selected").text();
                var location = $("#location_select").val();
                var request_id = (currency_id + location)   .split("")
                                                            .map(c => c.charCodeAt(0).toString(16).padStart(2, "0"))
                                                            .join("");

                if($("div[request_id='"+request_id+"']").length > 0){
                    $("div[request_id='"+request_id+"']").remove();
                }

                var accordion_item = `<div class='accordion-item' request_id='`+request_id+`'id='`+`PLACEHOLDER_ID`+`'>`+
                            `<h2 class='accordion-header' id='heading-`+`PLACEHOLDER_ID`+`'>`+
                                `<button class='accordion-button' type='button' data-bs-toggle='collapse' data-bs-target='#collapse-`+`PLACEHOLDER_ID`+`' aria-expanded='true' aria-controls='collapseOne'>`+
                                    currency_name + `-[` + location + `]`+
                                `</button>`+
                            `</h2>`+
                            `<div id='collapse-`+`PLACEHOLDER_ID`+`' class='accordion-collapse collapse show' aria-labelledby='headingOne' data-bs-parent='#item_product_profit_accordion'>`+
                                `<div class='accordion-body' id="accordion-body-`+`PLACEHOLDER_ID`+`">`+
                                 `<div class="loading-div">`+
                                 `</div>`+
                                `</div>`+
                            `</div>`+
                        `</div>`;
                $("div#item_product_profit_accordion").prepend(accordion_item);
                createProgressBar($("div#item_product_profit_accordion")[0]);


                $.ajax({
                    url: "<?=base_url('/test/currency_efficiency_calculator')?>",
                    type: "POST",
                    data: {
                        currency_id: currency_id,
                        location: location,
                        request_id: request_id,
                    },
                    success: function (data) {
                        var data = JSON.parse(data);

                        if(data.status != "success"){
                            $(".message-row").html(data.message);
                            return;
                        }

                        $("div.accordion-item[request_id='"+data.request_id+"']").attr("id", data.request_id);
                        $("div.accordion-item[request_id='"+data.request_id+"']").html(
                            $("div.accordion-item[request_id='"+data.request_id+"']").html()
                                .replaceAll("PLACEHOLDER_NAME", data.item_name)
                                .replaceAll("PLACEHOLDER_LOCATION", data.location)
                                .replaceAll("PLACEHOLDER_ID", data.request_id)
                        );

                        
                        var table = $("<table class='table table-striped table-bordered table-hover' id='table-"+data.request_id+"'>");
                        var headers = $('<thead>');
                        //headers.append($('<th> ID </th>'));
                        headers.append($('<th> Item Name</th>'));
                        headers.append($('<th> Currency Cost (?)</th>').attr("tooltip-text", "The cost of the item in the currency you selected"));
                        headers.append($('<th> MSS (?)</th>').attr("tooltip-text", "The median stack size of the item listings"));
                        headers.append($('<th> Min MB Price</th>'));
                        headers.append($('<th> RSV (?)</th>').attr("tooltip-text", "Regular Sale Velocity, the number of times the item has been sold in the last 24h"));
                        headers.append($('<th> DMC (?)</th>').attr("tooltip-text", "Daily Market Cap: The total amount gil moved in the last 24h"));
                        headers.append($('<th> DMC% (?)</th>').attr("tooltip-text", "Daily Market Cap Percent: The percentage of the total market cap for all items of this currency that the item represents"));
                        headers.append($('<th> RDA (?)</th>').attr("tooltip-text", "Recommended Daily Amount, the amount of the item you should try to sell daily to maximize your profit."));
                        headers.append($('<th> FFMT Score (?)</th>').attr("tooltip-text", "The score of the item based on the FFMT algorithm (higher is better)"));
                        headers.append($('<th> Universalis</th>'));
                        headers.append($('</thead>'));

                        table.append(headers);
                        table.append($("<tbody>"));

                        gil_price_sum = 0;
                        currency_price_sum = 0;

                        $.each(data.data, function(index, item){
                            var row = $("<tr></tr>");

                            //Prepare table data
                            id = item.id
                            name = item.name
                            price = item.price
                            medianStackSize = item.medianStackSize
                            minPrice = item.minPrice
                            regularSaleVelocity = item.regularSaleVelocity
                            dailyMarketCap = item.dailyMarketCap
                            dailyMarketCapPercent = item.dailyMarketCapPercent
                            recommendedAmountToCraftDaily = Math.round(item.dailyMarketCap/item.minPrice)
                            ffmt_score = item.mtvirux_score
                            universalisLink = '<a href=https://universalis.app/market/'+id+'>Link</a>'

                            //Stat vars
                            gil_price_sum = gil_price_sum + minPrice
                            currency_price_sum = currency_price_sum + price

                            //Append table data
                            //row.append($("<td>"+id+"</td>"));
                            row.append($("<td>"+name+"</td>"));
                            row.append($("<td>"+price+"</td>"));
                            row.append($("<td>"+medianStackSize+"</td>"));
                            row.append($("<td>"+minPrice+"</td>"));
                            row.append($("<td>"+regularSaleVelocity+"</td>"));
                            row.append($("<td>"+dailyMarketCap+"</td>"));
                            row.append($("<td>"+dailyMarketCapPercent+"%</td>"));
                            row.append($("<td>"+recommendedAmountToCraftDaily+"</td>"));
                            row.append($("<td>"+ffmt_score+"</td>"));
                            row.append($("<td>"+universalisLink+"</td>"));
                            table.append(row);
                        });
                        table.append($("</tbody>"));
                        table.append($("</table>"));

                        //currency_info = ($("<span class=\"currency-unit-average-value-title\">Currency Unit Average Value: </span> <span class=\"currency-unit-average-value-value\">"+((gil_price_sum / currency_price_sum))+"</span>"));

                        currency_info = $("<span>Currency Unit Average Value: " + (gil_price_sum / currency_price_sum)+"</span>")


                        $("#accordion-body-"+data.request_id).html(currency_info[0].outerHTML + table[0].outerHTML);

                        $("#table-"+data.request_id).find("[tooltip-text]").each(function(index,element){
                            new bootstrap.Tooltip(element, {
                                title: $(element).attr("tooltip-text"),
                                placement: "top",
                                trigger: "hover"
                            });
                        });

                        $("#table-" + data.request_id).DataTable({
                            order: [[9, 'desc']]
                        });

                    },
                    error: function (data) {
                        $("div.accordion-item[request_id='"+data.request_id+"']").remove();
                        $("#PLACEHOLDER_ID").remove();

                    }
                });
            }); 

            function createProgressBar(parent_element) {
                var progressBar = $('<div>', { class: 'progress' });
                var progressBarInner = $('<div>', { class: 'progress-bar progress-bar-striped progress-bar-animated', role: 'progressbar', 'aria-valuenow': '0', 'aria-valuemin': '0', 'aria-valuemax': '100' });
                progressBar.append(progressBarInner);
                jQuery(parent_element).children().find("div.loading-div").html(progressBar);

                currentValue = 0;
                intervalId = setInterval(function() {
                    currentValue += 1;
                    progressBarInner.css('width', currentValue + '%');
                    progressBarInner.css('background-color', '#990099');
                    progressBarInner.css('color', '#FF00FF');
                    progressBarInner.attr('aria-valuenow', currentValue);
                    if (currentValue >= 99) {
                        clearInterval(intervalId);
                    }
                }, 40);
            }

            function createWorldOptions(parent_element){
                //AJAX GET REQUEST
                $.ajax({
                    url: "<?=base_url('api/v1/worlds')?>",
                    type: "GET",
                    success: function (data) {

                        if(data.status != true){
                            $(".message-row").html(data.message);
                            return;
                        }
                        options_string = "";

                        $.each(data.data, function(index, region_data){
                            options_string = options_string + "<option class='region' value='"+index+"'>"+index+"</option>";
                            $.each(region_data, function(index, datacenter_data){
                                options_string = options_string + "<option class='datacenter' value='"+index+"'>"+index+"</option>";
                                $.each(datacenter_data, function(index, world){
                                    options_string = options_string + "<option class='world' value='"+world+"'>"+world+"</option>";
                                });
                            });
                        });
                        
                        original_html = jQuery(parent_element).html()
                        jQuery(parent_element).html(original_html + options_string);
                        ident_world_options();
                    },
                    error: function (data) {
                        console.log(data);
                    }
                });
            }
        </script>
        <style>
            --#{$prefix}tooltip-zindex: #{$zindex-tooltip};
            --#{$prefix}tooltip-max-width: #{$tooltip-max-width};
            --#{$prefix}tooltip-padding-x: #{$tooltip-padding-x};
            --#{$prefix}tooltip-padding-y: #{$tooltip-padding-y};
            --#{$prefix}tooltip-margin: #{$tooltip-margin};
            @include rfs($tooltip-font-size, --#{$prefix}tooltip-font-size);
            --#{$prefix}tooltip-color: #{$tooltip-color};
            --#{$prefix}tooltip-bg: #{$tooltip-bg};
            --#{$prefix}tooltip-border-radius: #{$tooltip-border-radius};
            --#{$prefix}tooltip-opacity: #{$tooltip-opacity};
            --#{$prefix}tooltip-arrow-width: #{$tooltip-arrow-width};
            --#{$prefix}tooltip-arrow-height: #{$tooltip-arrow-height};
        </style>