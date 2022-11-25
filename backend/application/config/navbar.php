<?php
defined('BASEPATH') OR exit('No direct script access allowed');


$config['navbar_structure'] = 
[
    "Home" => "home",
    "Gil Flux" => [
        "All Items" => [

            "Chaos" => [
                "Chaos DC" => 'gilflux/index/Chaos',
                "Cerberus" => 'gilflux/index/Cerberus',
                "Louisoix" => 'gilflux/index/Louisoix',
                "Moogle" => 'gilflux/index/Moogle',
                "Omega" => 'gilflux/index/Omega',
                "Phantom" => 'gilflux/index/Phantom',
                "Ragnarok" => 'gilflux/index/Ragnarok',
                "Sagittarius" => 'gilflux/index/Sagittarius',
                "Spriggan" => 'gilflux/index/Spriggan',
            ],
            "Light" => [
                "Light DC" => 'gilflux/index/Light',
                "Alpha" => 'gilflux/index/Alpha',
                "Lich" => 'gilflux/index/Lich',
                "Odin" => 'gilflux/index/Odin',
                "Phoenix" => 'gilflux/index/Phoenix',
                "Raiden" => 'gilflux/index/Raiden',
                "Shiva" => 'gilflux/index/Shiva',
                "Twintania" => 'gilflux/index/Twintania',
                "Zodiark" => 'gilflux/index/Zodiark',
            ],

        ],
        "Crafted Only" => [
            "Chaos" => [
                "Chaos DC" => 'gilflux/index/Chaos/craft',
                "Cerberus" => 'gilflux/index/Cerberus/craft',
                "Louisoix" => 'gilflux/index/Louisoix/craft',
                "Moogle" => 'gilflux/index/Moogle/craft',
                "Omega" => 'gilflux/index/Omega/craft',
                "Phantom" => 'gilflux/index/Phantom/craft',
                "Ragnarok" => 'gilflux/index/Ragnarok/craft',
                "Sagittarius" => 'gilflux/index/Sagittarius/craft',
                "Spriggan" => 'gilflux/index/Spriggan/craft',
            ],
            "Light" => [
                "Light DC" => 'gilflux/index/Light/craft',
                "Alpha" => 'gilflux/index/Alpha/craft',
                "Lich" => 'gilflux/index/Lich/craft',
                "Odin" => 'gilflux/index/Odin/craft',
                "Phoenix" => 'gilflux/index/Phoenix/craft',
                "Raiden" => 'gilflux/index/Raiden/craft',
                "Shiva" => 'gilflux/index/Shiva/craft',
                "Twintania" => 'gilflux/index/Twintania/craft',
                "Zodiark" => 'gilflux/index/Zodiark/craft',
            ],

        ],
    ],
    "Other Tools" =>[
        "Item Product Profit Solver" => 'tools/item_product_profit_calculator',
    ],
];