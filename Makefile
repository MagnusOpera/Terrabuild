env ?= default

version ?= 0.0.0

ifeq ($(env), default)
	config = Debug
else
	config = $env
endif


.PHONY: src tools


#
#  _______   ___________    ____
# |       \ |   ____\   \  /   /
# |  .--.  ||  |__   \   \/   /
# |  |  |  ||   __|   \      /
# |  '--'  ||  |____   \    /
# |_______/ |_______|   \__/
#

build:
	dotnet build -c $(config) terrabuild.sln

src:
	dotnet build -c $(config) src/src.sln

tools:
	dotnet build -c $(config) tools/tools.sln

test:
	dotnet test terrabuild.sln

parser:
	dotnet build -c $(config) /p:DefineConstants="GENERATE_PARSER"

clean:
	-rm terrabuild-debug.*
	-rm -rf $(PWD)/.out


clear-cache:
	dotnet run --project src/Terrabuild -- clear --cache --home

docker-prune:
	docker system prune -af

#
# .______       _______  __       _______     ___           _______. _______
# |   _  \     |   ____||  |     |   ____|   /   \         /       ||   ____|
# |  |_)  |    |  |__   |  |     |  |__     /  ^  \       |   (----`|  |__
# |      /     |   __|  |  |     |   __|   /  /_\  \       \   \    |   __|
# |  |\  \----.|  |____ |  `----.|  |____ /  _____  \  .----)   |   |  |____
# | _| `._____||_______||_______||_______/__/     \__\ |_______/    |_______|
#

publish:
	dotnet publish -c $(config) -p:Version=$(version) -o $(PWD)/.out/dotnet src/Terrabuild
	cd .out/dotnet; zip -r ../dotnet-$(version).zip ./*

publish-all: clean
	dotnet publish -c $(config) -r win-x64 -p:PublishSingleFile=true --self-contained -p:Version=$(version) -o $(PWD)/.out/windows src/Terrabuild
	cd .out/windows; zip -r ../terrabuild-$(version)-windows-x64.zip ./terrabuild.exe

	dotnet publish -c $(config) -r osx-x64 -p:PublishSingleFile=true --self-contained -p:Version=$(version) -o $(PWD)/.out/darwin src/Terrabuild
	cd .out/darwin; zip -r ../terrabuild-$(version)-darwin-x64.zip ./terrabuild

	dotnet publish -c $(config) -r linux-x64 -p:PublishSingleFile=true --self-contained -p:Version=$(version) -o $(PWD)/.out/linux src/Terrabuild
	cd .out/linux; zip -r ../terrabuild-$(version)-linux-x64.zip ./terrabuild

	dotnet publish -c $(config) -p:Version=$(version) -o $(PWD)/.out/dotnet src/Terrabuild
	cd .out/dotnet; zip -r ../dotnet-$(version).zip ./*

pack:
	dotnet pack -c $(config) /p:Version=$(version) -o .out

dist-all: clean publish-all pack

docs:
	dotnet build src/Terrabuild.Extensions -c $(config)
	dotnet run --project tools/DocGen -- src/Terrabuild.Extensions/bin/$(config)/net8.0/Terrabuild.Extensions.xml ../websites/terrabuild.io/content/docs/extensions

self-build: clean publish
	.out/dotnet/terrabuild build --workspace src --environment $(env) --retry --debug

self-dist: clean publish
	.out/dotnet/terrabuild dist --workspace src --environment $(env) --retry --debug

self-test: clean publish
	.out/dotnet/terrabuild test --workspace src --environment $(env) --retry --debug

self-publish: clean publish
	.out/dotnet/terrabuild publish --workspace src --environment $(env) --retry --debug

self-check: clean publish
	.out/dotnet/terrabuild publish --workspace src --environment $(env) --retry --debug --whatif


#
# .___________. _______     _______.___________.    _______.
# |           ||   ____|   /       |           |   /       |
# `---|  |----`|  |__     |   (----`---|  |----`  |   (----`
#     |  |     |   __|     \   \       |  |        \   \
#     |  |     |  |____.----)   |      |  |    .----)   |
#     |__|     |_______|_______/       |__|    |_______/
#

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
	dotnet run --project src/Terrabuild -- build --workspace tests/simple --environment $(env) --debug

run-build-playground: clean
	dotnet run --project src/Terrabuild -- deploy --workspace ../playgrounds/terrabuild

run-build-env: clean
	TB_VAR_secret_message="pouet pouet" dotnet run --project src/Terrabuild -- build --workspace tests/simple --environment $(env) --debug

run-rebuild: clean
	dotnet run --project src/Terrabuild -- build --workspace tests/simple --environment $(env) --label app --debug --force

run-dist:
	dotnet run --project src/Terrabuild -- dist --workspace tests/simple --environment $(env) --debug

run-docker:
	dotnet run --project src/Terrabuild -- run docker --workspace tests/simple --environment $(env) --label app --debug --retry

run-push:
	dotnet run --project src/Terrabuild -- run push --workspace tests/simple --environment $(env) --label app --debug --retry

run-deploy:
	dotnet run --project src/Terrabuild -- run deploy --workspace tests/simple --environment $(env) --debug --retry

run-deploy-dev:
	dotnet run --project src/Terrabuild -- run deploy --workspace tests/simple --environment $(env) --variable workspace=dev

run-build-app:
	dotnet run --project src/Terrabuild -- build --workspace tests/simple --environment $(env) --label dotnet --debug

github-tests:
	dotnet run --project src/Terrabuild -- run deploy --workspace tests/simple --environment $(env) --debug --retry --parallel 4

usage:
	dotnet run --project src/Terrabuild -- --help
	dotnet run --project src/Terrabuild -- build --help
	dotnet run --project src/Terrabuild -- run --help

version:
	dotnet run --project src/Terrabuild -- version

