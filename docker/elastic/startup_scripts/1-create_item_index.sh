#! /bin/bash

# Create the item index

curl -XPUT 'http://localhost:9200/items'

curl -XPUT 'http://localhost:9200/items/_mapping' -H 'Content-Type: application/json' -d '
{
    "properties" : {
        "name" : { "type" : "text" },
        "id" : { "type" : "integer" }
    }
}'
