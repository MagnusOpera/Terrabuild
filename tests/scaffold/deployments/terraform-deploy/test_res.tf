resource "null_resource" "test_res" {
    provisioner "local-exec" {
      command = "echo 'Hello terraform in workspace ${terraform.workspace}!'"
  }
}
