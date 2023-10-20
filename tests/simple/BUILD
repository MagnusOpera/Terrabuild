storage: azureblob

targets:
  build: [^build]
  publish: [build]
  docker: [publish]
  push: [docker]
  deploy: [build]

environments:
  debug:
    configuration: Debug
    secret_message: tagada

  release:
    configuration: Release

extensions:
  dotnet:
    configuration: $(configuration)
