resource "null_resource" "test_res" {
    provisioner "local-exec" {
      command = "echo 'workspace ${terraform.workspace}\ndotnet_app_version = ${var.dotnet_app_version}' >> deploy.log"
  }
}
