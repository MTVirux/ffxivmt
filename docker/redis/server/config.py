### WORLD CONFIG

CHAOS_WORLDS = {
39:"Omega",
71:"Moogle",
80:"Cerberus",
83:"Louisoix",
85:"Spriggan",
97:"Ragnarok",
400:"Sagittarius",
401:"Phantom",
}

LIGHT_WORLDS = {
402:"Alpha",
36:"Lich",
66:"Odin",
56:"Phoenix",
403:"Raiden",
67:"Shiva",
33:"Twintania",
42:"Zodiark",
}



WORLDS = {
21:"Ravana",
22:"Bismarck",
23:"Asura",
24:"Belias",
28:"Pandaemonium",
29:"Shinryu",
30:"Unicorn",
31:"Yojimbo",
32:"Zeromus",
33:"Twintania",
34:"Brynhildr",
35:"Famfrit",
36:"Lich",
37:"Mateus",
39:"Omega",
40:"Jenova",
41:"Zalera",
42:"Zodiark",
43:"Alexander",
44:"Anima",
45:"Carbuncle",
46:"Fenrir",
47:"Hades",
48:"Ixion",
49:"Kujata",
50:"Typhon",
51:"Ultima",
52:"Valefor",
53:"Exodus",
54:"Faerie",
55:"Lamia",
56:"Phoenix",
57:"Siren",
58:"Garuda",
59:"Ifrit",
60:"Ramuh",
61:"Titan",
62:"Diabolos",
63:"Gilgamesh",
64:"Leviathan",
65:"Midgardsormr",
66:"Odin",
67:"Shiva",
68:"Atomos",
69:"Bahamut",
70:"Chocobo",
71:"Moogle",
72:"Tonberry",
73:"Adamantoise",
74:"Coeurl",
75:"Malboro",
76:"Tiamat",
77:"Ultros",
78:"Behemoth",
79:"Cactuar",
80:"Cerberus",
81:"Goblin",
82:"Mandragora",
83:"Louisoix",
85:"Spriggan",
86:"Sephirot",
87:"Sophia",
88:"Zurvan",
90:"Aegis",
91:"Balmung",
92:"Durandal",
93:"Excalibur",
94:"Gungnir",
95:"Hyperion",
96:"Masamune",
97:"Ragnarok",
98:"Ridill",
99:"Sargatanas",
400:"Sagittarius",
401:"Phantom",
402:"Alpha",
403:"Raiden",
1167:"红玉海",
1081:"神意之地",
1042:"拉诺西亚",
1044:"幻影群岛",
1060:"萌芽池",
1173:"宇宙和音",
1174:"沃仙曦染",
1175:"晨曦王座",
1172:"白银乡",
1076:"白金幻象",
1171:"神拳痕",
1170:"潮风亭",
1113:"旅人栈桥",
1121:"拂晓之间",
1166:"龙巢神殿",
1176:"梦羽宝境",
1043:"紫水栈桥",
1169:"延夏",
1106:"静语庄园",
1045:"摩杜纳",
1177:"海猫茶屋",
1178:"柔风海湾",
1179:"琥珀原",
1192:"水晶塔",
1183:"银泪湖",
1180:"太阳海岸",
1186:"伊修加德",
1201:"红茶川",
1068:"黄金谷",
1064:"月牙湾",
1187:"雪松原"
}

WORLDS_TO_USE = {
1:CHAOS_WORLDS, 
2:LIGHT_WORLDS,
}

#LOGGING

PRINT_TO_LOG = {
"DEBUG":True,
"ERROR":True,
"ACTION":True,
}

PRINT_TO_SCREEN = {
"DEBUG":True,
"ERROR":True,
"ACTION":True,
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