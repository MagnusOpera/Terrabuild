storage: azureblob
sourcecontrol: github

targets:
  build: [^build]
  dist: [build]
  docker: [dist]
  push: [docker]
  deploy: [push]

environments:
  debug:
    configuration: Debug
    secret_message: tagada
    workspace: default

  release:
    configuration: Release
    workspace: default

extensions:
  dotnet:
    container:
    parameters:
      configuration: $(configuration)
  terraform:
    container:
    parameters:
      workspace: $(workspace)
