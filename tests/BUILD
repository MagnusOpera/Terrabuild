dependencies:
  - projects/project1
  - projects/project2
  - projects/project3

targets:
  build:
    depends-on: [^build]

  publish:
    depends-on: [build]

  container:
    depends-on: [publish]

variables:
  configuration: Release
