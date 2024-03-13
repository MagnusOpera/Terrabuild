config ?= Debug
version ?= 0.0.0

build:
	dotnet build -c $(config)

dist:
	rm -rf $(PWD)/.out
	dotnet publish -c $(config) -r win-x64 -p:PublishSingleFile=true --self-contained -o $(PWD)/.out/windows src/Terrabuild
	cd .out/windows; zip -r ../terrabuild-$(version)-windows-x64.zip ./terrabuild.exe

	dotnet publish -c $(config) -r osx-x64 -p:PublishSingleFile=true --self-contained -o $(PWD)/.out/darwin src/Terrabuild
	cd .out/darwin; zip -r ../terrabuild-$(version)-darwin-x64.zip ./terrabuild

	dotnet publish -c $(config) -r linux-x64 -p:PublishSingleFile=true --self-contained -o $(PWD)/.out/linux src/Terrabuild
	cd .out/linux; zip -r ../terrabuild-$(version)-linux-x64.zip ./terrabuild

	dotnet publish -c $(config) -o $(PWD)/.out/dotnet src/Terrabuild
	cd .out/dotnet; zip -r ../dotnet-$(version).zip ./*

parser:
	dotnet build -c $(config) /p:DefineConstants="GENERATE_PARSER"

all:
	dotnet pack -c $(config) /p:Version=$(version) -o .nugets

self-dist:
	.out/dotnet/terrabuild dist --workspace src --environment $(config) --retry --debug

self-test:
	.out/dotnet/terrabuild test --workspace src --environment $(config) --retry --debug

self-publish:
	.out/dotnet/terrabuild publish --workspace src --environment $(config) --retry --debug

test:
	dotnet test



tests: run-build run-build-nc target usage

run-build-multirefs:
	dotnet run --project src/Terrabuild -- build --workspace tests/multirefs

run-build-circular:
	dotnet run --project src/Terrabuild -- build --workspace tests/circular

run-scafold:
	dotnet run --project src/Terrabuild -- scafold --workspace tests/scafold

run-rescafold:
	dotnet run --project src/Terrabuild -- scafold --workspace tests/scafold --force

run-build-scafold:
	dotnet run --project src/Terrabuild -- build --workspace tests/scafold --debug --retry

run-publish-scafold:
	dotnet run --project src/Terrabuild -- publish --workspace tests/scafold --debug --retry

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

