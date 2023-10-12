storage: azureblob

dependencies:
- projects/project1
- projects/project2
- projects/project3

targets:
  build: [^build]
  publish: [build]
  docker: [publish]
  push: [docker]

environments:
  debug:
    configuration: Debug

  release:
    configuration: Release

extensions:
  dotnet:
    configuration: $(configuration)
