#Initiate redis client
import redis
import time
import config
import redis_info_treating
import redis_info_parse
from prettytable import PrettyTable

VIRUX_REDIS_MONITOR = redis.Redis(host=config.REDIS_HOST, port=config.REDIS_PORT, db=config.REDIS_TIMESERIES_DB)
VIRUX_REDIS_MONITOR.client_setname("VIRUX_REDIS_MONITOR")





#run "MEMORY STATS" on redis server
def redis_server_monitor():
    while(True):
        sections = {}
        key_section             = redis_info_treating.treat_redis_key_section(VIRUX_REDIS_MONITOR.execute_command("info keyspace"));
        memory_section          = redis_info_treating.treat_redis_memory_section(VIRUX_REDIS_MONITOR.execute_command("info memory"));
        cpu_section             = redis_info_treating.treat_redis_cpu_section(VIRUX_REDIS_MONITOR.execute_command("info cpu"));
        stats_section           = redis_info_treating.treat_redis_stats_section(VIRUX_REDIS_MONITOR.execute_command("info stats"));
        persistence_section     = redis_info_treating.treat_redis_persistence_section(VIRUX_REDIS_MONITOR.execute_command("info persistence"));
        server_section          = redis_info_treating.treat_redis_server_section(VIRUX_REDIS_MONITOR.execute_command("info server"));

        sections["key_section"] = key_section
        sections["memory_section"] = memory_section
        sections["cpu_section"] = cpu_section
        sections["stats_section"] = stats_section
        sections["persistence_section"] = persistence_section
        sections["server_section"] = server_section

        final_info = {};
    
        for i in sections:
            if i == "key_section":
                final_info["key"]               = redis_info_parse.parse_redis_key(sections[i])
            elif i == "memory_section":
                final_info["memory"]            = redis_info_parse.parse_redis_memory(sections[i])
            elif i == "cpu_section":
                final_info["cpu"]               = redis_info_parse.parse_redis_cpu(sections[i])
            elif i == "stats_section":
                final_info["stats"]             = redis_info_parse.parse_redis_stats(sections[i])
            elif i == "persistence_section":
                final_info["persistence"]       = redis_info_parse.parse_redis_persistence(sections[i])
            elif i == "server_section":
                final_info["server"]            = redis_info_parse.parse_redis_server(sections[i])

        final_info = redis_info_treating.treat_final_info(final_info);
        
        with open(config.LOGS_FILE, 'w') as f:
            open(config.LOGS_FILE)
            for i in final_info:

                if(final_info[i] == {}):
                    continue

                #Create new pretty table
                table = PrettyTable()
                #Add final_info[i].keys as headers
                #print (final_info[i].keys());
                #print (final_info[i].values())
                table.field_names = final_info[i].keys();
                #Add final_info[i].values as rows
                table.add_row(final_info[i].values())
                #Print table
                f.write(table.get_string())
                f.write("\n")

        #Sleep for SLEEP_TIME seconds
        time.sleep(float(config.SLEEP_TIME))
redis_server_monitor();