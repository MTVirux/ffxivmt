echo "Updating Item DB from CSV (Log Channel ITEM_DB)..."
curl -X POST localhost/updatedb/ > /dev/null

echo "Updating item sales from universalis (Log Channel UNIVERSALIS_API)..."
curl -X POST localhost/updatedb/update_sales_from_universalis > /dev/null

echo "Transposing sales to timeseries..."
curl -X POST localhost/test/transpose_sales_to_ts > /dev/null

echo "Updating item scores..."
curl -X POST localhost/test/update_item_scores > /dev/null