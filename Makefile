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

publish-all: clean
	dotnet publish -c $(buildconfig) -p:Version=$(full_version) -p:VersionSuffix=$(version_suffix) -o $(PWD)/.out/dotnet src/Terrabuild

	dotnet pack -c $(buildconfig) -p:Version=$(full_version) -p:VersionSuffix=$(version_suffix) -o .out

	dotnet publish -c $(buildconfig) -r win-x64 -p:PublishSingleFile=true --self-contained -p:Version=$(full_version) -p:VersionSuffix=$(version_suffix) -o $(PWD)/.out/windows src/Terrabuild

	dotnet publish -c $(buildconfig) -r osx-x64 -p:PublishSingleFile=true --self-contained -p:Version=$(full_version) -p:VersionSuffix=$(version_suffix) -o $(PWD)/.out/darwin/x64 src/Terrabuild
	dotnet publish -c $(buildconfig) -r osx-arm64 -p:PublishSingleFile=true --self-contained -p:Version=$(full_version) -p:VersionSuffix=$(version_suffix) -o $(PWD)/.out/darwin/arm64 src/Terrabuild

	dotnet publish -c $(buildconfig) -r linux-x64 -p:PublishSingleFile=true --self-contained -p:Version=$(full_version) -p:VersionSuffix=$(version_suffix) -o $(PWD)/.out/linux src/Terrabuild

docs:
	dotnet build src/Terrabuild.Extensions -c $(buildconfig) /p:GenerateDocumentationFile=true
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
	dotnet run --project src/Terrabuild -- run build --workspace tests/circular --debug --logs

run-scaffold:
	dotnet run --project src/Terrabuild -- scaffold --workspace tests/scaffold --debug --logs

run-rescaffold:
	dotnet run --project src/Terrabuild -- scaffold --workspace tests/scaffold --debug --force --logs

run-build-scaffold:
	dotnet run --project src/Terrabuild -- run build --workspace tests/scaffold --debug --retry --logs

run-build-simple:
	dotnet run --project src/Terrabuild -- run build --workspace tests/simple --debug --retry --logs

run-rebuild-simple:
	dotnet run --project src/Terrabuild -- run build --workspace tests/simple --debug --retry --logs

run-deploy-simple:
	dotnet run --project src/Terrabuild -- run deploy --workspace tests/simple --debug --retry --logs

run-build-playground:
	dotnet run --project src/Terrabuild -- run build --workspace ../playground --retry --debug

run-dist-playground:
	dotnet run --project src/Terrabuild -- run dist --workspace ../playground --retry --debug

run-deploy-playground:
	dotnet run --project src/Terrabuild -- run deploy --workspace ../playground --retry --debug

run-test-insights:
	dotnet run --project src/Terrabuild -- run build test apply plan -w ../../insights --debug --force --localonly --whatif

run-test-terrabuild:
	dotnet run --project src/Terrabuild -- run build test publish -w src --debug --force --localonly

run-test-cluster-layers:
	dotnet run --project src/Terrabuild -- run build -w tests/cluster-layers --debug --force

run-test-simple:
	dotnet run --project src/Terrabuild -- run build -w tests/simple --debug --force

define diff_file
#	cp $(1)/$(2) $(1)/results/$(2)
	diff $(1)/results/$(2) $(1)/$(2)
endef

define diff_results
	$(call diff_file,$(1),terrabuild-debug.config.json)
	$(call diff_file,$(1),terrabuild-debug.build-graph.json)
	$(call diff_file,$(1),terrabuild-debug.build-graph.mermaid)
endef


define run_integration_test
	@printf "\n*** Running integration test %s ***\n" $(1)
	-cd $(1); rm terrabuild-debug.*
	cd $(1); GITHUB_SHA=1234 GITHUB_REF_NAME=main GITHUB_STEP_SUMMARY=terrabuild-debug.md GITHUB_REPOSITORY=MagnusOpera/Terrabuild GITHUB_RUN_ID=42 $(current_dir)/.out/dotnet/terrabuild $(2)
	$(call diff_results,$(1))
endef

# $(call run_integration_test, tests/cluster-layers, run build --force --debug -p 2 --logs)

self-test-cluster-layers:
	$(call run_integration_test, tests/cluster-layers, run build --force --debug -p 2 --logs --containertool docker)

self-test-multirefs:
	$(call run_integration_test, tests/multirefs, run build --force --debug -p 2 --logs --containertool docker)

self-test-simple:
	$(call run_integration_test, tests/simple, run build --force --debug -p 2 --logs --containertool docker)

self-test-all: publish self-test-cluster-layers self-test-multirefs self-test-simple
