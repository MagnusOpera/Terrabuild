
build:
	dotnet build

run:
	dotnet run --project Terrabuild -- build --workspace tests

target:
	dotnet run --project Terrabuild -- target build --workspace tests
