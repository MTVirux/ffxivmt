import database
import log
import pprint

def parse(new_entry, url, external_item_name_dict):

    parsed_sales = 0
    total_sales_entries = 0

    for item_id in new_entry["items"]:
        for sale in new_entry["items"][item_id]["entries"]:
            total_sales_entries += 1
            sale['total'] = int(sale['pricePerUnit']) * int(sale['quantity'])
            sale['itemID'] = int(item_id)
            sale['itemName'] = external_item_name_dict[int(item_id)]
            formatted_query = ""
            params = ()
            escaped_params = ()

            try:
                query = """INSERT INTO sales (buyer_name, hq, on_mannequin, unit_price, quantity, sale_time, world_id, item_id, world_name, item_name, total) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)"""
                params = (
                    sale['buyerName'],
                    sale['hq'],
                    sale['onMannequin'],
                    int(sale['pricePerUnit']),
                    sale['quantity'],
                    int(sale['timestamp'])*1000,
                    sale['worldID'],
                    sale['itemID'],
                    sale['worldName'],
                    sale['itemName'],
                    int(sale['total']),
                )

                escaped_params = (
                    "'" + sale['buyerName'].replace("'", "''") + "'",
                    sale['hq'],
                    sale['onMannequin'],
                    int(sale['pricePerUnit']),
                    sale['quantity'],
                    int(sale['timestamp'])*1000,
                    sale['worldID'],
                    sale['itemID'],
                    "'" + sale['worldName'] + "'",
                    "'" + sale['buyerName'].replace("'", "''") + "'",
                    int(sale['total']),
                )

                formatted_query = query % escaped_params
                
            except Exception as e:
                log.error(e)
            try:
                result_set_object = database.SCYLLA_DB.execute(query, params)
                pprint.pprint(result_set_object)
                parsed_sales += 1
                
            except Exception as e:
                log.panic(formatted_query)
                log.error(e)
    
    #If no sales were added, log error
    if(parsed_sales == total_sales_entries):
        log.action("[FULL PARSE] " + str(parsed_sales)  + " --- " +  str(url))
    elif(parsed_sales != 0):
        log.action("[PARTIAL PARSE] " + str(parsed_sales) + " --- " + str(url))
        log.error("[PARTIAL PARSE] " + str(parsed_sales) + " --- " + str(url))
    else:
        log.error("[FAILED TO PARSE] --- " + url)