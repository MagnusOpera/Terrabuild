
build:
	dotnet build

run:
	dotnet run --project Terrabuild -- build --workspace tests

target:
	dotnet run --project Terrabuild -- run build --workspace tests

usage:
	dotnet run --project Terrabuild -- --help
	dotnet run --project Terrabuild -- build --help
	dotnet run --project Terrabuild -- run --help
