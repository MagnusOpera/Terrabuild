config ?= default
env ?= default

version ?= 0.0.0

ifeq ($(config), default)
	buildconfig = Debug
else
	buildconfig = $(config)
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
	dotnet build -c $(buildconfig) terrabuild.sln

src:
	dotnet build -c $(buildconfig) src/src.sln

tools:
	dotnet build -c $(buildconfig) tools/tools.sln

test:
	dotnet test terrabuild.sln

parser:
	dotnet build -c $(buildconfig) /p:DefineConstants="GENERATE_PARSER"

clean:
	-rm terrabuild-debug.*
	-rm -rf $(PWD)/.out

upgrade:
	dotnet restore --force-evaluate

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
	dotnet publish -c $(buildconfig) -p:Version=$(version) -o $(PWD)/.out/dotnet src/Terrabuild
	cd .out/dotnet; zip -r ../dotnet-$(version).zip ./*

publish-all: clean
	dotnet publish -c $(buildconfig) -r win-x64 -p:PublishSingleFile=true --self-contained -p:Version=$(version) -o $(PWD)/.out/windows src/Terrabuild
	cd .out/windows; zip -r ../terrabuild-$(version)-windows-x64.zip ./terrabuild.exe

	dotnet publish -c $(buildconfig) -r osx-x64 -p:PublishSingleFile=true --self-contained -p:Version=$(version) -o $(PWD)/.out/darwin src/Terrabuild
	cd .out/darwin; zip -r ../terrabuild-$(version)-darwin-x64.zip ./terrabuild

	dotnet publish -c $(buildconfig) -r linux-x64 -p:PublishSingleFile=true --self-contained -p:Version=$(version) -o $(PWD)/.out/linux src/Terrabuild
	cd .out/linux; zip -r ../terrabuild-$(version)-linux-x64.zip ./terrabuild

	dotnet publish -c $(buildconfig) -p:Version=$(version) -o $(PWD)/.out/dotnet src/Terrabuild
	cd .out/dotnet; zip -r ../dotnet-$(version).zip ./*

pack:
	dotnet pack -c $(buildconfig) /p:Version=$(version) -o .out

dist-all: clean publish-all pack


docs:
	dotnet build src/Terrabuild.Extensions -c $(buildconfig)
	dotnet run --project tools/DocGen -- src/Terrabuild.Extensions/bin/$(buildconfig)/net8.0/Terrabuild.Extensions.xml ../website/content/docs/extensions


self-build: clean publish
	.out/dotnet/terrabuild run build --workspace src --configuration $(env) --retry --debug --logs

self-build-local: clean publish
	.out/dotnet/terrabuild run build --workspace src --configuration $(env) --retry --debug --logs --localonly

self-dist: clean publish
	.out/dotnet/terrabuild run dist --workspace src --configuration $(env) --retry --debug --logs

self-test: clean publish
	.out/dotnet/terrabuild run test --workspace src --configuration $(env) --retry --debug --whatif --localonly

self-publish: clean publish
	.out/dotnet/terrabuild run dist --workspace src --configuration $(env) --retry --debug --logs

self-check: clean publish
	.out/dotnet/terrabuild run dist --workspace src --configuration $(env) --retry --debug --whatif --logs


tb-build: clean
	terrabuild run build --workspace src --configuration $(env) --retry --debug

tb-dist: clean
	terrabuild run dist --workspace src --configuration $(env) --retry --debug --tag $(version)

tb-test: clean
	terrabuild run test --workspace src --configuration $(env) --retry --debug

tb-publish: clean
	terrabuild run dist --workspace src --configuration $(env) --retry --debug

tb-check: clean
	terrabuild run dist --workspace src --configuration $(env) --retry --debug --whatif



#
# .___________. _______     _______.___________.    _______.
# |           ||   ____|   /       |           |   /       |
# `---|  |----`|  |__     |   (----`---|  |----`  |   (----`
#     |  |     |   __|     \   \       |  |        \   \
#     |  |     |  |____.----)   |      |  |    .----)   |
#     |__|     |_______|_______/       |__|    |_______/
#

run-build-multirefs:
	dotnet run --project src/Terrabuild -- run build --workspace tests/multirefs

run-build-circular:
	dotnet run --project src/Terrabuild -- run build --workspace tests/circular

run-scaffold:
	dotnet run --project src/Terrabuild -- scaffold --workspace tests/scaffold

run-rescaffold:
	dotnet run --project src/Terrabuild -- scaffold --workspace tests/scaffold --force

run-build-scaffold:
	dotnet run --project src/Terrabuild -- run build --workspace tests/scaffold --debug --retry

run-publish-scaffold:
	dotnet run --project src/Terrabuild -- run dist --workspace tests/scaffold --debug --retry

run-build: clean
	dotnet run --project src/Terrabuild -- run build --workspace tests/simple --configuration $(env) --debug

run-build-playground: clean
	dotnet run --project src/Terrabuild -- run deploy --workspace ../playgrounds/terrabuild

run-build-env: clean
	TB_VAR_secret_message="pouet pouet" dotnet run --project src/Terrabuild -- run build --workspace tests/simple --configuration $(env) --debug

run-rebuild: clean
	dotnet run --project src/Terrabuild -- run build --workspace tests/simple --configuration $(env) --label app --debug --force

run-dist:
	dotnet run --project src/Terrabuild -- run dist --workspace tests/simple --configuration $(env) --debug

run-docker:
	dotnet run --project src/Terrabuild -- run docker --workspace tests/simple --configuration $(env) --label app --debug --retry

run-push:
	dotnet run --project src/Terrabuild -- run push --workspace tests/simple --configuration $(env) --label app --debug --retry

run-deploy:
	dotnet run --project src/Terrabuild -- run deploy --workspace tests/simple --configuration $(env) --debug --retry

run-deploy-dev:
	dotnet run --project src/Terrabuild -- run deploy --workspace tests/simple --configuration $(env) --variable workspace=dev

run-build-app:
	dotnet run --project src/Terrabuild -- run build --workspace tests/simple --configuration $(env) --label dotnet --debug

run-graph-cluster-layers:
	dotnet run --project src/Terrabuild -- run build --workspace tests/cluster-layers --whatif --force --debug

github-tests:
	dotnet run --project src/Terrabuild -- run deploy --workspace tests/simple --configuration $(env) --debug --retry --parallel 4

usage:
	dotnet run --project src/Terrabuild -- --help
	dotnet run --project src/Terrabuild -- run --help
	dotnet run --project src/Terrabuild -- clear --help
	dotnet run --project src/Terrabuild -- login --help

version:
	dotnet run --project src/Terrabuild -- version

