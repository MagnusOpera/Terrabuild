store:
  type: local
  url: /Users/pierre/.terrabuild-localstore

dependencies:
  - projects/project1
  - projects/project2

targets:
  build:
    depends-on: [^build]

  publish:
    depends-on: [build]

# variables:
#   configuration: Release
