storage: azureblob

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

  release:
    configuration: Release

extensions:
  dotnet:
    configuration: $(configuration)
