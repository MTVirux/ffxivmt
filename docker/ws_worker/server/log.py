import datetime
import traceback
import inspect
import config
import json
import time


#########################################
#                ERROR                  #
#########################################

def error(message = "None", print_stack = True):

    if(config.PRINT_TO_LOG["ERROR"] == False):
        return

    caller_filename = str(inspect.stack()[1][0].f_code.co_filename).split("/")[-1].split(".")[0]
    log_filename = config.LOGS_DIR+"error/" + caller_filename + "_" + str(datetime.datetime.now().strftime("%Y-%m-%d")) + ".log"
    message = str(message)

    if(config.PRINT_TO_SCREEN['ERROR'] == True):
        print("[ERROR]["+caller_filename+"][" + str(datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")) + "] - " + str(message))


    error_file = open(log_filename, "a")
    error_file.write("["+datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")+"] - " + caller_filename)
    error_file.write('\n')
    if(print_stack == True):
        traceback.print_stack(file=error_file)
    error_file.write('\n')
    if message is not None:
        error_file.write(f"\tERROR MESSAGE: " + message)
    error_file.write('\n\n\n')

    if(config.LIMIT_LOGS['ERROR'] != 0):
        limit_log(log_filename , "ERROR")

    return

#########################################
#                ACTION                 #
#########################################


def action(message = "None", custom_log_channel = ""):

    if(config.PRINT_TO_LOG["ACTION"] == False):
        return

    if(custom_log_channel == ""):
        caller_filename = inspect.stack()[1][0].f_code.co_filename.split("/")[-1].split(".")[0]
    else:
        caller_filename = custom_log_channel

    log_filename = config.LOGS_DIR+"action/" + caller_filename + "_" + str(datetime.datetime.now().strftime("%Y-%m-%d")) + ".log"
    message = json.loads(message)
    message['timestamp'] = int(time.time())*1000
    message['caller_filename'] = caller_filename
    message = json.dumps(message)

    if(config.PRINT_TO_SCREEN['ACTION'] == True):
        print("[ACTION]" + message)


    log_file = open(log_filename, "a")
    if message is not None:
        log_file.write(message)
        log_file.write('\n')
    
    if(config.LIMIT_LOGS['ACTION'] != 0):
        limit_log(log_filename, "ACTION")

    return


#########################################
#                DEBUG                  #
#########################################

def debug(message = "None"):

    if(config.PRINT_TO_LOG["DEBUG"] == False):
        return

    log_file_name = config.LOGS_DIR+"debug/" + "debug" + "_" + str(datetime.datetime.now().strftime("%Y-%m-%d")) + ".log"
    caller_filename = inspect.stack()[1][0].f_code.co_filename.split("/")[-1].split(".")[0]

    if message is not None:
        log_file = open(log_file_name, "a");
        log_file.write("["+caller_filename+"][" + str(datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")) + "] - " + str(message))
        log_file.write('\n')

        if(config.LIMIT_LOGS['DEBUG'] != 0):
            limit_log(log_file, "DEBUG")

        if(config.PRINT_TO_SCREEN['DEBUG'] == True):
            print("[DEBUG][" + str(datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")) + "] - " + str(message))    

        return


#########################################
#             LIMIT LOG                 #
#########################################
def limit_log(filename, type):
    # If config.LOG_LIMIT is set to 0, then we don't want to limit the log
    if(config.LOG_LIMIT == 0):
        return

    #Don't limit if printing to log is on
    if(config.PRINT_TO_LOG[type] == True):
        return
    
    # If the log is less than the limit, then we don't need to do anything
    if(len(open(filename).readlines()) < config.LOG_LIMIT[type]):
        return

    # If the log is greater than the limit, then we need to remove the oldest lines
    with open(filename, "r") as f:
        lines = f.readlines()
    with open(filename, "w") as f:
        f.writelines(lines[-config.LOG_LIMIT[type]:])
    return


def panic(cql):
    log_file_name = config.LOGS_DIR+"panic/" + "panic" + "_" + str(datetime.datetime.now().strftime("%Y-%m-%d")) + ".log"

    if cql is not None:
        log_file = open(log_file_name, "a");
        log_file.write(cql+";")
        log_file.write('\n')

        return    
