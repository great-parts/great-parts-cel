# Great.Parts.Cel

[![NuGet version](https://img.shields.io/nuget/v/Great.Parts.Cel)](https://www.nuget.org/packages/Great.Parts.Cel)
[![CI status](https://github.com/great-parts/great-parts-cel/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/great-parts/great-parts-cel/actions/workflows/ci.yml)
[![OpenSSF Scorecard](https://img.shields.io/ossf-scorecard/github.com/great-parts/great-parts-cel?label=openssf%20scorecard&style=flat)](https://scorecard.dev/viewer/?uri=github.com/great-parts/great-parts-cel)
[![OpenSSF Best Practices](https://img.shields.io/cii/level/16897)](https://www.bestpractices.dev/projects/16897)

CEL (Common Expression Language) for .NET / C#.

**Status:** the current release is 2.0, and no other release is planned; see the [Roadmap](ROADMAP.md). Release 2.0 added the packages `Great.Parts.Cel.Policy` and `Great.Parts.Cel.Filtering`. Versions 1.x are not supported. The public API follows [Semantic Versioning](docs/versioning.md): a breaking change comes only in a major version.

[CEL](https://cel.dev/) is an expression language that is not Turing complete: an expression reads only the data that the application passes to it. An application compiles an expression once and evaluates it on different data many times. Great.Parts.Cel does both in a .NET application, under JIT and under Native AOT.

## Install

The library targets `net10.0` and `net11.0`. Add the package to a project:

```shell
dotnet add package Great.Parts.Cel
```

Messages of protobuf need a second package, `Great.Parts.Cel.Protobuf`, which depends on `Great.Parts.Cel` and `Google.Protobuf`; see [Protobuf messages](docs/protobuf.md). An application with dependency injection takes `Great.Parts.Cel.DependencyInjection` (an environment, its compilation cache and an evaluator as services, and the validation of options), and an ASP.NET Core application `Great.Parts.Cel.AspNetCore` (authorization policies and the validation of requests). Two more packages are part of 2.0. `Great.Parts.Cel.Policy` reads policies written in the [CEL policy format](https://github.com/cel-expr/cel-policy), YAML documents of rules that compile to CEL expressions. `Great.Parts.Cel.Filtering` reads the filters of [AIP-160](https://google.aip.dev/160), the `filter` string of a list method of an API, as CEL expressions. Both build on the core of the library and use its type checking. The type `decimal` is available on both targets, with a smaller range on `net10.0`; see [Decimal](docs/decimal.md).

## Quick start

The steps below take a clean machine with the .NET SDK 10 to a first result in less than five minutes; CI runs them in a clean container on every push to `main`.

<!-- quick-start:begin -->
Create a console application and add the package:

```shell
dotnet new console -o QuickStart
cd QuickStart
dotnet add package Great.Parts.Cel
```

Replace the content of `Program.cs` with the example:

```csharp
using Great.Parts.Cel;

Console.WriteLine(CelExpression.Evaluate<long>("(x + 2) / 3", new { x = 40L })); // Output: 14
Console.WriteLine(CelExpression.Evaluate<long>("(x + 2) / 3", new { x = -5L })); // Output: -1
```

Run it:

```shell
dotnet run
```
<!-- quick-start:end -->

`CelExpression.Evaluate<T>` is the simple entry: it compiles the expression, binds the variables from the properties of the anonymous object (here `x`, of the CEL type `int`) and returns the result as `T`. Every error is thrown as `CelException`, and every call has a cost limit.

For rules that run many times, build an environment once and compile each expression once. The environment declares what an expression may use: here the standard library and one variable `x`. `Compile<long>` parses the expression, checks its types and returns a program. The program is immutable: it is evaluated any number of times, from any number of threads. Errors are returned as results, each with its place in the expression; `Get…OrThrow()` turns an error into an exception and keeps the example short.

```csharp
using Great.Parts.Cel;

CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
    .AddVariable<long>("x")
    .Build()
    .GetEnvironmentOrThrow();

CelProgram<long> program = environment.Compile<long>("(x + 2) / 3").GetProgramOrThrow();

Console.WriteLine(program.Evaluate(new CelBindings().Add("x", 40L)).GetValueOrThrow()); // Output: 14
Console.WriteLine(program.Evaluate(new CelBindings().Add("x", -5L)).GetValueOrThrow()); // Output: -1
```

[Getting Started](docs/getting-started.md) has seven scenarios, from the simple entry on: an environment with variables and `JsonElement`, objects of the application, validation of a rule when a user saves it, functions of the application, a web service, and the compilation cache.

## What it covers

- **The language.** The complete CEL syntax, the type checking of the CEL language definition and the standard library, [version 1](docs/standard-library.md): arithmetic, comparisons, strings, lists, maps, `timestamp` and `duration` with time zones from a database that the application supplies or from the operating system, the standard macros, and `matches` with the syntax of RE2, found in time linear in the length of the string.
- **Extension sets.** The sets of cel-go, each added with an explicit version: `strings`, `math`, `lists`, `sets`, `encoders`, `bindings`, `two-var-comprehensions`, `network`, `optional` with its syntax, and `protos`; and `decimal`, a set of this library, on both targets. See [Extension sets](docs/extensions.md).
- **Data of the application.** Variables and results of .NET types — numbers, strings, time, enumerations, collections, `JsonElement`; objects of the application, described by a source generator at build time, with no reflection, so they work under Native AOT and trimming ([Type mapping](docs/type-mapping.md)), or described at run time ([Types described at run time](docs/runtime-types.md)); messages of protobuf, with `Any`, in the package `Great.Parts.Cel.Protobuf`. An environment can also be described in [JSON](docs/environment-json.md), and an application can add functions of its own, with the implementations and the costs it declares ([Application functions](docs/recipes/application-functions.md)).
- **Policies and filters.** The package `Great.Parts.Cel.Policy` reads a policy in the [CEL policy format](https://github.com/cel-expr/cel-policy): a YAML document with named variables and rules that take the first matching choice (`match`) or collect every matching choice (`aggregate`), compiled to one CEL expression. The package `Great.Parts.Cel.Filtering` reads a filter in the syntax of [AIP-160](https://google.aip.dev/160), such as `status = "open" AND total > 100`, and checks it against the environment of the application as a CEL expression.
- **Untrusted expressions.** The length and the nesting of the text are limited, an evaluation can have a cost limit and can be cancelled, and the cost of every operation is a published number ([Limits](docs/limits.md), [Cost](docs/cost.md)). The library sets no cost limit by default.
- **Errors.** A parse, type or evaluation error is a result with a code of `CelErrorCode`, a stage and a place in the expression, never an exception for a wrong expression ([Error codes](docs/error-codes.md)).

## Conformance

The library runs the conformance suite of [cel-spec](https://github.com/cel-expr/cel-spec) on every platform and every target it supports. The page [Conformance](docs/conformance.md) has the share of the cases that pass with its date, the commit of cel-spec and a link to the report of the run, the skip list with the reasons, and the result of the last run on the master branch of cel-spec with the same data.

## Where the packages come from

Every `.nupkg` package of a release on nuget.org has an attestation of its origin: GitHub Actions built the package from the commit of the release, and the attestation is recorded in the public log of Sigstore. The symbol packages (`.snupkg`) have no attestation. To check a package that you downloaded, use the [GitHub CLI](https://cli.github.com/manual/gh_attestation_verify). For version 2.0.0, download the package `great.parts.cel.2.0.0.nupkg` from nuget.org, then run the check in the folder of the file:

```shell
gh attestation verify great.parts.cel.2.0.0.nupkg --repo great-parts/great-parts-cel --signer-workflow great-parts/great-parts-cel/.github/workflows/release.yml --source-ref refs/tags/v2.0.0 --deny-self-hosted-runners
```

The command checks the file as nuget.org gives it. If `gh` asks you to sign in, run `gh auth login` (or set `GH_TOKEN`) first. The check succeeds only for a package that the workflow `release.yml` of this repository built on a standard GitHub runner from the tag `v2.0.0`. For another version, replace `2.0.0` and `v2.0.0`; the other packages are checked the same way.

## How it is checked

Every pull request and every push to `main` runs [CI](.github/workflows/ci.yml):

| Target | Ubuntu, JIT | macOS, JIT | linux-x64, Native AOT |
| :- | :- | :- | :- |
| `net10.0` | yes | yes | yes |
| `net11.0` | yes | yes | yes |

- In every cell the tests include the conformance tests of [cel-spec](https://github.com/cel-expr/cel-spec) at a pinned commit ([tests/cel-spec/COMMIT](tests/cel-spec/COMMIT)). A case that does not pass is in the [skip list](tests/Great.Parts.Cel.Conformance/CelSpec/skips.md) with its reason; the page [Conformance](docs/conformance.md) shows the result.
- The Native AOT cells publish the test projects, run them and compare the test count with the JIT run.
- The examples of this README and of `docs/` are compiled and run, and their output is compared with the output they show. The links are checked.
- A differential run compares the library with cel-go on generated expressions on every push to `main` and every night, and a run of at least two hours on the commit of a release must pass before the release is published. A release also waits for every difference that is not analysed yet. Differences that have been analysed are listed in [known-differences.md](tests/Great.Parts.Cel.Conformance/Regression/known-differences.md); see [Conformance](docs/conformance.md#differential-run-against-cel-go).

## Packages

| Package | What it gives |
| :- | :- |
| [`Great.Parts.Cel`](https://www.nuget.org/packages/Great.Parts.Cel) | The core: compile and evaluate CEL, the standard library, extension sets, the source generator |
| [`Great.Parts.Cel.Protobuf`](https://www.nuget.org/packages/Great.Parts.Cel.Protobuf) | Messages of protobuf in expressions |
| `Great.Parts.Cel.DependencyInjection` | An environment, a compilation cache and an evaluator as services; the validation of options |
| `Great.Parts.Cel.AspNetCore` | Authorization policies and the validation of requests of minimal API endpoints |
| `Great.Parts.Cel.Policy` | Policies in the CEL policy format |
| `Great.Parts.Cel.Filtering` | AIP-160 filters as CEL expressions |

A package without a link is not published yet; its link appears with its first release.

## Documentation

The documentation of a release is the content of this repository at the tag of the release, for example `v2.0.0`. The pages below and the links in the XML documentation of the packages point to the branch `main`, which can be newer than the version you use: open the page at the tag of your version.

- [Getting Started](docs/getting-started.md): seven scenarios, from a first expression to a web service, each with a runnable sample.
- [Samples](samples/): the projects of the scenarios, `SimpleEntry`, `EnvironmentAndJson`, `AppObject`, `ValidateOnSave`, `AppFunctions`, `WebService`, `CacheAndBulk`, and `FluentValidation`.
- [FluentValidation recipe](docs/recipes/fluentvalidation.md): CEL rules next to the validators of FluentValidation.
- [Standard library](docs/standard-library.md): the functions, operators, macros and type identifiers of version 1, with their errors.
- [Extension sets](docs/extensions.md): the catalog, the versions and every function of every set.
- [API concepts](docs/api-concepts.md): every public type and the CEL concept it stands for.
- [Type mapping](docs/type-mapping.md): how .NET values, collections, objects and `JsonElement` become CEL values and back.
- [Types described at run time](docs/runtime-types.md): object types that an application describes when it builds the environment.
- [Protobuf messages](docs/protobuf.md): the package `Great.Parts.Cel.Protobuf`.
- [Decimal](docs/decimal.md): the type `decimal` and its extension set, on both targets.
- [Environment description in JSON](docs/environment-json.md): the keys and the type records of the JSON form of an environment.
- [Cost](docs/cost.md): the units of every operation.
- [Limits](docs/limits.md): the sizes past which the library reports an error.
- [Error codes](docs/error-codes.md): every code of `CelErrorCode`, its stage and when it occurs.
- [Source generator diagnostics](docs/generator-diagnostics.md): `GPCEL001` to `GPCEL012`, without `GPCEL007`.
- [Application functions](docs/recipes/application-functions.md): how to give the functions of an application the data they need.
- [Time zone database](docs/recipes/time-zone-database.md): how to give an environment the time zones that `getHours('Europe/Moscow')` needs.
- [Conformance](docs/conformance.md): the result of the cel-spec suite.
- [Versioning and compatibility](docs/versioning.md): what is a breaking change and what a minor version may change.
- [Roadmap](ROADMAP.md): the plan of release 2.0, including the packages `Great.Parts.Cel.Policy` and `Great.Parts.Cel.Filtering`.
- [Design](docs/design/): how the library and its checks are built, for contributors.

## How it is built

The maintainer sets the requirements and makes the design decisions; AI agents write the design documents, the code and the tests under his direction; cel-spec and CI verify every change. Every change passes the same checks, whoever wrote it, and every commit of `main` carries the maintainer's sign-off. [CONTRIBUTING.md](CONTRIBUTING.md#how-the-library-is-developed) says more.

## Maintainers

Vladislav Nekrasov ([va-nekrasov](https://github.com/va-nekrasov)).

## Contributing and policies

- [CONTRIBUTING.md](CONTRIBUTING.md): how to build, test and propose a change.
- [GOVERNANCE.md](GOVERNANCE.md): how decisions are made, and who holds which role.
- [SECURITY.md](SECURITY.md): how to report a vulnerability, how a fix is announced, and which versions get fixes.
- [SUPPORT.md](SUPPORT.md): where to ask questions and report bugs.
- [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md): the code of conduct of the project.
- [CHANGELOG.md](CHANGELOG.md): the changes of each version.

## License

[Apache License 2.0](LICENSE). See also [NOTICE](NOTICE).
