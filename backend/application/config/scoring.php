<?php
defined('BASEPATH') OR exit('No direct script access allowed');


$craft_complexity_weight = 10;

//Crafting Complexity Weight on score eval (0 - 100), 0 = no weight, 100 = full weight
$config['craft_complexity_weight'] = max(0, min(100, $craft_complexity_weight));