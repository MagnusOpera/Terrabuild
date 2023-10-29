build:
	dotnet build


tests: run-build run-build-nc target usage

run-build-multirefs:
	dotnet run --project Terrabuild -- build --workspace tests/multirefs

run-build-circular:
	dotnet run --project Terrabuild -- build --workspace tests/circular

run-build:
	dotnet run --project Terrabuild -- build --workspace tests/simple --environment debug --label app --debug

run-build-az:
	dotnet run --project Terrabuild -- build --workspace tests/simple --environment debug --label app --debug --ci

run-dist:
	dotnet run --project Terrabuild -- dist --workspace tests/simple --environment debug --debug

run-docker:
	dotnet run --project Terrabuild -- run docker --workspace tests/simple --environment debug --label app --debug --retry

run-push:
	dotnet run --project Terrabuild -- run push --workspace tests/simple --environment debug --label app --debug --retry

run-deploy:
	dotnet run --project Terrabuild -- run deploy --workspace tests/simple --environment debug --debug

run-deploy-az:
	dotnet run --project Terrabuild -- run deploy --workspace tests/simple --environment debug --debug --ci

run-deploy-dev:
	dotnet run --project Terrabuild -- run deploy --workspace tests/simple --environment debug --variable workspace=dev

run-build-app:
	dotnet run --project Terrabu ild -- build --workspace tests/simple --environment debug --label dotnet --debug

run-deploy-az-retry:
	dotnet run --project Terrabuild -- run deploy --workspace tests/simple --environment debug --debug --ci --retry

run-build-nc:
	dotnet run --project Terrabuild -- build --workspace tests/simple --nocache --env debug


github-tests:
	dotnet run --project Terrabuild -- run deploy --workspace tests/simple --environment debug --debug --ci --retry --parallel 4

usage:
	dotnet run --project Terrabuild -- --help
	dotnet run --project Terrabuild -- build --help
	dotnet run --project Terrabuild -- run --help
	dotnet run --project Terrabuild -- serve --help
	dotnet run --project Terrabuild -- clear --help

clear-cache:
	dotnet run --project Terrabuild -- clear --buildcache

docker-prune:
	docker system prune -af

