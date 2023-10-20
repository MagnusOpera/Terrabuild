
build:
	dotnet build


tests: run-build run-build-nc target usage

run-build-multirefs:
	dotnet run --project Terrabuild -- build --workspace tests/multirefs

run-build-circular:
	dotnet run --project Terrabuild -- build --workspace tests/circular

run-build:
	dotnet run --project Terrabuild -- build --workspace tests/simple --environment debug --debug

run-build-app:
	dotnet run --project Terrabu ild -- build --workspace tests/simple --environment debug --label dotnet --debug

run-docker:
	dotnet run --project Terrabuild -- run docker --workspace tests/simple --environment debug

run-build-az:
	dotnet run --project Terrabuild -- build --workspace tests/simple --shared --env release

run-build-nc:
	dotnet run --project Terrabuild -- build --workspace tests/simple --nocache --env debug

run-push:
	dotnet run --project Terrabuild -- run push --workspace tests/simple

usage:
	dotnet run --project Terrabuild -- --help
	dotnet run --project Terrabuild -- build --help
	dotnet run --project Terrabuild -- run --help
	dotnet run --project Terrabuild -- serve --help
	dotnet run --project Terrabuild -- clear --help

clear-cache:
	dotnet run --project Terrabuild -- clear --buildcache
