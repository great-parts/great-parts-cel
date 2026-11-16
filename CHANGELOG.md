# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
From 1.0.0 the public API follows Semantic Versioning; see [Versioning and compatibility](docs/versioning.md).

A change of the result or of the error of an expression that was valid in an earlier version is listed under `Behavior changes` of its version.

## [Unreleased]

## [1.0.0] - 2026-11-17

The first stable release of the packages `Great.Parts.Cel` and `Great.Parts.Cel.Protobuf`, for `net10.0` and `net11.0`, under JIT and under Native AOT.

### Added

- Compilation and evaluation of CEL expressions. `CelEnvironmentBuilder` collects declarations and builds an immutable `CelEnvironment`; `CelEnvironment.Compile<T>` parses, type-checks and plans an expression into a `CelProgram<T>`, which is evaluated any number of times, from any number of threads, with `CelBindings` and `CelEvalOptions`, and gives its result as the requested .NET type. `CelEnvironment.Evaluate<T>` does both steps in one call, and `CelCompilationCache` keeps the compilations of one environment. An error of any stage is returned as a result with a code of `CelErrorCode`, its stage and its place in the expression. See [API concepts](docs/api-concepts.md) and [Error codes](docs/error-codes.md).
- The complete CEL syntax, type checking by the gradual type checking of the CEL language definition, and the standard library, version 1 (`CelStandardLibrary.V1`): logic, equality, arithmetic and ordering, also across `int`, `uint` and `double`, conversions, strings, lists and maps, `timestamp` and `duration` with their receivers and time zones, the standard macros, and `matches` with the syntax and the semantics of RE2, found in time linear in the length of the string. See [Standard library](docs/standard-library.md).
- Extension sets, each added with an explicit version by `CelEnvironmentBuilder.AddExtension`: `strings` 5, `math` 3, `lists` 4, `sets` 0, `encoders` 1, `bindings` 1, `two-var-comprehensions` 0, `network` 1, `optional` 2 with the optional syntax `e.?f`, `e[?k]`, `[?e]`, `{?k: v}` and `T{?f: v}`, and `protos` 0. An application builds sets of its own with `CelExtension.Create`. `Build` checks the sets and reports every conflict at once. See [Extension sets](docs/extensions.md).
- The type `decimal`, only on .NET 11: an IEEE 754 decimal128 number of 34 significant digits, with `CelType.Decimal`, `CelValue.FromDecimal`, the extension set `decimal` version 1 and, after `CelEnvironmentBuilder.EnableDecimalLiterals()`, the literals `12.34m` and `100m`. See [Decimal](docs/decimal.md).
- Data of the application as variables, bindings and results: numbers, `string`, `Guid`, enumerations, bytes, time (`DateTimeOffset`, `DateTime` of the kind `Utc`, `TimeSpan`, `CelTimestamp`, `CelDuration`), `Nullable<>`, arrays, lists, sets, dictionaries, `JsonElement`, `object`, `CelValue`, `CelList` and `CelMap`, converted without reflection. See [Type mapping](docs/type-mapping.md).
- Objects of the application. A source generator reads a type context — a `partial` class derived from `CelTypeContext` with `[CelObject]` attributes, with `[CelName]`, `[CelIgnore]` and the naming policies `CelNamingPolicy.CamelCase`, `AsIs` and `SnakeCaseLower` — and writes the descriptions of the listed types and of the types their members reach, so that a field is read by a direct access under JIT and under Native AOT; the diagnostics `GPCEL001` to `GPCEL009` say what it cannot describe. Types can also be described at run time with `CelObjectTypeBuilder` and supplied by a `CelTypeProvider`. See [Source generator diagnostics](docs/generator-diagnostics.md) and [Types described at run time](docs/runtime-types.md).
- Messages of protobuf, in the package `Great.Parts.Cel.Protobuf`: `CelProtobufTypesBuilder` takes the files that `protoc` generated for C# and the typed extensions of proto2, and the provider it builds gives an environment the messages, their fields, object literals, the equality of messages, `google.protobuf.Any` with its unpacking and the well-known types of `google/protobuf`. See [Protobuf messages](docs/protobuf.md).
- Functions of the application: `CelEnvironmentBuilder.AddFunction` with a .NET delegate or a `CelFunctionDecl` of overloads, `AddFunctionImplementation`, and a cost function (`CelOverloadDecl.WithCost`). See [Application functions](docs/recipes/application-functions.md).
- An environment described in JSON, read by `CelEnvironmentJson.Apply`: variables, functions, extension sets and object types of the application, with the place of every error in the JSON text. See [Environment description in JSON](docs/environment-json.md).
- Time zones for the receivers of `timestamp` and `duration`: `CelTimeZoneDatabase.Create` reads the TZif files of the application, and `CelEnvironmentBuilder.UseTimeZones` gives them to an environment. The library reads no files and no data of the operating system. See [Time zone database](docs/recipes/time-zone-database.md).
- Protection from untrusted expressions: limits of the length and of the nesting of an expression, a cost limit for an evaluation (`CelEvalOptions.CostLimit`) with a published unit for every operation, and cancellation with a `CancellationToken`. See [Limits](docs/limits.md) and [Cost](docs/cost.md).
- The result of the cel-spec conformance suite on every platform of the matrix, with the skip list and the result on the master branch of cel-spec. See [Conformance](docs/conformance.md).
- An attestation of origin for each `.nupkg` package on nuget.org, and the command that checks it. See the [README](README.md#where-the-packages-come-from).

### Behavior changes

None: 1.0.0 is the first release, so there is no earlier version whose results or errors change. From this version on, a change of the result or of the error of an expression that was valid before is listed here, in the entry of its version.

[Unreleased]: https://github.com/great-parts/great-parts-cel/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/great-parts/great-parts-cel/releases/tag/v1.0.0
