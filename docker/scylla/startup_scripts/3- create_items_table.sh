#!/bin/bash


cqlsh -e "CREATE TABLE IF NOT EXISTS ffmt.items (
id int,
name text,
description text,
craftable boolean,
marketable boolean,
can_Be_HQ boolean,
always_collectible boolean,
stack_size int,
item_Level int,
icon_Image int,
rarity int,
filter_Group int,
item_UI_Category int,
equip_Slot_Category int,
unique boolean,
untradable boolean,
dyable boolean,
aetherial_Reductible boolean,
materia_Slot_Count int,
item_Search_Category int,
disposable boolean,
advanced_Melding boolean,
PRIMARY KEY (id));"


cqlsh -e "CREATE INDEX IF NOT EXISTS item_names ON ffmt.items (name)"
cqlsh -e "CREATE INDEX IF NOT EXISTS is_crafted ON ffmt.items (craftable)"
cqlsh -e "CREATE INDEX IF NOT EXISTS marketable ON ffmt.items (marketable)"
