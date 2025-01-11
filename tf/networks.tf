
resource "hcloud_network" "ffmt_network" {
  name     = "ffmt_local"
  ip_range = "10.0.0.0/16"
}

resource "hcloud_network_subnet" "ffmt_subnet" {
  ip_range = "10.0.0.0/28"
  network_id = hcloud_network.ffmt_network.id
  network_zone = var.network_zone
  type = "cloud"

  depends_on = [hcloud_network.ffmt_network]
}


resource "null_resource" "wait_for_networks"{
    depends_on = [hcloud_network.ffmt_network]
}

resource "null_resource" "wait_for_subnets" {
    depends_on = [hcloud_network_subnet.ffmt_subnet]
}