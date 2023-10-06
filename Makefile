
build:
	dotnet build


tests: run-build run-build-nc target usage

run-build:
	dotnet run --project Terrabuild -- build --workspace tests

run-build-nc:
	dotnet run --project Terrabuild -- build --workspace tests --nocache

run-push:
	dotnet run --project Terrabuild -- run push --workspace tests

usage:
	dotnet run --project Terrabuild -- --help
	dotnet run --project Terrabuild -- build --help
	dotnet run --project Terrabuild -- run --help
	dotnet run --project Terrabuild -- serve --help
	dotnet run --project Terrabuild -- clear --help
