# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
From 1.0.0 the public API follows Semantic Versioning; see [Versioning and compatibility](docs/versioning.md).

A change of the result or of the error of an expression that was valid in an earlier version is listed under `Behavior changes` of its version.

A fix of a vulnerability is listed under `Security` of its version, with the GHSA identifier of its advisory and the CVE identifier when one is assigned; see [SECURITY.md](SECURITY.md#where-a-fixed-vulnerability-is-announced).

## [Unreleased]

### Added

- `CelDuration(long seconds, int nanoseconds)`, `CelDuration.Seconds` and `CelDuration.Nanoseconds`: a duration is whole seconds and nanoseconds, as `google.protobuf.Duration` keeps it.
- `CelExpression`, `CelAnonymousData`, `CelValidationResult`, `CelSyntax`, `CelFunctionOptions` and `CelSourceSpan`: the simple entry that evaluates an expression on an anonymous object or a dictionary, the validation of an expression without a program, the helpers for the lexical syntax of CEL, the options of a function declared from a delegate and the fragment of an expression that an error refers to.
- In `CelEnvironmentBuilder`: a constructor without arguments, `UseInput<TInput>()`, `SetDefaultCostLimit(ulong)`, overloads of `AddFunction` and `AddMethod` with `CelFunctionOptions`, and `AddMethod` for methods with up to three parameters.
- In `CelEnvironment`: `Create`, `Validate`, `EvaluateInput<T>` on an instance of the input type, and the read-only description of the declarations: `Variables`, `Functions`, `ObjectTypes`, `EnumTypes`, `InputType` and `DefaultCostLimit`.
- `EvaluateInput` on an instance of the input type in `CelProgram<T>` and `CelCompilationCache`; `CelCompilationCache` also has `Count`, `Clear`, `TryRemove` and the default capacities. `CelEvalResult<T>.Cost` is the cost an evaluation spent.
- `CelBindings` has an indexer, `Clear` and enumeration, so that `new CelBindings { { "order", order } }` works.
- Every error has the fragment of the expression it refers to, from its place to the end of its node, and the parts that its code names: `CelError.Span`, `Subject`, `ExpectedType` and `ActualType`; the page [Error codes](docs/error-codes.md#parts-of-an-error) lists them for each code.
- `CelError.ToDisplayString`; `CelOverloadDecl.Description` and `WithDescription`; `ToString` of `CelSourceLocation`, `CelError` and `CelValue`; `CelErrorCode.InputConflict`.
- `CelTimeZoneDatabase.FromSystem()`: the time zone database of the operating system, requested explicitly. It finds a zone only by its IANA identifier, after a check of the form of the name (at most 64 characters, parts of Latin letters, digits, `_`, `-` and `+`), and a receiver of time with a zone costs a constant number of units more in an environment with it; see [Limits](docs/limits.md#time-zones-of-the-operating-system) and [Cost](docs/cost.md#time-zones-of-the-operating-system).
- `CelException.InnerException` is the exception of the first error that has one, such as the exception of a function of the application.
- The `decimal` extension set, `EnableDecimalLiterals`, `CelType.Decimal`, `CelTypeKind.Decimal`, `CelValueKind.Decimal`, `CelValue.TryGetDecimal`, `CelValue.FromDecimal` and `CelObjectType.FromDecimal` on `net10.0` too, with a `System.Decimal`: 96 bits of coefficient and the exponents -28 to 0, so `string(1e2m)` is `"100"` there and `"1E+2"` on `net11.0`; the rules are one code for both targets, and the source generator describes a `System.Decimal` member on both. `System.Decimal.Parse`, `TryParse`, `ToString` and `TryFormat` join the banned APIs of the package. See [Decimal](docs/decimal.md#same-and-different-on-the-two-targets).
- The source generator registers the anonymous objects that calls of `CelExpression.Evaluate` pass, so the simple entry reads them without reflection and under Native AOT; the diagnostics `GPCEL010` (the static type of the data is not an anonymous type or a supported dictionary) and `GPCEL011` (a property type the simple entry cannot read); see [Source generator diagnostics](docs/generator-diagnostics.md#gpcel010) and [Type mapping](docs/type-mapping.md#data-of-the-simple-entry). Under C# 11 and earlier that generator creates no file and reports `GPCEL008` once for the compilation, at the first call.
- `CelObjectType<TInstance>`: the description of an object type that reads an instance passed by reference (`GetField(in TInstance, int)`, `FieldReadCost(in TInstance, int)`). An input that is a structure, `program.EvaluateInput(order)` with `UseInput<Order>()`, is read without boxing: the call allocates the frame of the evaluation and nothing for the structure. The descriptions that the source generator creates and the types described with `CelObjectTypeBuilder<TCarrier>` derive from it.

### Changed

- Reading `CelEvalResult<T>.Value`, `CelBuildResult.Environment` or `CelCompileResult<T>.Program` of an unsuccessful result throws `CelException` with all errors, where it threw `InvalidOperationException`.
- `default(CelEvalResult<T>)`, a structure that no call returned, is not successful and has no errors; reading its `Value`, `GetValueOrThrow()` or `Cost` throws `InvalidOperationException`. `CelObjectType.CreateInstance` throws `InvalidOperationException` by default, where it threw `NotSupportedException`.
- On `net11.0` (source-breaking) the members of `Decimal128` have new names, since `FromDecimal(decimal)` and `TryGetDecimal(out decimal)` take the old ones on both targets: `CelValue.FromDecimal(Decimal128)` is `CelValue.FromDecimal128`, `CelValue.TryGetDecimal(out Decimal128)` is `CelValue.TryGetDecimal128`, and `CelObjectType.FromDecimal(int, Decimal128)` is `CelObjectType.FromDecimal128`.
- `GPCEL007` is not reported any more (a removed diagnostic): a `System.Decimal` member is a field of the type `decimal` on both targets.

### Behavior changes

- On `net11.0`, with no compile error: code of 1.x that calls `TryGetDecimal(out var value)` still compiles, and `value` is now a `decimal`; the method returns `false` for a value that `System.Decimal` does not hold exactly (more than 96 bits, an exponent below -28 or a value beyond its range). Read such a value with `TryGetDecimal128`.
- The range of `duration` is that of `google.protobuf.Duration`, from `-315576000000.999999999s` to `315576000000.999999999s` (about ±10 000 years), as the language definition says; it was a 64-bit number of nanoseconds, about ±292 years. A longer duration is now a value where it was `Overflow` (a sum or a difference of durations, the difference of two timestamps) or `ConversionRange` (`duration(string)`, a `TimeSpan` of the application, a `google.protobuf.Duration` field of a message). `duration('9223372036854775808ns')` is `9223372036.854775808s`, and `duration('9223372036854775808ns9223372036854775808ns')` is `18446744073.709551616s` where it was `0s`. `int(duration)` is `ConversionRange` when the nanoseconds do not fit an `int`.
- `CelDuration.TotalNanoseconds` throws `InvalidOperationException` for a duration whose nanoseconds do not fit a `long`; read `Seconds` and `Nanoseconds` instead.

## [1.0.1] - 2026-11-17

Only the properties of the packages `Great.Parts.Cel` and `Great.Parts.Cel.Protobuf` change; the code of the libraries is the code of 1.0.0.

### Added

- A README in each package, shown on nuget.org: what the package is, how to install it, a short example and links to the documentation of the release.

### Behavior changes

None: the assemblies of 1.0.1 are built from the same code as those of 1.0.0.

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

[Unreleased]: https://github.com/great-parts/great-parts-cel/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/great-parts/great-parts-cel/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/great-parts/great-parts-cel/releases/tag/v1.0.0
