
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
                                <option value="none" selected>Select a currency</option>

                                <option value="none"disabled>PvE:</option>
                                <option value="Allagan Tomestone of Astronomy">Allagan Tomestone of Astronomy (Red and Black)</option>
                                <option value="Allagan Tomestone of Casuality">Allagan Tomestone of Casuality (Green and White)</option>
                                <option value="Allagan Tomestone of Poetics">Allagan Tomestone of Poetics</option>

                                <option value="none"disabled>Disciples of Hand/Land:</option>
                                <option value="White Crafters' Scrip">  White Crafters' Scrip</option>
                                <option value="Purple Crafters' Scrip"> Purple Crafters' Scrip</option>
                                <option value="White Gatherers' Scrip"> White Gatherers' Scrip</option>
                                <option value="Purple Gatherers' Scrip">Purple Gatherers' Scrip</option>
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
                                <option value="none" selected>Select a DC for which profits will be calculated:</option>
                                <option value="Chaos">Chaos</option>
                                <option value="Light">Light</option>
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
                ident_options();
            });

            function ident_options(){
                $('option').each(function() {
                    if ($(this).val() !== "none") {
                        $(this).text("\u00a0\u00a0\u00a0\u00a0" + $(this).text());
                    }
                });
            }

            $("#submit_request_button").click(function () {

                if($("#location_select").val() == "none"){
                    alert("Please select a location");
                    return;
                }

                $(".message-row").html("");                

                var currency_id = $("#currency_select").val();
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
                                    currency_id + `-[` + location + `]`+
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
                    url: "<?php echo base_url('test/currency_efficiency_calculator')?>",
                    type: "POST",
                    data: {
                        currency_id: currency_id,
                        location: location,
                        request_id: request_id,
                    },
                    success: function (data) {
                        var data = JSON.parse(data);
                        console.log(data);

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

                        console.log("Updating content")
                        
                        var table = $("<table class='table table-striped table-bordered table-hover'>");
                        var headers = $('<thead>');
                        headers.append($('<tr>'))
                        headers.append($('<th>ID</th>'));
                        headers.append($('<th>Name</th>'));
                        headers.append($('<th>Min Price</th>'));
                        headers.append($('<th>Regular Sale Velocity</th>'));
                        headers.append($('<th>FFMT Score</th>'));
                        headers.append($('</tr>'));
                        headers.append($('</thead>'));


                        table.append(headers);
                        table.append($("<tbody>"));

                        $.each(data.data, function(index, item){
                            console.log(item);
                            var row = $("<tr></tr>");
                            row.append($("<td>"+item.id+"</td>"));
                            row.append($("<td>"+item.name+"</td>"));
                            row.append($("<td>"+item.minPrice+"</td>"));
                            row.append($("<td>"+item.regularSaleVelocity+"</td>"));
                            row.append($("<td>"+item.mtvirux_score+"</td>"));
                            table.append(row);
                        });

                        table.append($("</tbody>"));

                        table.append("</table>");

                        console.log($("#accordion-body-"+data.request_id))

                        $("#accordion-body-"+data.request_id).html(table);

                    },
                    error: function (data) {
                        $("div.accordion-item[request_id='"+data.request_id+"']").remove();
                        $("#PLACEHOLDER_ID").remove();

                    }
                });
            }); 
        </script>