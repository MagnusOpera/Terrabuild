config ?= default
env ?= default

version ?= 0.0.0
version_suffix ?=

ifeq ($(config), default)
	buildconfig = Debug
else
	buildconfig = $(config)
endif

ifeq ($(version_suffix), )
	full_version = $(version)
else
	full_version = $(version)-$(version_suffix)
endif

current_dir = $(shell pwd)


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

test:
	dotnet test terrabuild.sln

parser:
	dotnet build -c $(buildconfig) /p:DefineConstants="GENERATE_PARSER"

clean:
	-rm terrabuild-debug.*
	-rm -rf $(PWD)/.out

upgrade:
	dotnet restore --force-evaluate

usage:
	dotnet run --project src/Terrabuild -- --help
	dotnet run --project src/Terrabuild -- run --help
	dotnet run --project src/Terrabuild -- clear --help
	dotnet run --project src/Terrabuild -- login --help

#
# .______       _______  __       _______     ___           _______. _______
# |   _  \     |   ____||  |     |   ____|   /   \         /       ||   ____|
# |  |_)  |    |  |__   |  |     |  |__     /  ^  \       |   (----`|  |__
# |      /     |   __|  |  |     |   __|   /  /_\  \       \   \    |   __|
# |  |\  \----.|  |____ |  `----.|  |____ /  _____  \  .----)   |   |  |____
# | _| `._____||_______||_______||_______/__/     \__\ |_______/    |_______|
#

publish:
	dotnet publish -c $(buildconfig) -p:Version=$(full_version) -p:VersionSuffix=$(version_suffix) -o $(PWD)/.out/dotnet src/Terrabuild
	cd .out/dotnet; zip -r ../dotnet-$(full_version).zip ./*

publish-all: clean
	dotnet publish -c $(buildconfig) -r win-x64 -p:PublishSingleFile=true --self-contained -p:Version=$(full_version) -p:VersionSuffix=$(version_suffix) -o $(PWD)/.out/windows src/Terrabuild
	cd .out/windows; zip -r ../terrabuild-$(full_version)-windows-x64.zip ./terrabuild.exe

	dotnet publish -c $(buildconfig) -r osx-x64 -p:PublishSingleFile=true --self-contained -p:Version=$(full_version) -p:VersionSuffix=$(version_suffix) -o $(PWD)/.out/darwin src/Terrabuild
	cd .out/darwin; zip -r ../terrabuild-$(full_version)-darwin-x64.zip ./terrabuild

	dotnet publish -c $(buildconfig) -r linux-x64 -p:PublishSingleFile=true --self-contained -p:Version=$(full_version) -p:VersionSuffix=$(version_suffix) -o $(PWD)/.out/linux src/Terrabuild
	cd .out/linux; zip -r ../terrabuild-$(full_version)-linux-x64.zip ./terrabuild

	dotnet publish -c $(buildconfig) -p:Version=$(full_version) -p:VersionSuffix=$(version_suffix) -o $(PWD)/.out/dotnet src/Terrabuild
	cd .out/dotnet; zip -r ../dotnet-$(full_version).zip ./*

pack:
	dotnet pack -c $(buildconfig) -p:Version=$(full_version) -p:VersionSuffix=$(version_suffix) -o .out

dist-all: clean publish-all pack

docs:
	dotnet build src/Terrabuild.Extensions -c $(buildconfig)
	dotnet run --project tools/DocGen -- src/Terrabuild.Extensions/bin/$(buildconfig)/net8.0/Terrabuild.Extensions.xml ../website/content/docs/extensions

self: clean publish
	.out/dotnet/terrabuild run build test dist --workspace src --configuration $(env) --retry --debug --logs --localonly

terrabuild:
	terrabuild run build test dist --workspace src --configuration $(env) --retry --debug --logs --localonly


#
# .___________. _______     _______.___________.    _______.
# |           ||   ____|   /       |           |   /       |
# `---|  |----`|  |__     |   (----`---|  |----`  |   (----`
#     |  |     |   __|     \   \       |  |        \   \
#     |  |     |  |____.----)   |      |  |    .----)   |
#     |__|     |_______|_______/       |__|    |_______/
#

run-build-circular:
	dotnet run --project src/Terrabuild -- run build --workspace tests/circular

run-scaffold:
	dotnet run --project src/Terrabuild -- scaffold --workspace tests/scaffold

run-rescaffold:
	dotnet run --project src/Terrabuild -- scaffold --workspace tests/scaffold --force

run-build-scaffold:
	dotnet run --project src/Terrabuild -- run build --workspace tests/scaffold --debug --retry


define diff_results
	diff $(1)/results/terrabuild-debug.config.json $(1)/terrabuild-debug.config.json
	diff $(1)/results/terrabuild-debug.config-graph.json $(1)/terrabuild-debug.config-graph.json
	diff $(1)/results/terrabuild-debug.consistent-graph.json $(1)/terrabuild-debug.consistent-graph.json
	diff $(1)/results/terrabuild-debug.required-graph.json $(1)/terrabuild-debug.required-graph.json
	diff $(1)/results/terrabuild-debug.build-graph.json $(1)/terrabuild-debug.build-graph.json
	diff $(1)/results/terrabuild-debug.config-graph.mermaid $(1)/terrabuild-debug.config-graph.mermaid
	diff $(1)/results/terrabuild-debug.consistent-graph.mermaid $(1)/terrabuild-debug.consistent-graph.mermaid
	diff $(1)/results/terrabuild-debug.required-graph.mermaid $(1)/terrabuild-debug.required-graph.mermaid
	diff $(1)/results/terrabuild-debug.build-graph.mermaid $(1)/terrabuild-debug.build-graph.mermaid
endef


define run_integration_test
	@printf "\n*** Running integration test %s ***\n" $(1)
	-cd $(1); rm terrabuild-debug.*
	cd $(1); GITHUB_SHA=1234 GITHUB_REF_NAME=main GITHUB_STEP_SUMMARY=terrabuild-debug.md $(current_dir)/.out/dotnet/terrabuild $(2)
	$(call diff_results, $(1))
endef

run-test-insights:
	dotnet run --project src/Terrabuild -- run build test apply plan -w ../../insights --debug --force --localonly --whatif

run-test-terrabuild:
	dotnet run --project src/Terrabuild -- run build test publish -w src --debug --force --localonly

run-test-cluster-layers:
	dotnet run --project src/Terrabuild -- run build -w tests/cluster-layers --debug --force

run-test-simple:
	dotnet run --project src/Terrabuild -- run build -w tests/simple --debug --force

# $(call run_integration_test, tests/cluster-layers, run build --force --debug -p 2 --logs)

self-test-cluster-layers:
	$(call run_integration_test, tests/cluster-layers, run build --force --debug -p 2 --logs)

self-test-multirefs:
	$(call run_integration_test, tests/multirefs, run build --force --debug -p 2 --logs)

self-test-simple:
	$(call run_integration_test, tests/simple, run build --force --debug -p 2 --logs)

self-test-all: publish self-test-cluster-layers self-test-multirefs self-test-simple
