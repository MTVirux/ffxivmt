import datetime

def treat_redis_key_section(redis_key_section):
    redis_key_section = redis_key_section.decode("utf-8")
    redis_key_section = redis_key_section.split("\r\n");
    redis_key_section.pop(0)
    redis_key_section.pop()
    treated_redis_key_section = {}
    for i in redis_key_section:
        ni = redis_key_section.index(i);
        index = redis_key_section[ni].split(":")[0]; 
        value = redis_key_section[ni].split(":")[1].split(',')[0].split("=")[1];
        treated_redis_key_section[index] = value;
    return treated_redis_key_section;

def treat_redis_memory_section(redis_memory_section):
    redis_memory_section = redis_memory_section.decode("utf-8")
    redis_memory_section = redis_memory_section.split("\r\n");
    redis_memory_section.pop(0)
    redis_memory_section.pop()
    treated_redis_memory_section = {}
    for i in redis_memory_section:
        ni = redis_memory_section.index(i);
        index = redis_memory_section[ni].split(":")[0]; 
        value = redis_memory_section[ni].split(":")[1];
        treated_redis_memory_section[index] = value;
    return treated_redis_memory_section;

def treat_redis_cpu_section(redis_cpu_section):
    redis_cpu_section = redis_cpu_section.decode("utf-8")
    redis_cpu_section = redis_cpu_section.split("\r\n");
    redis_cpu_section.pop(0)
    redis_cpu_section.pop()
    treated_redis_cpu_section = {}
    for i in redis_cpu_section:
        ni = redis_cpu_section.index(i);
        index = redis_cpu_section[ni].split(":")[0]; 
        value = redis_cpu_section[ni].split(":")[1];
        treated_redis_cpu_section[index] = value;
    return treated_redis_cpu_section;

def treat_redis_stats_section(redis_stats_section):
    redis_stats_section = redis_stats_section.decode("utf-8")
    redis_stats_section = redis_stats_section.split("\r\n");
    redis_stats_section.pop(0)
    redis_stats_section.pop()
    treated_redis_stats_section = {}
    for i in redis_stats_section:
        ni = redis_stats_section.index(i);
        index = redis_stats_section[ni].split(":")[0]; 
        value = redis_stats_section[ni].split(":")[1];
        treated_redis_stats_section[index] = value;
    return treated_redis_stats_section;

def treat_redis_persistence_section(redis_persistence_section):
    redis_persistence_section = redis_persistence_section.decode("utf-8")
    redis_persistence_section = redis_persistence_section.split("\r\n");
    redis_persistence_section.pop(0)
    redis_persistence_section.pop()
    treated_redis_persistence_section = {}
    for i in redis_persistence_section:
        ni = redis_persistence_section.index(i);
        index = redis_persistence_section[ni].split(":")[0]; 
        value = redis_persistence_section[ni].split(":")[1];
        treated_redis_persistence_section[index] = value;
    return treated_redis_persistence_section;

def treat_redis_server_section(redis_server_section):
    redis_server_section = redis_server_section.decode("utf-8")
    redis_server_section = redis_server_section.split("\r\n");
    redis_server_section.pop(0)
    redis_server_section.pop()
    treated_redis_server_section = {}
    for i in redis_server_section:
        ni = redis_server_section.index(i);
        index = redis_server_section[ni].split(":")[0]; 
        value = redis_server_section[ni].split(":")[1];
        treated_redis_server_section[index] = value;
    return treated_redis_server_section;


def treat_final_info(final_info):
    final_info["persistence"]["Last Save Time"] = datetime.datetime.fromtimestamp(int(final_info["persistence"]["Last Save Time"])).strftime('%Y-%m-%d %H:%M:%S')
    return final_info;
