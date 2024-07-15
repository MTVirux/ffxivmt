
        <div id="page-title" class="row" style="text-align:center">
        <h1 class="auto">Item Product Profit Calculator</h1>
        </div>
        <div class="container" style="max-width:98vw">
            <div class="row">
                <div class="row">
                    <div class="col-11">
                    <div class="input-group mb-3">
                        <span class="input-group-text" id="basic-addon1">Item To Use In Craft</span>
                            <input id="search_term_text_input" type="text" class="form-control" placeholder="Shinryu's Wing" aria-label="Username" aria-describedby="basic-addon1">
                            <select class="form-select" aria-label="Default select example" id="location_select">
                                <option value="none" selected disabled>Select a Region</option>
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

            function randomize_placeholder_text(){
                setInterval(() => {
                    item_selection = ["Shinryu's Wing", "Inferno Horn", "Vortex Feather", "Blissful Shroud", "Namazu Whisker", "Mist Sewage"];
                    random_item = item_selection[Math.floor(Math.random() * item_selection.length)];
                    $("#search_term_text_input").attr("placeholder", random_item);
                }, 1000);
            }

            function animate_loading_text(){
                var loading_text_animation_index = 0;
                var loading_text_animation = ["Loading", "Loading.", "Loading..", "Loading..."];
                setInterval(() => {
                    //console.log($(".loading_text"))
                    $(".loading_text").html(loading_text_animation[loading_text_animation_index]);
                    loading_text_animation_index++;
                    if(loading_text_animation_index >= loading_text_animation.length){
                        loading_text_animation_index = 0;
                    }
                }, 500);
            }

            $(document).ready(function () {
                randomize_placeholder_text();
                animate_loading_text();
                createWorldOptions($("#location_select")[0]);
            });

            $("#submit_request_button").click(function () {

                if($("#location_select").val() == "none"){
                    alert("Please select a location for the search");
                    return;
                }

                $(".message-row").html("");                

                var search_term = $("#search_term_text_input").val();
                var location = $("#location_select").val();
                var request_id = (search_term + location)   .split("")
                                                            .map(c => c.charCodeAt(0).toString(16).padStart(2, "0"))
                                                            .join("");

                if($("div[request_id='"+request_id+"']").length > 0){
                    $("div[request_id='"+request_id+"']").remove();
                }

                var accordion_item = `<div class='accordion-item' request_id='`+request_id+`'id='`+`PLACEHOLDER_ITEM_ID`+`'>`+
                            `<h2 class='accordion-header' id='heading-`+`PLACEHOLDER_ID`+`'>`+
                                `<button class='accordion-button' type='button' data-bs-toggle='collapse' data-bs-target='#collapse-`+`PLACEHOLDER_ID`+`' aria-expanded='true' aria-controls='collapseOne'>`+
                                    search_term + `-[` + location + `]`+
                                `</button>`+
                            `</h2>`+
                            `<div id='collapse-`+`PLACEHOLDER_ID`+`' class='accordion-collapse collapse show' aria-labelledby='headingOne' data-bs-parent='#item_product_profit_accordion'>`+
                                `<div class='accordion-body' id="accordion-body-`+`PLACEHOLDER_ID`+`">`+
                                 `<div class="loading-div">`+
                                    `Fetching data<span class="loading-text"></span>`+
                                 `</div>`+
                                `</div>`+
                            `</div>`+
                        `</div>`;

                $("div#item_product_profit_accordion").prepend(accordion_item);

                $.ajax({
                    url: "<?=base_url('/api/v1/tools/item_product_profit_calculator')?>",
                    type: "GET",
                    data: {
                        search_term: search_term,
                        location: location,
                        request_id: request_id,
                    },
                    success: function (data) {

                        data = data.data

                        console.log(data)

                        if(data.status != "success"){
                            $(".message-row").html(data.message);
                            return;
                        }


                        response_accordion_item = $("div.accordion-item[request_id='"+data.request_id+"']");
                        $(response_accordion_item).find("button").html(data.item_name + " - [" + data.location + "]");
                        $("#"+data.item_id).remove();

                        response_accordion_item.attr("id", data.item_id);
                        response_accordion_item.html(
                            response_accordion_item.html()
                                .replaceAll("PLACEHOLDER_NAME", data.item_name)
                                .replaceAll("PLACEHOLDER_LOCATION", data.location)
                                .replaceAll("PLACEHOLDER_ID", data.request_id)
                                .replaceAll("PLACEHOLDER_ITEM_ID", data.item_id)
                        );
                        
                        var table = $("<table class='table table-striped table-hover table-bordered'>");
                        var thead = $("<thead>");
                        var headerRow = $("<tr>");
                        //headerRow.append($("<th>").text("ID"));
                        headerRow.append($("<th>").text("Name"));
                        headerRow.append($("<th>").text("Min Price"));
                        headerRow.append($("<th>").text("Regular Sale Velocity"));
                        headerRow.append($("<th>").text("FFMT Score"));
                        headerRow.append($("<th>").text("Universalis"));
                        thead.append(headerRow);
                        table.append(thead);

                        var tbody = $("<tbody>");
                        for (var key in data.data){
                            var row = $("<tr>");
                            //var idCell = $("<td>").text(data.data[key]["id"]);
                            var nameCell = $("<td>").text(data.data[key]["name"]);
                            var minPriceCell = $("<td>").text(data.data[key]["min_price"]);
                            var regularSaleVelocityCell = $("<td>").text(data.data[key]["regularSaleVelocity"]);
                            var UniversalisLink = $("<td>").html('<a href=https://universalis.app/market/'+data.data[key]["id"]+'>Link</a>')
                            var ffmtScoreCell = $("<td>").text(data.data[key]["ffmt_score"]);


                            //row.append(idCell);
                            row.append(nameCell);
                            row.append(minPriceCell);
                            row.append(regularSaleVelocityCell);
                            row.append(ffmtScoreCell);
                            row.append(UniversalisLink)
                            tbody.append(row);
                        }
                        table.append(tbody);
                        table.DataTable({
                            order: [[4, 'desc']]
                        });
                        $("body").append(table);


                        $("#accordion-body-"+data.request_id).html(table);



                    },
                    error: function (data) {
                        response_accordion_item = $("div.accordion-item[request_id='"+data.request_id+"']");
                        response_accordion_item.remove();
                        $("#PLACEHOLDER_ID").remove();
                    }
                });
            }); 

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
            };

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
</script>