
build:
	dotnet build


tests: run-build run-build-nc target usage

run-build-multirefs:
	dotnet run --project Terrabuild -- build --workspace tests/multirefs

run-build-circular:
	dotnet run --project Terrabuild -- build --workspace tests/circular

run-build:
	dotnet run --project Terrabuild -- build --workspace tests/simple

run-build-az:
	dotnet run --project Terrabuild -- build --workspace tests/simple --shared

run-build-nc:
	dotnet run --project Terrabuild -- build --workspace tests/simple --nocache

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
