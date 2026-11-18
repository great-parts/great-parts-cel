# Support

## Where to ask

Start with the [README](README.md) and the [documentation](docs/). Ask questions and report bugs in [GitHub issues](https://github.com/great-parts/great-parts-cel/issues). GitHub Discussions are not enabled.

A vulnerability goes through [SECURITY.md](SECURITY.md), not through an issue.

## Supported versions

Versions 1.x are not supported: they get no fixes. From release 2.0 only the latest release is supported: a bug is fixed in a new release, and earlier releases get no fixes. Please check a bug against the latest release before you report it, and give its version in the issue.

## Reporting a bug

Please include:

- the package version, or the commit for a build from the repository;
- the target framework (`net10.0` or `net11.0`), JIT or Native AOT, and the operating system;
- the expression and its variables as JSON;
- the result you expect and the result you get. When the result differs from cel-go, give the result of cel-go for the same expression.

Differences from cel-go that have already been analysed are listed in [known-differences.md](tests/Great.Parts.Cel.Conformance/Regression/known-differences.md).

## Answers

The maintainer reads and answers issues. There is no guaranteed response time. Everyone in the issues follows the [code of conduct](CODE_OF_CONDUCT.md).
