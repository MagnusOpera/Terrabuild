terraform {
  backend "local" {
    path = "terrabuild.tfstate/default/terraform.state"
    workspace_dir= "terrabuild.tfstate"
  }
}