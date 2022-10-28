CREATE VIEW Spriggan_Daily AS SELECT entry_id, item_id, name, craft, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Spriggan' ORDER BY `item_scores`.`1d` DESC;
CREATE VIEW Spriggan_Craft_Daily AS SELECT entry_id, item_id, name, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Spriggan' AND `craft` = 1 ORDER BY `item_scores`.`1d` DESC;


CREATE VIEW Cerberus_Daily AS SELECT entry_id, item_id, name, craft, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Cerberus' ORDER BY `item_scores`.`1d` DESC;
CREATE VIEW Cerberus_Craft_Daily AS SELECT entry_id, item_id, name, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Cerberus' AND `craft` = 1 ORDER BY `item_scores`.`1d` DESC;


CREATE VIEW Louisoix_Daily AS SELECT entry_id, item_id, name, craft, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Loui' ORDER BY `item_scores`.`1d` DESC;
CREATE VIEW Louisoix_Craft_Daily AS SELECT entry_id, item_id, name, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Loui' AND `craft` = 1 ORDER BY `item_scores`.`1d` DESC;


CREATE VIEW Moogle_Daily AS SELECT entry_id, item_id, name, craft, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Moogle' ORDER BY `item_scores`.`1d` DESC;
CREATE VIEW Moogle_Craft_Daily AS SELECT entry_id, item_id, name, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Moogle' AND `craft` = 1 ORDER BY `item_scores`.`1d` DESC;


CREATE VIEW Omega_Daily AS SELECT entry_id, item_id, name, craft, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Omega' ORDER BY `item_scores`.`1d` DESC;
CREATE VIEW Omega_Craft_Daily AS SELECT entry_id, item_id, name, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Omega' AND `craft` = 1 ORDER BY `item_scores`.`1d` DESC;


CREATE VIEW Phantom_Daily AS SELECT entry_id, item_id, name, craft, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Phantom' ORDER BY `item_scores`.`1d` DESC;
CREATE VIEW Phantom_Craft_Daily AS SELECT entry_id, item_id, name, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Phantom' AND `craft` = 1 ORDER BY `item_scores`.`1d` DESC;


CREATE VIEW Ragnarok_Daily AS SELECT entry_id, item_id, name, craft, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Ragnarok' ORDER BY `item_scores`.`1d` DESC;
CREATE VIEW Ragnarok_Craft_Daily AS SELECT entry_id, item_id, name, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Ragnarok' AND `craft` = 1 ORDER BY `item_scores`.`1d` DESC;


CREATE VIEW Sagittarius_Daily AS SELECT entry_id, item_id, name, craft, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Sagittarius' ORDER BY `item_scores`.`1d` DESC;
CREATE VIEW Sagittarius_Craft_Daily AS SELECT entry_id, item_id, name, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Sagittarius' AND `craft` = 1 ORDER BY `item_scores`.`1d` DESC;


CREATE VIEW Alpha_Daily AS SELECT entry_id, item_id, name, craft, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Alpha' ORDER BY `item_scores`.`1d` DESC;
CREATE VIEW Alpha_Craft_Daily AS SELECT entry_id, item_id, name, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Alpha' AND `craft` = 1 ORDER BY `item_scores`.`1d` DESC;


CREATE VIEW Lich_Daily AS SELECT entry_id, item_id, name, craft, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Lich' ORDER BY `item_scores`.`1d` DESC;
CREATE VIEW Lich_Craft_Daily AS SELECT entry_id, item_id, name, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Lich' AND `craft` = 1 ORDER BY `item_scores`.`1d` DESC;


CREATE VIEW Odin_Daily AS SELECT entry_id, item_id, name, craft, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Odin' ORDER BY `item_scores`.`1d` DESC;
CREATE VIEW Odin_Craft_Daily AS SELECT entry_id, item_id, name, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Odin' AND `craft` = 1 ORDER BY `item_scores`.`1d` DESC;


CREATE VIEW Phoenix_Daily AS SELECT entry_id, item_id, name, craft, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Phoenix' ORDER BY `item_scores`.`1d` DESC;
CREATE VIEW Phoenix_Craft_Daily AS SELECT entry_id, item_id, name, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Phoenix' AND `craft` = 1 ORDER BY `item_scores`.`1d` DESC;


CREATE VIEW Shiva_Daily AS SELECT entry_id, item_id, name, craft, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Shiva' ORDER BY `item_scores`.`1d` DESC;
CREATE VIEW Shiva_Craft_Daily AS SELECT entry_id, item_id, name, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Shiva' AND `craft` = 1 ORDER BY `item_scores`.`1d` DESC;


CREATE VIEW Zodiark_Daily AS SELECT entry_id, item_id, name, craft, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Zodiark' ORDER BY `item_scores`.`1d` DESC;
CREATE VIEW Zodiark_Craft_Daily AS SELECT entry_id, item_id, name, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Zodiark' AND `craft` = 1 ORDER BY `item_scores`.`1d` DESC;


CREATE VIEW Raiden_Daily AS SELECT entry_id, item_id, name, craft, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Raiden' ORDER BY `item_scores`.`1d` DESC;
CREATE VIEW Raiden_Craft_Daily AS SELECT entry_id, item_id, name, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Raiden' AND `craft` = 1 ORDER BY `item_scores`.`1d` DESC;


CREATE VIEW Twintania_Daily AS SELECT entry_id, item_id, name, craft, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Twintania' ORDER BY `item_scores`.`1d` DESC;
CREATE VIEW Twintania_Craft_Daily AS SELECT entry_id, item_id, name, 1d, latest_sale, 5min, 15min, 30min, 1h, 2h, 6h, 12h, updated_at FROM `item_scores` WHERE `world` LIKE 'Twintania' AND `craft` = 1 ORDER BY `item_scores`.`1d` DESC;

CREATE VIEW Chaos_Daily AS  SELECT item_id, name, craft, sum(1d) as 1d, max(latest_sale) as latest_sale, sum(5min) as 5min, sum(15min) as 15min, sum(30min) as 30min, sum(1h) as 1h, sum(2h) as 2h, sum(6h) as 6h, sum(12h) as 12h, updated_at FROM `item_scores` WHERE `datacenter` LIKE 'Chaos' GROUP BY `item_id` ORDER BY `item_scores`.`1d` DESC ;
CREATE VIEW Chaos_Daily_Craft AS  SELECT item_id, name, sum(1d) as 1d, max(latest_sale) as latest_sale, sum(5min) as 5min, sum(15min) as 15min, sum(30min) as 30min, sum(1h) as 1h, sum(2h) as 2h, sum(6h) as 6h, sum(12h) as 12h, updated_at FROM `item_scores` WHERE `datacenter` LIKE 'Chaos' AND `craft` = 1 GROUP BY `item_id` ORDER BY `item_scores`.`1d` DESC;

CREATE VIEW Light_Daily AS  SELECT item_id, name, craft, sum(1d) as 1d, max(latest_sale) as latest_sale, sum(5min) as 5min, sum(15min) as 15min, sum(30min) as 30min, sum(1h) as 1h, sum(2h) as 2h, sum(6h) as 6h, sum(12h) as 12h, updated_at FROM `item_scores` WHERE `datacenter` LIKE 'Light' GROUP BY `item_id` ORDER BY `item_scores`.`1d` DESC ;
CREATE VIEW Light_Daily_Craft AS  SELECT item_id, name, sum(1d) as 1d, max(latest_sale) as latest_sale, sum(5min) as 5min, sum(15min) as 15min, sum(30min) as 30min, sum(1h) as 1h, sum(2h) as 2h, sum(6h) as 6h, sum(12h) as 12h, updated_at FROM `item_scores` WHERE `datacenter` LIKE 'Light' AND `craft` = 1 GROUP BY `item_id` ORDER BY `item_scores`.`1d` DESC;