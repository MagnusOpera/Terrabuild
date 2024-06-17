# Contributing to Terrabuild

Thanks for contributing to Terrabuild and helping make it better. We appreciate the help!

## Communications

You are welcome to join the [Terrabuild Community Slack](https://terrabuild.io/community/) for questions and feature requests.
We discuss features and file bugs on GitHub via [Issues](https://github.com/MagnusOpera/Terrabuild/issues) as well as [Discussions](https://github.com/MagnusOpera/Terrabuild/discussions).

### Issues

Feel free to pick up any existing issue that looks interesting to you or fix a bug you stumble across while using Terrabuild. No matter the size, we welcome all improvements.

Please keep in mind Terrabuild is a young product. We are doing our best to move forward while keeping product simple.

### Feature Work

For larger features, we'd appreciate it if you open a [new issue](https://github.com/MagnusOpera/Terrabuild/issues/new) before investing a lot of time so we can discuss the feature together.
Please also be sure to browse [current issues](https://github.com/MagnusOpera/Terrabuild/issues) to make sure your issue is unique, to lighten the triage burden on our maintainers.
Finally, please limit your pull requests to contain only one feature at a time. Separating feature work into individual pull requests helps speed up code review and reduces the barrier to merge.

## Developing

### Setting up your Terrabuild development environment

You'll want to install the following on your machine:

- [.net SDK](https://dotnet.microsoft.com/download)
- [GNU Make](https://www.gnu.org/software/make/)
- [Docker](https://www.docker.com/products/docker-desktop/) or [OrbStack](https://orbstack.dev/)

### Build

We use `make` as shortcuts to run commands.

We develop mainly on macOS and Ubuntu - with limited support for doing development on Windows. Feel free to pitch in if you can to improve situation.

`Makefile` contains several targets. The ones you care are:
1. `build`: build Terrabuild
1. `test`: build and run tests
1. `dist-all`: build and publish all artifacts
1. `self-build`: build Terrabuild, which is used to self-build
1. `tb-build`: build Terrabuild using locally installed Terrabuild

You probably also want to install current Terrabuild distribution: `dotnet tool install --global terrabuild`

## Submitting a Pull Request

For contributors we use the standard GitHub workflow: fork, create a branch and when ready, open a pull request from your fork.

### Changelog messages

Changelog notes are written in the active imperative form. They should not end with a period. The simple rule is to pretend the message starts with "This change will ..."

Good examples for changelog entries are:
- move whatif at task level
- invalidate local cache on cache inconsistency

Here's some examples of what we're trying to avoid:
- Fixes a bug
- Adds a feature
- Feature now does something

### Magnus Opera employees

Magnus Opera employees have write access to Magnus Opera repositories and must push directly to branches rather than forking the repository. Tests can run directly without approval for PRs based on branches rather than forks.

## Getting Help

We're sure there are rough edges and we appreciate you helping out. If you want to talk with other folks in the Terrabuild community (including members of the Magnus Opera team) come hang out in the `#contribute` channel on the [Terrabuild Community Slack](https://terrabuild.io/community/).
