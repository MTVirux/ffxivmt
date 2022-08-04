#!/bin/bash
ENV=0;
BAT_IN_SCRIPTS=0;
SH_IN_SCRIPTS=0;

for file in ./scripts/*; do
    if [ -d "$file" ]; 
    then
        echo "Ignoring directory: $file"
    elif [ -f "$file" ];
    then
        if [[ $file == *.bat ]]
        then
                BAT_IN_SCRIPTS=1;
        elif [[ $file == *.sh ]];
        then
                SH_IN_SCRIPTS=1;
        fi
    fi
done

if [ $BAT_IN_SCRIPTS = 1 ] && [ $SH_IN_SCRIPTS = 0 ];
then 
        for bat_script in ./scripts/*; do
                if [ -f "$bat_script" ] && [[ $bat_script == *.bat ]];
                then
                        
                        file_no_path=${bat_script##*/}
                        filename=${file_no_path%%.*}

                        mv ./scripts/"${filename}".bat .
                        mv ./"${filename}".sh ./scripts/

                        echo "Swapping ./scripts/${filename}.bat with ./${filename}.sh"
                        
                fi
        done
        echo "Swapped to Windows .bat scripts"
elif [ $BAT_IN_SCRIPTS = 0 ] && [ $SH_IN_SCRIPTS = 1 ];
then
        for shell_script in ./scripts/*; do
                if [ -f "$shell_script" ] && [[ $shell_script == *.sh ]];
                then
                        
                        file_no_path=${shell_script##*/}
                        filename=${file_no_path%%.*}
                        
                        mv ./scripts/"${filename}".sh .
                        mv ./"${filename}".bat ./scripts/
                        
                        echo "Swapping ./scripts/${filename}.sh with ./${filename}.bat"
                fi
        done
        echo "Swapped to BASH scripts"
elif [ $BAT_IN_SCRIPTS = 1 ] && [ $SH_IN_SCRIPTS = 1 ];
then
        for shell_script in ./scripts/*; do
                if [ -f "$shell_script" ] && [[ $shell_script == *.sh ]];
                then
                        
                        file_no_path=${shell_script##*/}
                        filename=${file_no_path%%.*}
                        
                        mv ./scripts/"${filename}".sh .                        
                fi
        done
        echo "Swapping ./scripts/${filename}.sh with ./${filename}.bat"
        echo "Moved BASH scripts to ."
fi