terraform {
  backend "local" {
    path = "terrabuild.tfstate"
    workspace_dir= "terrabuild.tfstate.d"
  }
}