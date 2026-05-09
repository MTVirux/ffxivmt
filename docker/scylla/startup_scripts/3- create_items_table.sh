#!/bin/bash

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.items (
    id int,
    name text,
    description text,
    craftable boolean,
    marketable boolean,
    from_scrips boolean,
    purchased_with text,
    can_be_hq boolean,
    always_collectible boolean,
    stack_size int,
    item_level int,
    icon_image int,
    rarity int,
    filter_group int,
    item_ui_category int,
    equip_slot_category int,
    \"unique\" boolean,
    untradable boolean,
    dyable boolean,
    aetherial_reductible boolean,
    materia_slot_count int,
    item_search_category int,
    disposable boolean,
    advanced_melding boolean,
    PRIMARY KEY (id));"

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.marketable_items (
    bucket  int,
    item_id int,
    PRIMARY KEY ((bucket), item_id));"

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.craftable_items (
    bucket  int,
    item_id int,
    PRIMARY KEY ((bucket), item_id));"

cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.scrip_items (
    bucket  int,
    item_id int,
    PRIMARY KEY ((bucket), item_id));"
