
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
                                <option value="none" selected>Select a DC</option>
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

                var accordion_item = `<div class='accordion-item' request_id='`+request_id+`'id='`+`PLACEHOLDER_ID`+`'>`+
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

                console.log(request_id);


                $.ajax({
                    url: "<?php echo base_url('tools/item_product_profit_calculator')?>",
                    type: "POST",
                    data: {
                        search_term: search_term,
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

                        console.log("Updating content")
                        $("#accordion-body-"+data.request_id).html(data.data);
                    },
                    error: function (data) {
                        $("div.accordion-item[request_id='"+data.request_id+"']").remove();
                        $("#PLACEHOLDER_ID").remove();
                    }
                });
            }); 
        </script>