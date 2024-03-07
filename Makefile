config ?= Debug
version ?= 0.0.0

build:
	dotnet build -c $(config)

dist:
	rm -rf $(PWD)/.out
	dotnet publish -c $(config) -r win-x64 -p:PublishSingleFile=true --self-contained -o $(PWD)/.out/windows src/Terrabuild
	dotnet publish -c $(config) -r osx-x64 -p:PublishSingleFile=true --self-contained -o $(PWD)/.out/macos src/Terrabuild
	dotnet publish -c $(config) -r linux-x64 -p:PublishSingleFile=true --self-contained -o $(PWD)/.out/linux src/Terrabuild
	dotnet publish -c $(config) -p:PublishSingleFile=true -o $(PWD)/.out/dotnet src/Terrabuild

parser:
	dotnet build -c $(config) /p:DefineConstants="GENERATE_PARSER"

all:
	dotnet pack -c $(config) /p:Version=$(version) -o .nugets

self-dist: dist
	.out/dotnet/Terrabuild dist --workspace src --environment release --retry --debug

self-test: dist
	.out/dotnet/Terrabuild test --workspace src --environment release --retry --debug

self-publish: dist
	.out/dotnet/Terrabuild publish --workspace src --environment release --retry --debug

test:
	dotnet test



tests: run-build run-build-nc target usage

run-build-multirefs:
	dotnet run --project src/Terrabuild -- build --workspace tests/multirefs

run-build-circular:
	dotnet run --project src/Terrabuild -- build --workspace tests/circular

run-build:
	dotnet run --project src/Terrabuild -- build --workspace tests/simple --environment debug --label app --debug

run-rebuild:
	dotnet run --project src/Terrabuild -- build --workspace tests/simple --environment debug --label app --debug --nc

run-dist:
	dotnet run --project src/Terrabuild -- dist --workspace tests/simple --environment debug --debug

run-docker:
	dotnet run --project src/Terrabuild -- run docker --workspace tests/simple --environment debug --label app --debug --retry

run-push:
	dotnet run --project src/Terrabuild -- run push --workspace tests/simple --environment debug --label app --debug --retry

run-deploy:
	dotnet run --project src/Terrabuild -- run deploy --workspace tests/simple --environment debug --debug

run-deploy-dev:
	dotnet run --project src/Terrabuild -- run deploy --workspace tests/simple --environment debug --variable workspace=dev

run-build-app:
	dotnet run --project src/Terrabuild -- build --workspace tests/simple --environment debug --label dotnet --debug


github-tests:
	dotnet run --project src/Terrabuild -- run deploy --workspace tests/simple --environment debug --debug --retry --parallel 4

usage:
	dotnet run --project src/Terrabuild -- --help
	dotnet run --project src/Terrabuild -- build --help
	dotnet run --project src/Terrabuild -- run --help
	dotnet run --project src/Terrabuild -- serve --help
	dotnet run --project src/Terrabuild -- clear --help

clear-cache:
	dotnet run --project src/Terrabuild -- clear --buildcache

docker-prune:
	docker system prune -af

