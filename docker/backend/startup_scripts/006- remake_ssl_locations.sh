#!/bin/bash

# Set the directory path
directory="/etc/apache2/sites-available"

echo "HOST_SSL_CERT = " ${HOST_SSL_CERT}
echo "HOST_SSL_PRIVATE_KEY = ${HOST_SSL_PRIVATE_KEY}"

# Use a loop to iterate through the files in the directory
for file in "$directory"/*; do
    if [ -f "$file" ] && [[ "$file" == *ssl* ]]; then
        echo "Accessing: $file"
        # Use sed with a different delimiter to avoid conflicts
        while IFS= read -r line; do
            if [[ "$line" != *"#"* ]];then
                if [[ "$line" == *"SSLCertificateFile"* ]]; then
                    echo "Replacing cert in line: $line"
                    sed -i "s|SSLCertificateFile.*|SSLCertificateFile ${HOST_SSL_CERT}|" "$file"
                    echo "Replaced cert in line: $line"
                fi
                if [[ "$line" == *"SSLCertificateKeyFile"* ]]; then
                    echo "Replacing key in line: $line"
                    sed -i "s|SSLCertificateKeyFile.*|SSLCertificateKeyFile ${HOST_SSL_PRIVATE_KEY}|" "$file"
                    echo "Replaced in key file: $file"
                fi
            fi
        done < "$file"
    fi
done

systemctl restart apache2