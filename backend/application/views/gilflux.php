
 <div id="page-title" class="row" style="text-align:center">
        <h1 class="auto">Gilflux</h1>
        </div>
        <div class="container" style="max-width:98vw">
            <div class="row">
                <div class="row">
                    <div class="col-11">
                        <div class="input-group mb-3">
                            <select class="form-select" aria-label="Default select example" id="location_select">
                                <option value="none" selected disabled>Select a location for which to calculate Gilflux:</option>
                            </select>
                            <div class="input-group-append">
                                <div class="input-group-text">
                                    <input class="form-check-input" type="checkbox" id="crafted_only_checkbox" checked>
                                    <label class="form-check-label" for="checkbox">
                                        Crafted only
                                    </label>
                                </div>
                            </div>
                        </div>
                    </div>
                    <div class="col-1">
                        <button type="button" class="btn btn-primary" id="submit_request_button">Submit</button>
                    </div>
                </div>

                <div class="row message-row">
                    <?php echo isset($message) ? $message : ''?>
                </div>
                <div class="row content">
                    <div class="accordion" id="gilflux_accordion">

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

                if($("#location_select").val() == "none" || $("#location_select").val() == null){
                    alert("Please select a location");
                    return;
                }

                //Remove message row content
                $(".message-row").html("");                

                //check the value of the inputs
                var crafted_only = $("#crafted_only_checkbox").is(":checked");
                var location = $("#location_select").val();
                var request_id = (crafted_only + location).split("")
                                                            .map(c => c.charCodeAt(0).toString(16).padStart(2, "0"))
                                                            .join("");

                //If a div with this request_id already exists, remove it
                if($("div[request_id='"+request_id+"']").length > 0){
                    $("div[request_id='"+request_id+"']").remove();
                }


                //Creaete accordion item with placeholder values
                var accordion_item = `<div class='accordion-item' request_id='`+request_id+`'id='`+`PLACEHOLDER_ID`+`'>`+
                            `<h2 class='accordion-header' id='heading-`+`PLACEHOLDER_ID`+`'>`+
                                `<button class='accordion-button' type='button' data-bs-toggle='collapse' data-bs-target='#collapse-`+`PLACEHOLDER_ID`+`' aria-expanded='true' aria-controls='collapseOne'>`+
                                    (crafted_only ? ' Crafted Only - ' : '')  + `[` + location + `]`+
                                `</button>`+
                            `</h2>`+    
                            `<div id='collapse-`+`PLACEHOLDER_ID`+`' class='accordion-collapse collapse show' aria-labelledby='headingOne' data-bs-parent='#item_product_profit_accordion'>`+
                                `<div class='accordion-body' id="accordion-body-`+`PLACEHOLDER_ID`+`">`+
                                 `<div class="loading-div">`+
                                 `</div>`+
                                `</div>`+
                            `</div>`+
                        `</div>`;

                //Append it to the accordion
                $("div#gilflux_accordion").prepend(accordion_item);
                createProgressBar($("div#gilflux_accordion")[0]);


                //AJAX Rrequest for the data
                $.ajax({
                    url: "https://mtvirux.app/api/v1/gilflux",
                    type: "GET",
                    data: {
                        target_location: location,
                        crafted_only: crafted_only ? 1 : 0,
                        request_id: request_id,
                    },
                    success: function (data) {

                        if(data.status != "success" && data.status !== true){
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

                        
                        var table = $("<table class='table' id='table-"+data.request_id+"'>");
                        var headers = $('<thead>');
                        var headers_row = $('<tr>');
        
                        headers_row.append($('<th> ID </th>'));
                        headers_row.append($('<th> Item Name</th>'));
                        headers_row.append($('<th> 1h </th>'));
                        headers_row.append($('<th> 3h </th>'));
                        headers_row.append($('<th> 6h </th>'));
                        headers_row.append($('<th> 12h </th>'));
                        headers_row.append($('<th> 1d </th>'));
                        headers_row.append($('<th> 3d </th>'));
                        headers_row.append($('<th> 7d </th>'));
                        headers_row.append($('<th> Universalis </th>'));
                        //headers.append($('<th> FFMT Score </th>'));

                        headers.append(headers_row);
                        headers.append($('</thead>'));

                        table.append(headers);
                        table.append($("<tbody>"));

                        $.each(JSON.parse(data.data), function(index, item){
                            var row = $("<tr></tr>");

                            //Separate numbers with commas
                            item_id = item.item_id;
                            item_name = item.item_name;
                            //updated_at = item.updated_at
                            ranking_1h = item.ranking_1h.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ",");
                            ranking_3h = item.ranking_3h.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ",");
                            ranking_6h = item.ranking_6h.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ",");
                            ranking_12h = item.ranking_12h.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ",");
                            ranking_1d = item.ranking_1d.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ",");
                            ranking_3d = item.ranking_3d.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ",");
                            ranking_7d = item.ranking_7d.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ",");
                            //ffmt_score = 0;

                            row.addClass(item_id)

                            row.append($("<td>"+item_id+"</td>"));
                            row.append($("<td>"+item_name+"</td>"));
                            row.append($("<td>"+ranking_1h+"</td>"));
                            row.append($("<td>"+ranking_3h+"</td>"));
                            row.append($("<td>"+ranking_6h+"</td>"));
                            row.append($("<td>"+ranking_12h+"</td>"));
                            row.append($("<td>"+ranking_1d+"</td>"));
                            row.append($("<td>"+ranking_3d+"</td>"));
                            row.append($("<td>"+ranking_7d+"</td>"));
                            row.append($("<td><a href='https://universalis.app/market/"+item_id+"' target='_blank'>Link</a></td>"));
                            //row.append($("<td>"+ffmt_score+"</td>"));

                            table.append(row);

                            //
                        });

                        table.append($("</tbody>"));

                        table.append("</table>");

                        $("#accordion-body-"+data.request_id).html(table);


                        $("#table-"+data.request_id).find("[tooltip-text]").each(function(index,element){
                            new bootstrap.Tooltip(element, {
                                title: $(element).attr("tooltip-text"),
                                placement: "top",
                                trigger: "hover"
                            });
                        });

                        $("#table-" + data.request_id).DataTable({
                            order: [[2, 'desc']]
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
                jQuery(parent_element).children().find("div.loading-div").html(progressBar).append("<i>Calculating gilflux is a process that might take between 1s and 10min. The duration is inversely proportional to the traffic of the Unviersalis websocket. <br>Yes, <b>INVERSELY</b>. No, I'm not crazy.</i>");

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
                    url: "https://mtvirux.app/api/v1/worlds",
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
                .dataTables_wrapper .dataTables_length select{
                color:white !important;
            }

            .dataTables_length>label>select>option{
                color:white !important;
            }
            
            .dataTables_length>label>select>option:not(:checked){
                color:black !important;
            }
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