import datetime
import traceback
import inspect
import config


#########################################
#                ERROR                  #
#########################################

def error(message = "None", print_stack = True):

    caller_filename = str(inspect.stack()[1][0].f_code.co_filename).split("/")[-1].split(".")[0]
    log_filename = "logs/error" + caller_filename + "_" + str(datetime.datetime.now().strftime("%Y-%m-%d")) + ".log"
    message = str(message)

    if(config.PRINT_TO_SCREEN['ERROR'] == True):
        if(print_stack == True):
            print (traceback.print_stack())
        print("[ERROR]["+caller_filename+"][" + str(datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")) + "] - " + str(message))

    if(config.PRINT_TO_LOG['ERROR'] == False):
        return


    error_file = open(log_filename, "a")

    error_file.write("###########################################################################")
    error_file.write('\n')
    error_file.write("["+datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")+"] - " + caller_filename)
    error_file.write('\n')
    error_file.write("###########################################################################")
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
    log_filename = "logs/action" + caller_filename + "_" + str(datetime.datetime.now().strftime("%Y-%m-%d")) + ".log"
    message = str(message)

    if(config.PRINT_TO_SCREEN['ACTION'] == True):
        print("[ACTION]["+caller_filename+"][" + str(datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")) + "] - " + str(message))

    if(config.PRINT_TO_LOG['ACTION'] == False):
        return


    log_file = open(log_filename, "a")
    log_file.write("["+datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")+"] - " + caller_filename) 
    log_file.write('\n')
    if message is not None:
        log_file.write(message)
    log_file.write('\n')
    log_file.write("###########################################################################")
    log_file.write('\n')
    log_file.write('\n')
    
    return


#########################################
#                DEBUG                  #
#########################################

def debug(message = "None"):
    print("DEBUG_PRINT: [" + datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S") + "]")
    log_file = open("logs/debug.log", "a")
    if message is not None:
        log_file.write(datetime.datetime.now().strftime("%Y-%m-%d") + " - " + str(message))
    log_file.write('\n')
    return