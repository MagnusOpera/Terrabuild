dependencies:
  - projects/project1
  - projects/project2

targets:
  build:
    depends-on: [^build]

  publish:
    depends-on: [build]

  container:
    depends-on: [publish]

variables:
  configuration: Release
