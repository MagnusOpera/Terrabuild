config ?= Debug

build:
	dotnet build

self:
	dotnet publish src/Terrabuild -o $(PWD)/out/dotnet

publish: self
	out/dotnet/Terrabuild publish --workspace src --environment release --retry --debug

dist:
	rm -rf $(PWD)/out
	dotnet publish -c $(config) -r win-x64 -p:PublishSingleFile=true --self-contained -o $(PWD)/out/windows src/Terrabuild
	cd out/windows; zip -r ../windows.zip ./*

	dotnet publish -c $(config) -r osx-x64 -p:PublishSingleFile=true --self-contained -o $(PWD)/out/macos src/Terrabuild
	cd out/macos; zip -r ../macos.zip ./*

	dotnet publish -c $(config) -r linux-x64 -p:PublishSingleFile=true --self-contained -o $(PWD)/out/linux src/Terrabuild
	cd out/linux; zip -r ../linux.zip ./*


tests: run-build run-build-nc target usage

run-build-multirefs:
	dotnet run --project src/Terrabuild -- build --workspace tests/multirefs

run-build-circular:
	dotnet run --project src/Terrabuild -- build --workspace tests/circular

run-build:
	dotnet run --project src/Terrabuild -- build --workspace tests/simple --environment debug --label app --debug

run-rebuild:
	dotnet run --project src/Terrabuild -- build --workspace tests/simple --environment debug --label app --debug --nc

run-build-az:
	dotnet run --project src/Terrabuild -- build --workspace tests/simple --environment debug --label app --debug --ci

run-dist:
	dotnet run --project src/Terrabuild -- dist --workspace tests/simple --environment debug --debug

run-docker:
	dotnet run --project src/Terrabuild -- run docker --workspace tests/simple --environment debug --label app --debug --retry

run-push:
	dotnet run --project src/Terrabuild -- run push --workspace tests/simple --environment debug --label app --debug --retry

run-deploy:
	dotnet run --project src/Terrabuild -- run deploy --workspace tests/simple --environment debug --debug

run-deploy-az:
	dotnet run --project src/Terrabuild -- run deploy --workspace tests/simple --environment debug --debug --ci

run-deploy-dev:
	dotnet run --project src/Terrabuild -- run deploy --workspace tests/simple --environment debug --variable workspace=dev

run-build-app:
	dotnet run --project src/Terrabuild -- build --workspace tests/simple --environment debug --label dotnet --debug

run-deploy-az-retry:
	dotnet run --project src/Terrabuild -- run deploy --workspace tests/simple --environment debug --debug --ci --retry


github-tests:
	dotnet run --project src/Terrabuild -- run deploy --workspace tests/simple --environment debug --debug --ci --retry --parallel 4

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

