import pprint

def parse_redis_key(section):
    total_keys = 0;
    final_info = {}

    #Count total keys
    for i in section:
        total_keys += int(section[i])
    
    keys_to_log = [ "db0", 
                    "db1", 
                    "db2",
                    #"db3",
                    ];
    keys_to_log_human = {"db0": "INDEX",
                         "db1": "SALES",
                         "db2": "TIMESERIES",
                         #"db3": "LISTINGS",
                         };


    for i in keys_to_log:
        #If i is in keys_to_log, then log it
        final_info[keys_to_log_human[i]] = section[i];

    final_info["Total Keys"] = total_keys;

    return final_info
    exit(0)

def parse_redis_memory(section):
    final_info = {}
    keys_to_log = ["used_memory_human", "used_memory_peak_human"];
    keys_to_log_human = {"used_memory_human": "Used Memory", 
                         "used_memory_peak_human": "Peak Memory"};

    for i in keys_to_log:
        #If i is in keys_to_log, then log it
        final_info[keys_to_log_human[i]] = section[i];

    return final_info

def parse_redis_cpu(section):
    final_info = {}
    keys_to_log = [];
    keys_to_log_human = {};

    for i in keys_to_log:
        #If i is in keys_to_log, then log it
        final_info[keys_to_log_human[i]] = section[i];

    return final_info

def parse_redis_stats(section):
    final_info = {}
    keys_to_log = ["instantaneous_ops_per_sec", "instantaneous_output_kbps", "instantaneous_input_kbps", "total_net_input_bytes", "total_net_output_bytes"];
    keys_to_log_human = {"instantaneous_ops_per_sec": "Ops/s",
                            "instantaneous_output_kbps": "Output KB/s",
                            "instantaneous_input_kbps": "Input KB/s",
                            "total_net_input_bytes": "Total Input Bytes",
                            "total_net_output_bytes": "Total Output Bytes"};

    for i in keys_to_log:
        #If i is in keys_to_log, then log it
        final_info[keys_to_log_human[i]] = section[i];

    return final_info


def parse_redis_persistence(section):
    final_info = {}
    keys_to_log = ["rdb_last_save_time"]
    keys_to_log_human = {"rdb_last_save_time": "Last Save Time"}

    for i in keys_to_log:
        #If i is in keys_to_log, then log it
        final_info[keys_to_log_human[i]] = section[i];

    return final_info

def parse_redis_replication(section):
    final_info = {}
    keys_to_log = [];
    keys_to_log_human = {};

    for i in keys_to_log:
        #If i is in keys_to_log, then log it
        final_info[keys_to_log_human[i]] = section[i];

    return final_info
    
def parse_redis_server(section):
    final_info = {}
    keys_to_log = [];
    keys_to_log_human = {};

    for i in keys_to_log:
        #If i is in keys_to_log, then log it
        final_info[keys_to_log_human[i]] = section[i];

    return final_info