dependencies:
- projects/project1
- projects/project2
- projects/project3/src
- projects/project3/deploy

targets:
  build: [^build]
  publish: [build]
  docker: [publish, ^publish]
  push: [docker]

variables:
  configuration: Release
