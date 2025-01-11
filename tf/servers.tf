resource "hcloud_server" "ffmt_main" {
  name        = "ffmt-main"
  server_type = "cx32"
  image       = "ubuntu-24.04"
  location    = var.location
  ssh_keys    = ["${var.hcloud_ssh_key_name}"]
  network {
      network_id = hcloud_network.ffmt_network.id
      ip         = "10.0.0.2"
  }

  depends_on = [
    null_resource.wait_for_networks,
    null_resource.wait_for_subnets
  ]
}

resource "hcloud_server" "ffmt_scylla_instances" {
  count       =  var.scylla_instances
  name        = "ffmt-scylla-${count.index + 1}"
  server_type = "cx32"
  image       = "ubuntu-24.04"
  location    = var.location
  ssh_keys    = ["${var.hcloud_ssh_key_name}"]

  network {
      network_id = hcloud_network.ffmt_network.id
      ip         = "10.0.0.${count.index + 3}"
  }

  depends_on = [
    null_resource.wait_for_networks,
    null_resource.wait_for_subnets
  ]
}


resource "null_resource" "wait_for_servers"{
  depends_on = [
    hcloud_server.ffmt_main,
    hcloud_server.ffmt_scylla_instances
    ]
}
