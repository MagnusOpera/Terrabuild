config ?= Debug
version ?= 0.0.0

.PHONY: src tools tests

build:
	dotnet build -c $(config) terrabuild.sln

src:
	dotnet build -c $(config) src/src.sln

tools:
	dotnet build -c $(config) tools/tools.sln

tests:
	dotnet test src/terrabuild.sln


clean:
	-rm terrabuild-debug.*
	-rm -rf $(PWD)/.out

dist:
	dotnet publish -c $(config) -o $(PWD)/.out/dotnet src/Terrabuild
	cd .out/dotnet; zip -r ../dotnet-$(version).zip ./*

dist-all: clean
	dotnet publish -c $(config) -r win-x64 -p:PublishSingleFile=true --self-contained -o $(PWD)/.out/windows src/Terrabuild
	cd .out/windows; zip -r ../terrabuild-$(version)-windows-x64.zip ./terrabuild.exe

	dotnet publish -c $(config) -r osx-x64 -p:PublishSingleFile=true --self-contained -o $(PWD)/.out/darwin src/Terrabuild
	cd .out/darwin; zip -r ../terrabuild-$(version)-darwin-x64.zip ./terrabuild

	dotnet publish -c $(config) -r linux-x64 -p:PublishSingleFile=true --self-contained -o $(PWD)/.out/linux src/Terrabuild
	cd .out/linux; zip -r ../terrabuild-$(version)-linux-x64.zip ./terrabuild

	dotnet publish -c $(config) -o $(PWD)/.out/dotnet src/Terrabuild
	cd .out/dotnet; zip -r ../dotnet-$(version).zip ./*

docs:
	dotnet build src/Terrabuild.Extensions -c $(config)
	dotnet run --project tools/DocGen -- src/Terrabuild.Extensions/bin/$(config)/net8.0/Terrabuild.Extensions.xml ../websites/terrabuild.io/content/docs/extensions

parser:
	dotnet build -c $(config) /p:DefineConstants="GENERATE_PARSER"

all:
	dotnet pack -c $(config) /p:Version=$(version) -o .nugets

self-dist: clean dist
	.out/dotnet/terrabuild dist --workspace src --environment $(config) --retry --debug

self-test: clean dist
	.out/dotnet/terrabuild test --workspace src --environment $(config) --retry --debug

self-publish: clean dist
	.out/dotnet/terrabuild publish --workspace src --environment $(config) --retry --debug

self-check: clean dist
	.out/dotnet/terrabuild publish --workspace src --environment $(config) --retry --debug --whatif



tests: run-build run-build-nc target usage

run-build-multirefs:
	dotnet run --project src/Terrabuild -- build --workspace tests/multirefs

run-build-circular:
	dotnet run --project src/Terrabuild -- build --workspace tests/circular

run-scaffold:
	dotnet run --project src/Terrabuild -- scaffold --workspace tests/scaffold

run-rescaffold:
	dotnet run --project src/Terrabuild -- scaffold --workspace tests/scaffold --force

run-build-scaffold:
	dotnet run --project src/Terrabuild -- build --workspace tests/scaffold --debug --retry

run-publish-scaffold:
	dotnet run --project src/Terrabuild -- publish --workspace tests/scaffold --debug --retry

run-build: clean
	dotnet run --project src/Terrabuild -- build --workspace tests/simple --environment debug --debug

run-rebuild: clean
	dotnet run --project src/Terrabuild -- build --workspace tests/simple --environment debug --label app --debug --force

run-dist:
	dotnet run --project src/Terrabuild -- dist --workspace tests/simple --environment debug --debug

run-docker:
	dotnet run --project src/Terrabuild -- run docker --workspace tests/simple --environment debug --label app --debug --retry

run-push:
	dotnet run --project src/Terrabuild -- run push --workspace tests/simple --environment debug --label app --debug --retry

run-deploy:
	dotnet run --project src/Terrabuild -- run deploy --workspace tests/simple --environment debug --debug --retry

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

clear-cache:
	dotnet run --project src/Terrabuild -- clear --buildcache

docker-prune:
	docker system prune -af

