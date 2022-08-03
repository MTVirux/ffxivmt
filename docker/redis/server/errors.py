import datetime
import traceback


def log(message, print_stack = True):
    print("ERROR_LOG: "("["+datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")+"]"))
    error_file = open("errors.log", "a")
    error_file.write("###########################################################################")
    error_file.write('\n')
    error_file.write("["+datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")+"]")
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

def print(message):
    print("ERROR_PRINT: "("["+datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")+"]"))
    error_file = open("errors.log", "a")
    error_file.write(str(message) + '\n')
    return