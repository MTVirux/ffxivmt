#!/bin/bash
ENV=0;

for file in ./scripts/*; do
    if [ -d "$file" ]; 
    then
        echo "Ignoring directory: $file"
    elif [ -f "$file" ];
    then
        if [[ $file == *.bat ]]
        then
            echo "Swapping to bat scripts"
            ENV=1
            break
        elif true;
        then
            echo "Swapping to sh scripts"
            ENV=2
            break
        fi
    fi
done

if [ $ENV = 1 ];
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
elif [ $ENV = 2 ];
then
        for shell_script in ./scripts/*; do
            echo "${shell_script}" 
                if [ -f "$shell_script" ] && [[ $shell_script == *.sh ]];
                then
                        
                        file_no_path=${shell_script##*/}
                        filename=${file_no_path%%.*}
                        
                        mv ./scripts/"${filename}".sh .
                        mv ./"${filename}".bat ./scripts/
                        
                        echo "Swapping ./scripts/${filename}.sh with ./${filename}.bat"
                fi
                echo "Swapped to Linux .sh scripts"
        done
else
        echo "Could not determine environments. Please check you ./scripts/ folder"
fi