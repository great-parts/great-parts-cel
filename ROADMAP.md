# Roadmap

What was planned. Release 2.0 is out, and no other release is planned. The plan had no dates: a step started when the one before it was done, and a step could change when its result was known. What is in the library is in [CHANGELOG.md](CHANGELOG.md).

## Versions 1.x

Versions 1.x are not supported: there are no fixes for them, security fixes included, and there is no maintenance branch. With 2.0 out, the packages 1.x are unlisted and marked as deprecated on nuget.org, as [SECURITY.md](SECURITY.md) says.

## Release 2.0

2.0 is a major version, so a breaking change of the public API was allowed in it, and a member of the API could be removed or replaced without an earlier `[Obsolete]` mark; see [Versioning and compatibility](docs/versioning.md). The steps below are in the order in which they were done; all of them are done except the run of the tests on .NET 12, which waits for the release of .NET 12.

1. **Unfinished work.** The documents of the repository follow this plan, the README shows the badges of OpenSSF Scorecard and Best Practices, the packages get an icon and the other metadata that nuget.org shows, and the library follows the master branch of cel-spec: the pinned commit moves and the cases that it adds are passed or listed with their reasons.
2. **A speed comparison.** The speed of this library is compared with other CEL libraries for .NET on compiling and evaluating expressions in a set of scenarios. The comparison runs on GitHub, in this repository, on a standard runner. Its result is published with the date of the run, the commit, the version of each library, the type of the runner and a link to the report, and it covers every scenario and operation, not a sample. The result is not a condition of a release: it is the input of the work on speed.
3. **Speed and a new API.** The work on speed follows the comparison:
   - a faster version of each operation in which the comparison shows room for it;
   - a deterministic finite automaton and a search for a literal prefix in `matches`, which make a match faster without changing its result or its cost; hashing in `distinct` and in the functions of `sets`, which changes the table of [Cost](docs/cost.md) and so comes in a major version;
   - a public API that is easier to use in different kinds of projects, including a package for the containers of dependency injection;
   - the way data enters an evaluation, if the comparison shows that it needs a new form. Today `CelBindings` binds the variables by name through a dictionary that is made for every evaluation, and a field or an argument crosses the border of the library as a 32-byte `CelValue`. A new form, such as binding by slot or typed reads of fields and arguments, would be added next to the present ones.
4. **More of CEL and of the platform.**
   - A public syntax tree, a way to give the library a parse tree from another parser, and the checked expression (`CheckedExpr`);
   - unknown values and an asynchronous driver of an evaluation;
   - `DateOnly` and `TimeOnly`, and `round` and `quantize` for `decimal`;
   - the `formats` extension set, with checks that a string has a text format (an e-mail address, a host name, a host and port, an IP prefix, a URI), in the core of the library; `char`, integers wider than 64 bits and object literals of .NET types;
   - the packages `Great.Parts.Cel.Policy`, for policies in the [CEL policy format](https://github.com/cel-expr/cel-policy) (YAML documents of rules that compile to CEL expressions), and `Great.Parts.Cel.Filtering`, for filters in the syntax of [AIP-160](https://google.aip.dev/160) (the `filter` string of a list method of an API); both build on the core of the library, which has the hooks they need, such as the position of an expression in its source document (`CelCompileOptions.SourceLocation`);
   - `browser-wasm`;
   - a run of the tests on .NET 12 in CI after it comes out.
5. **Freeze and release.** The extension points are reviewed, the record of the public API is fixed, the changelog and the README of the packages are written, the pinned commit of cel-spec moves for the last time, and the version 2.0.0 is released.
