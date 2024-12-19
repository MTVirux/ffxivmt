<?php
defined('BASEPATH') OR exit('No direct script access allowed');


$config['navbar_structure'] = 
[

    "Gilflux" => array(
                        "name" => "Gilflux",
                        "link" => 'gilflux/index',
                        "description" => "Show's the top items that moved the most gil for the selected World, DC or Region."
                    ),
    "Item Product Profit Solver" => array(
                                            "name" => "Item Product Profit Solver",
                                            "link" => 'tools/item_product_profit_calculator',
                                            "description" => "The most profitable craft for a certain material."
                                        ),
    "Currency Efficiency Calculator" =>  array(
                                                "name" => "Currency Efficiency Calculator",
                                                "link" => 'tools/currency_efficiency_calculator',
                                                "description" => "The most profitable way to spend your currency."
                                            ),

];