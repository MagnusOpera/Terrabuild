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
    container: mcr.microsoft.com/dotnet/sdk:7.0
    parameters:
      configuration: $(configuration)
  terraform:
    parameters:
      workspace: $(workspace)
