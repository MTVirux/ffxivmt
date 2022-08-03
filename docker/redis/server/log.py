import datetime
import traceback
import inspect


def error(message, print_stack = True):
    print("ERROR_PRINT: ["+datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")+"]")
    caller_filename = str(inspect.stack()[1][0].f_code.co_filename)
    message = str(message)
    error_file = open("logs/errors.log", "a")


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

def action(message):
    print("ACTION_PRINT: [" + str(datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")) + "]")
    caller_filename = inspect.stack()[1][0].f_code.co_filename.split("/")[-1].split(".")[0]
    log_filename = "logs/" + caller_filename + "_" + str(datetime.datetime.now().strftime("%Y-%m-%d")) + ".log"
    print(log_filename);
    message = str(message)
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

def debug(message):
    print("DEBUG_PRINT: [" + datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S") + "]")
    log_file = open("logs/debug.log", "a")
    if message is not None:
        log_file.write(datetime.datetime.now().strftime("%Y-%m-%d") + " - " + str(message))
    log_file.write('\n')
    return