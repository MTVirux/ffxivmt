
##### PUBLIC ACCESS TO MAIN ###
resource "hcloud_firewall_attachment" "pubacc_fwa_ffmt_main" {
    firewall_id = hcloud_firewall.PubAccess.id
    server_ids  = [
        tonumber(hcloud_server.ffmt_main.id), 
    ]

    depends_on = [
        null_resource.wait_for_servers,
        null_resource.wait_for_firewalls
    ]
}

##### PRIVATE ACCESS TO MAIN #####
resource "hcloud_firewall_attachment" "privacc_fwa_ffmt_main" {
    firewall_id = hcloud_firewall.PriAccess.id
    server_ids  = [
        tonumber(hcloud_server.ffmt_main.id), 
    ]

    depends_on = [
        null_resource.wait_for_servers,
        null_resource.wait_for_firewalls
    ]
}

##### LOCAL ACCESS WITHIN SUBNET #####
resource "hcloud_firewall_attachment" "locacc_fwa_ffmt_main" {
    firewall_id = hcloud_firewall.LocAccess.id
    server_ids  = concat(
        [tonumber(hcloud_server.ffmt_main.id)],
        [for id in hcloud_server.ffmt_scylla_instances[*].id : tonumber(id)]
    )


    depends_on = [
        null_resource.wait_for_servers,
        null_resource.wait_for_firewalls
    ]
}


##### LOCAL MANAGEMENT FROM MAIN #####
resource "hcloud_firewall_attachment" "locman_fwa_ffmt_main" {
    firewall_id = hcloud_firewall.LocManagement.id
    server_ids  = concat(
        [for id in hcloud_server.ffmt_scylla_instances[*].id : tonumber(id)]
    )

    depends_on = [
        null_resource.wait_for_servers,
        null_resource.wait_for_firewalls
    ]
}

resource "null_resource" "wait_for_firewall_attachements"{
    depends_on = [
        hcloud_firewall_attachment.pubacc_fwa_ffmt_main,
        hcloud_firewall_attachment.privacc_fwa_ffmt_main,
        hcloud_firewall_attachment.locacc_fwa_ffmt_main,
        hcloud_firewall_attachment.locman_fwa_ffmt_main,
        ]
}