import os

### WORLD CONFIG

WORLDS = {
21:{"name" : "Ravana","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
22:{"name" : "Bismarck","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
23:{"name" : "Asura","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
24:{"name" : "Belias","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
28:{"name" : "Pandaemonium","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
29:{"name" : "Shinryu","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
30:{"name" : "Unicorn","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
31:{"name" : "Yojimbo","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
32:{"name" : "Zeromus","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
33:{"name" : "Twintania","datacenter": "Light", "region": "Europe"},
34:{"name" : "Brynhildr","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
35:{"name" : "Famfrit","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
36:{"name" : "Lich","datacenter": "Light", "region": "Europe"},
37:{"name" : "Mateus","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
39:{"name" : "Omega","datacenter": "Chaos", "region": "Europe"},
40:{"name" : "Jenova","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
41:{"name" : "Zalera","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
42:{"name" : "Zodiark","datacenter": "Light", "region": "Europe"},
43:{"name" : "Alexander","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
44:{"name" : "Anima","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
45:{"name" : "Carbuncle","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
46:{"name" : "Fenrir","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
47:{"name" : "Hades","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
48:{"name" : "Ixion","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
49:{"name" : "Kujata","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
50:{"name" : "Typhon","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
51:{"name" : "Ultima","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
52:{"name" : "Valefor","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
53:{"name" : "Exodus","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
54:{"name" : "Faerie","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
55:{"name" : "Lamia","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
56:{"name" : "Phoenix","datacenter": "Light", "region": "Europe"},
57:{"name" : "Siren","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
58:{"name" : "Garuda","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
59:{"name" : "Ifrit","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
60:{"name" : "Ramuh","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
61:{"name" : "Titan","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
62:{"name" : "Diabolos","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
63:{"name" : "Gilgamesh","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
64:{"name" : "Leviathan","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
65:{"name" : "Midgardsormr","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
66:{"name" : "Odin","datacenter": "Light", "region": "Europe"},
67:{"name" : "Shiva","datacenter": "Light", "region": "Europe"},
68:{"name" : "Atomos","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
69:{"name" : "Bahamut","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
70:{"name" : "Chocobo","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
71:{"name" : "Moogle","datacenter": "Chaos", "region": "Europe"},
72:{"name" : "Tonberry","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
73:{"name" : "Adamantoise","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
74:{"name" : "Coeurl","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
75:{"name" : "Malboro","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
76:{"name" : "Tiamat","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
77:{"name" : "Ultros","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
78:{"name" : "Behemoth","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
79:{"name" : "Cactuar","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
80:{"name" : "Cerberus","datacenter": "Chaos", "region": "Europe"},
81:{"name" : "Goblin","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
82:{"name" : "Mandragora","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
83:{"name" : "Louisoix","datacenter": "Chaos", "region": "Europe"},
85:{"name" : "Spriggan","datacenter": "Chaos", "region": "Europe"},
86:{"name" : "Sephirot","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
87:{"name" : "Sophia","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
88:{"name" : "Zurvan","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
90:{"name" : "Aegis","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
91:{"name" : "Balmung","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
92:{"name" : "Durandal","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
93:{"name" : "Excalibur","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
94:{"name" : "Gungnir","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
95:{"name" : "Hyperion","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
96:{"name" : "Masamune","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
97:{"name" : "Ragnarok","datacenter": "Chaos", "region": "Europe"},
98:{"name" : "Ridill","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
99:{"name" : "Sargatanas","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
400:{"name" : "Sagittarius","datacenter": "Chaos", "region": "Europe"},
401:{"name" : "Phantom","datacenter": "Chaos", "region": "Europe"},
402:{"name" : "Alpha","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
403:{"name" : "Raiden","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1167:{"name" : "红玉海","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1081:{"name" : "神意之地","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1042:{"name" : "拉诺西亚","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1044:{"name" : "幻影群岛","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1060:{"name" : "萌芽池","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1173:{"name" : "宇宙和音","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1174:{"name" : "沃仙曦染","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1175:{"name" : "晨曦王座","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1172:{"name" : "白银乡","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1076:{"name" : "白金幻象","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1171:{"name" : "神拳痕","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1170:{"name" : "潮风亭","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1113:{"name" : "旅人栈桥","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1121:{"name" : "拂晓之间","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1166:{"name" : "龙巢神殿","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1176:{"name" : "梦羽宝境","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1043:{"name" : "紫水栈桥","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1169:{"name" : "延夏","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1106:{"name" : "静语庄园","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1045:{"name" : "摩杜纳","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1177:{"name" : "海猫茶屋","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1178:{"name" : "柔风海湾","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1179:{"name" : "琥珀原","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1192:{"name" : "水晶塔","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1183:{"name" : "银泪湖","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1180:{"name" : "太阳海岸","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1186:{"name" : "伊修加德","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1201:{"name" : "红茶川","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1068:{"name" : "黄金谷","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1064:{"name" : "月牙湾","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"},
1187:{"name" : "雪松原","datacenter": "DC_NOT_SET", "region": "REGION_NOT_SET"}
}

WORLDS_TO_USE = { 
}

DCS_TO_USE = {
}

REGIONS_TO_USE = {
1:"Europe",
}

#LOGGING

LOGS_DIR = "/server/logs/"

PRINT_TO_LOG = { 
"DEBUG":True,
"ERROR":True,
"ACTION":True,
}

PRINT_TO_SCREEN = {
"DEBUG":False,
"ERROR":True,
"ACTION":False,
}

### UNIVERSALLIS CONFIG

UNIVERSALLIS_URL = "wss://universalis.app/api/ws"

### BANNED IDs

BANNED_LISTING_IDS = ["5feceb66ffc86f38d952786c6d696c79c2dbc239dd4e91b46729d73a27fb57e9"]
BANNED_SALE_BUYERS = [""]

### REDIS DB CONFIG

REDIS_HOST = "localhost"

REDIS_PORT = 6379

HASH_FIELD_TTL = 604800 # 1 week TTL

#### REDIS DB INDEXES

REDIS_SALES_DB = os.environ.get('REDIS_SALES_DB')

REDIS_LISTINGS_DB = os.environ.get('REDIS_LISTINGS_DB')

REDIS_RECENT_DB = os.environ.get('REDIS_RECENT_CLEANING_DB')