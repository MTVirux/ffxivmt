#!/bin/bash

# Define the list of IPs to connect to
IP_LIST=("10.0.0.3" "10.0.0.4")
#IP_LIST=("10.0.0.3")

#Get the first element on the list
SEED_IP=${IP_LIST[0]}



for IP in "${IP_LIST[@]}"
do

    #Fix SSH Keys for rebuilt servers
    ssh-keygen -R $IP
    ssh-keyscan -H $IP >> /root/.ssh/known_hosts

    #Get last number of the current IP
    IP_LAST_NUMBER=$(echo $IP | cut -d'.' -f 4)

    ssh root@"$IP" 'sudo sudo gpg --homedir /tmp --no-default-keyring --keyring /etc/apt/keyrings/scylladb.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys d0a112e067426ab2'
    ssh root@"$IP" 'sudo wget -O /etc/apt/sources.list.d/scylla.list http://downloads.scylladb.com/deb/debian/scylla-5.2.list'
    ssh root@"$IP" 'sudo apt-get update && sudo apt-get install -y scylla'

    #Setup the seed node
    echo "Reforging the seed node @ $IP"
    ssh root@"$IP" "sudo sed -i 's/seeds: \"127.0.0.1\"/seeds: \"$SEED_IP\"/g' /etc/scylla/scylla.yaml"

    #setup the listen and rpc address
    echo "Reforging the listen and rpc address @ $IP"
    ssh root@"$IP" "sudo sed -i 's/listen_address: localhost/listen_address: $IP/g' /etc/scylla/scylla.yaml"
    ssh root@"$IP" "sudo sed -i 's/rpc_address: localhost/rpc_address: $IP/g' /etc/scylla/scylla.yaml"
    
    
    #Write cassandra-rackdc.properties file
    ssh root@"$IP" "sudo echo "rack=rack$IP_LAST_NUMBER" >> /etc/scylla/cassandra-rackdc.properties"
    ssh root@"$IP" "sudo echo "dc=ffmt_dc" >> /etc/scylla/cassandra-rackdc.properties"

    #Run the setup
    ssh root@"$IP" 'scylla_setup --no-raid-setup --online-discard 1 --nic ens10 --io-setup 1 --no-fstrim-setup --no-rsyslog-setup'
    
    #Start the scylla server
    echo "[$(date)] Starting Scylla Server @ $IP"
    ssh root@"$IP" 'sudo systemctl start scylla-server'
    
    #Wait until 'nodetool status' returns UN (Up and Normal), printing that you're waiting on that IP's server to be up in the loop
    #Make sure to include current timestamp in the output and that it overwrites itself on the same line
    
    while true
    do
        STATUS=$(ssh root@"$IP" 'sudo nodetool status' | grep "UN" | grep $IP | awk '{print $1}')
        if [ "$STATUS" == "UN" ]; then
            echo -ne "\r[$(date)] Scylla Server @ $IP is up!"
            break
        else
            echo -ne "\r[$(date)] Waiting on Scylla Server @ $IP to be up..."
            sleep 1
        fi
    done



    #IF $IP == $SEED_IP, then run the scripts on the seed node
    if [ "$IP" == "$SEED_IP" ]; then

        #Wait until CQLSH is able to connect to the server, printing that you're waiting on that IP's server to be up in the loop
        #Make sure to include current timestamp in the output and that it overwrites itself on the same line
        echo "Exporting the the value of $SEED_IP to the CQLSH_HOST env variable on the seed node @ $SEED_IP"
        ssh root@"$SEED_IP" 'echo CQLSH_HOST="'$SEED_IP'" >> /etc/environment && sleep 1 && source /etc/environment'
        while true
        do
            STATUS=$(ssh root@"$IP" 'cqlsh -e "SELECT now() FROM system.local;"' | grep "1 rows")
            echo $STATUS
            if [ "$STATUS" == "(1 rows)" ]; then
                echo "CONNECTED SUCCESSFULLY";
                echo -ne "\r[$(date)] CQLSH @ $IP is up!"
                break
            else
                echo $STATUS
                echo -ne "\r[$(date)] Waiting on CQLSH @ $IP to be up..."
                sleep 1
            fi
        done


        echo "Passing project scripts to seed node @ $SEED_IP"
        scp -r ./docker/scylla/startup_scripts root@"$SEED_IP":/tmp
        #Export the the value of $SEED_IP to the CQLSH_HOST env variable on the seed node
        echo "Exporting the the value of $SEED_IP to the CQLSH_HOST env variable on the seed node @ $SEED_IP"
        ssh root@"$SEED_IP" 'echo CQLSH_HOST="'$SEED_IP'" >> /etc/environment && sleep 1 && source /etc/environment'

        #Run the scripts on the seed node
        #Enabling x permission on the scripts
        echo "Enabling x permission on the scripts @ $SEED_IP"
        ssh root@"$SEED_IP" 'cd /tmp/startup_scripts && chmod +x *'
        #Running the scripts
        echo "Running the scripts on the seed node @ $SEED_IP"
        ssh root@"$SEED_IP" 'cd /tmp/startup_scripts && for file in *; do if [ -f "$file" ] && [ -x "$file" ]; then ./"'"\$file"'" ; fi; done'
    else
        echo $IP
        echo $SEED_IP
    fi


done



