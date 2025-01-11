resource "hcloud_firewall" "PriAccess" {
  name = "Private-Access"
  rule {
    direction = "in"
    protocol  = "tcp"
    port      = "22"
    source_ips = ["${var.local_ip}"]
    description = "Allow Private SSH Access"
  }
}

resource "hcloud_firewall" "PubAccess" {
  name = "Public-Access"

  rule {
    direction = "in"
    protocol  = "tcp"
    port      = "443"
    source_ips = ["0.0.0.0/0", "::/0"]
    description = "Allow Public HTTPS Access"
  }
}

resource "hcloud_firewall" "LocAccess" {
  name = "Local-Access"

  rule {
    direction = "in"
    protocol  = "tcp"
    port      = "80"
    source_ips = ["10.0.0.0/28"]
    description = "Allow Local HTTP connections"
  }

  rule {
    direction = "in"
    protocol  = "tcp"
    port      = "443"
    source_ips = ["10.0.0.0/28"]
    description = "Allow Local HTTPS connections"
  }

  rule {
    direction = "in"
    protocol  = "tcp"
    port      = "9042"
    source_ips = ["10.0.0.0/28"]
    description = "Allow Local Scylla connections"
  }
}

resource "hcloud_firewall" "LocManagement" {
  name = "Local-Management"

  rule {
    direction = "in"
    protocol  = "tcp"
    port      = "22"
    source_ips = ["10.0.0.0/28"]
    description = "Allow Local SSH connections"
  }

}

resource "null_resource" "wait_for_firewalls"{
    depends_on = [
        hcloud_firewall.PriAccess,
        hcloud_firewall.PubAccess,
        hcloud_firewall.LocAccess,
        hcloud_firewall.LocManagement,
    ]
}