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

    return

#########################################
#                ACTION                 #
#########################################


def action(message = "None"):
    caller_filename = inspect.stack()[1][0].f_code.co_filename.split("/")[-1].split(".")[0]
    log_file_name = config.LOGS_DIR+"action/" + caller_filename + "_" + str(datetime.datetime.now().strftime("%Y-%m-%d")) + ".log"

    if message is not None:
        log_file = open(log_file_name, "a");
        log_file.write("["+caller_filename+"][" + str(datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")) + "] - " + str(message))
        log_file.write('\n')

        return


#########################################
#                DEBUG                  #
#########################################

def debug(message = "None"):
    log_file_name = config.LOGS_DIR+"debug/" + "debug" + "_" + str(datetime.datetime.now().strftime("%Y-%m-%d")) + ".log"
    caller_filename = inspect.stack()[1][0].f_code.co_filename.split("/")[-1].split(".")[0]

    if message is not None:
        log_file = open(log_file_name, "a");
        log_file.write("["+caller_filename+"][" + str(datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")) + "] - " + str(message))
        log_file.write('\n')

        return


#########################################
#             REQUEST LOG               #
#########################################
def request(message = "None"):
    log_file_name = config.LOGS_DIR+"requests/" + "requests" + "_" + str(datetime.datetime.now().strftime("%Y-%m-%d")) + ".log"
    caller_filename = inspect.stack()[1][0].f_code.co_filename.split("/")[-1].split(".")[0]

    if message is not None:
        log_file = open(log_file_name, "a");
        log_file.write("["+caller_filename+"][" + str(datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")) + "] - " + str(message))
        log_file.write('\n')

        return


#########################################
#               PANIC LOG               #
#########################################
def panic(cql):
    log_file_name = config.LOGS_DIR+"panic/" + "panic" + "_" + str(datetime.datetime.now().strftime("%Y-%m-%d")) + ".log"

    if cql is not None:
        log_file = open(log_file_name, "a");
        log_file.write(cql+";")
        log_file.write('\n')

        return    
