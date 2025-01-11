##### INFRA VARS #####

variable "location" {
  description = "The location of the instances"
  type        = string
  default     = "hel1"
}

##### PROVIDER API VARS #####

variable "hcloud_token" {
  type = string
  sensitive = true
}

##### HETZNER SSH VARS #####
variable "hcloud_ssh_key_name"{
  type = string
}

##### FIREWALL VARS #####
variable "local_ip" {
  type = string
  description = "Local IP, to allow connection to servers"
}

##### NETWORK VARS #####
variable "network_zone" {
  type = string
  description = "Name of the Network Zone"
  default = "eu-central"
}


##### PROJECT VARS #####

variable "scylla_instances" {
  type = number
  description = "Number of Scylla Instances to be created"
  default = 1
}

