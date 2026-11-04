# Facade: from the builder to the result

This page describes how the public types of `Great.Parts.Cel` drive the internal stages, and the internal entry points that the tests and the conformance runner use:

```text
new CelEnvironmentBuilder(CelStandardLibrary.V1)   the builder of an environment
    .AddVariable<long>("x")                         x has the CEL type int
    .UseContainer("acme.sales")                     the name resolution container
    .Build()                                        CelBuildResult holding the CelEnvironment env
env.Compile<long>("x + 1")                          CelCompileResult<long> holding the CelProgram<long> program
program.Evaluate(bindings, options, token)          bindings: new CelBindings().Add("x", 41L)
                                                    CelEvalResult<long> with the value 42
env.Evaluate<long>("x + 1", bindings)               the same in one call
new CelCompilationCache(env, 1000, 100_000)         the same through a bounded cache
```

## Stages

| Stage | Code | Input | Output | Errors |
| :- | :- | :- | :- | :- |
| collect | `CelEnvironmentBuilder` | calls of the application or of `CelEnvironmentJson.Apply` | a set of declarations, the container and the standard library version ([declarations](declarations.md)) | exceptions for violated call conditions only |
| build | `CelEnvironmentBuilder.Build` | the declarations | an immutable `CelEnvironment` with the declaration index of [checking](checking.md) | stage `Build`, [declarations](declarations.md) |
| derive | `ConversionRules` in `Values` | `T` and `CelCompileOptions.ExpectedType` | the expected CEL type | `ArgumentException`, see [Expected type](#expected-type) |
| parse | `Syntax`, see [syntax](syntax.md) | the text | `ParsedExpression` | stage `Parse` |
| check | `Checking` | the parsed expression, the environment, the expected type | `CheckedExpression` | stage `Check` |
| plan | `Evaluation` | the checked expression | an immutable plan, held by `CelProgram<T>` | none in this part |
| evaluate | `Evaluation` | the plan, the bindings, the cost limit, the token | a `CelValue`, or an error value | stage `Evaluate`; `OperationCanceledException` |
| convert | `ConversionRules.Result.cs` | the value of the root | `T` | stage `Evaluate` |

`Compile<T>` is derive, parse, check, plan; each stage runs only when the previous one found no errors. The derive step comes first and throws before any text is read, so the same `T` and options throw for every text.

**Evaluation.** `CelProgram<T>.Evaluate(bindings, options, cancellationToken)` passes the dictionary inside `CelBindings`, `options?.CostLimit` and the token to `Plan.Evaluate(bindings, costLimit, cancellationToken)` ([evaluation](evaluation.md)); `null` options and a `null` limit mean no meter. An `OperationCanceledException` of the token passes through; an error value at the root is the single error of the result, any other value is converted to `T`. The program keeps the start of the source location and maps the evaluation error as described below. A program is immutable: any number of threads evaluate it at once.

`CelBindings.Add<T>` converts a value by its static type `T` with the input rows of `ConversionRules` ([values](values.md)); a `CelValue` is taken as it is. `AddVariable<T>` takes the CEL type of the same rows. The input types of this part are those rows: the integers, `double`, `float`, `Half`, `bool`, `string`, `Guid`, enumerations, `byte[]`, `ReadOnlyMemory<byte>`, `Nullable<X>` of these value types, `CelValue`, `CelList`, `CelMap` and `object`. `CelBindings.Add<T>` with another `T` throws `NotImplementedException` until its part; `AddVariable<T>` with it is `TypeNotDeclared` at `Build` ([declarations](declarations.md)).

## Errors

Every error is a `CelError`, created through its internal constructor:

```text
internal CelError(CelErrorCode code, CelErrorStage stage, string message,
                  CelSourceLocation location, CelSourceLocation? sourceLocation, Exception? exception)
```

- The message is English text by the template of the error code table; errors of evaluation get their text when they are read ([evaluation](evaluation.md)).
- `location` is the position in the expression: the offset in UTF-16 code units, the line and the column from 1, computed from the offset of a node and the line table of the text (`LineTable`, see [syntax](syntax.md)). Build errors pass `default`.
- `sourceLocation` is the position in a source document: the JSON text of an environment description ([configuration](configuration.md)), or the document that holds the expression, see [Source location](#source-location-of-an-expression).
- Compilation returns every error of the stage that failed, in the order of that stage: by offset, errors at one offset in the order found — for parsing with the error that stopped it last ([syntax](syntax.md)), for checking as [checking](checking.md), "The type `error` and the list of errors", says; evaluation returns one error, the error value that reached the root. Build errors are ordered by code and then by message ([declarations](declarations.md)).

**Compilation boundary.** `CelEnvironment` runs parsing and checking each inside one handler: an unexpected exception other than `OutOfMemoryException` becomes `InternalError` with the stage of that step and the original exception, after the errors found before it. For parsing the handler is in `CelEnvironment.Parse` ([syntax](syntax.md)), for checking in `CelEnvironment.Check` ([checking](checking.md), "The stack and the compilation boundary"); `Compile<T>`, the one-step call and the internal entry points share them. Planning has no handler of its own: a plan is built only from a checked expression, and planning has no error.

### Source location of an expression

`CelCompileOptions.SourceLocation` gives the name of the document and the position at which the expression starts in it; a value without a source name, such as `default(CelSourceLocation)`, throws `ArgumentException`. When it is set, every error of the compilation and of each evaluation of its program has, besides `Location`, `SourceLocation` in the document: the source name of the start; the offset of the start plus the offset in the expression; the line of the start plus the line in the expression minus 1; on the first line of the expression the column of the start plus the column minus 1, on the other lines the column itself. A sum that would pass `int.MaxValue` stays at `int.MaxValue`: the start is any position the application gives, and its offset, line and column can be as large as `Int32` allows, so the mapping neither throws nor wraps around. The text is assumed to stand in the document character for character. The facade creates each mapped error anew with the same code, stage, message, location and exception.

## Expected type

The expected CEL type comes from `T` by the expected-type table of the type mapping page and is passed to the checker ([checking](checking.md), "Expected type"). The rows of this part:

| `T` | Expected type |
| :- | :- |
| `bool`; `long`, `int`; `ulong`, `uint`; `double`; `string`; `ReadOnlyMemory<byte>`, `byte[]` | `bool`; `int`; `uint`; `double`; `string`; `bytes` |
| `IReadOnlyList<X>`; `IReadOnlyDictionary<XK, XV>` | `list(E')`; `map(K', V')`, from `X`, `XK`, `XV` by this table, one step deep (below) |
| `CelType` | `type` |
| `Nullable<X>` | `wrapper(int)` for `long?`, `int?`; `wrapper(uint)` for `ulong?`, `uint?`; `wrapper(double)`, `wrapper(bool)`, `wrapper(bytes)` for `double?`, `bool?`, `ReadOnlyMemory<byte>?` |
| `CelValue`, `object` | `dyn` |
| `CelList`; `CelMap` | `list(dyn)`; `map(dyn, dyn)` |

`CelCompileOptions.ExpectedType`, when set, replaces the type from `T`. It must be compatible with `T`: `dyn` and a type parameter are compatible with every `T` of the table; another type is compatible when the result table below has `T` for it, for `list` and `map` when each parameter is compatible with `X`, `XK`, `XV`, and `wrapper(P)` when `T` takes `null` and its value part is compatible with `P`. An incompatible pair throws `ArgumentException` naming both. `T` outside both tables — `short`, `Guid`, a class or a structure of the application — throws `ArgumentException`.

**Typed nesting is one step.** `X`, `XK` and `XV` are types of the table other than `IReadOnlyList<…>` and `IReadOnlyDictionary<…>` themselves: in this part `X` and `XV` are `bool`, `long`, `int`, `ulong`, `uint`, `double`, `string`, `ReadOnlyMemory<byte>`, `byte[]`, the `Nullable<X>` of the table, `CelValue`, `object`, `CelList`, `CelMap` and `CelType`, and `XK` is `long`, `int`, `ulong`, `uint`, `bool`, `string`, `object` or `CelValue`; later parts add their scalar types by the same rule. Under Native AOT a closed generic type known only at run time cannot be built — `MakeGenericType` requires dynamic code — so the rows are a closed list of instantiations, and a list or a map that is deeper goes through `CelList`, `CelMap`, `CelValue` or `object`, at any depth: `IReadOnlyList<CelList>` for `list(list(int))`. A deeper typed `T`, such as `IReadOnlyList<IReadOnlyList<long>>`, is `T` outside the table, `ArgumentException`; the same for JIT, so the result does not depend on the mode. `T` of later parts — `CelTimestamp`, `DateTimeOffset`, `CelDuration`, `TimeSpan` and their `Nullable<>`, `CelOptional<X>`, `Decimal128`, `System.Decimal` — throws `NotImplementedException` until its part, like the expected types `timestamp`, `duration`, `optional_type` and `decimal`.

`CelProgram<T>.ResultType` is `CheckedExpression.ResultType`.

## Result conversion

The value of the root is converted to `T` by the result rows of the type mapping table, in `ConversionRules.Result.cs`, after the evaluation and without cost. The rows of this part:

| CEL type of the value | `T` |
| :- | :- |
| `bool`, `int`, `uint`, `double`, `string`, `bytes` | `bool`; `long`, `int` with a range check; `ulong`, `uint` with a range check; `double`; `string`; `ReadOnlyMemory<byte>` without a copy, `byte[]` as a copy |
| `null_type` | `null` for a reference type, `Nullable<X>` and `object`; the `null` value for `CelValue` |
| `list(E)` | `IReadOnlyList<X>`, `E` → `X` by this table, one step deep ([Expected type](#expected-type)); `CelList` |
| `map(K, V)` | `IReadOnlyDictionary<XK, XV>`, one step deep; `CelMap` |
| `type` | `CelType` |
| any non-`null` value with `T = Nullable<X>` | by the row of its type with `T = X` |
| any | `CelValue`, as it is; `object`: `long`, `ulong`, `double`, `bool`, `string`, `byte[]`, `null`, `IReadOnlyList<object?>`, `IReadOnlyDictionary<object, object?>`, `CelType` |

A value whose type has no row for `T`, or that a row does not convert, is `ResultConversion` at the position of the root; `{celType}` is the name of the type of the value, and `{clrType}` is written by `Type.ToString()`. There is no conversion between numeric types. A list or a map is a lazy view when no element, key or value can fail its conversion, and otherwise an immutable copy made at the end of the evaluation, whose first failing element is `ResultConversion`. The decision is static, by the pairs of the element, key and value types of `CelProgram<T>.ResultType` — not of the expected type — and the parameters of `T`: `int` → `long`, `uint` → `ulong`, `string` → `string`, `dyn` → `object`, `dyn` → `CelValue` and the views of these cannot fail; `int` → `int`, `uint` → `uint` and `dyn` → any other `X` can. So `x` of type `list(dyn)` compiled as `IReadOnlyList<long>` (the expected type `list(int)` accepts `list(dyn)`) is a copy, and a string element is `ResultConversion`. A static type is no guarantee of the value ([values](values.md), "Fits a type"): `x + y` with `x` of type `list(int)` bound to `[1]` and `y` of type `dyn` bound to `['a']` has the type `list(int)` and the value `[1, 'a']`. So before a lazy view the facade checks the value of the root with `ValueTypeRules.Fits` without a meter — one pass, without allocation; a mismatch is `ResultConversion`, whose `{celType}` is the type that `Fits` returns (`list(string)`). Like the copy, the check is not charged. The check is against the expected type that the table of [Expected type](#expected-type) derives from `T` — not against `ResultType`, and not against `CelCompileOptions.ExpectedType`: that type names exactly what the view reads by a static type — a position that the view gives as `object` or `CelValue` is `dyn`, one that it gives as `CelList` or `CelMap` is `list(dyn)` or `map(dyn, dyn)`, whose kind only is checked. One addition: a position that the view gives as `string`, `byte[]`, `CelList`, `CelMap` or `CelType` also takes `null`, because the `null_type` row above reads `null` there as the `null` reference of a reference type, and the copy does the same. A position of `ReadOnlyMemory<byte>`, `long`, `int` or another value type does not take `null`; `long?`, `int?` and the other `Nullable<X>` take it as `wrapper(P)` does. The `{celType}` of a mismatch names the checked type without that addition (`list(int)`, never a type with `null` in it). So the check refuses what a copy would refuse and takes what a copy would convert, and the choice between a view and a copy never changes the outcome. With the value `[1, 'a']` of type `list(int)`: `IReadOnlyList<long>` is checked against `list(int)` and is `ResultConversion`; `IReadOnlyList<object>` and `IReadOnlyList<CelValue>` against `list(dyn)` and give the value; `IReadOnlyList<CelList>` for `list(list(int))` is checked against `list(list(dyn))`. With the value `['a', null]`: `IReadOnlyList<string>` gives it both for `['a'] + dyn([null])`, a view, and for `dyn(['a', null])`, a copy, and the second element is `null`; `IReadOnlyList<ReadOnlyMemory<byte>>` is `ResultConversion` both for `[b'a'] + dyn([null])` and for `dyn([b'a', null])`. The same holds for a `null` value of a map and for a `null` element of `IReadOnlyList<CelList>`, `IReadOnlyList<CelMap>` and `IReadOnlyList<CelType>`. At the root itself nothing is checked: `(['a'] + dyn([null]))[1]` has the static type `string` and the value `null`, and `T = string` gives `null`. `object`, `CelValue`, `CelList` and `CelMap` as `T` are no checked views: they rely on no static type. A view of a later part over a collection of the application, whose element reads run application code, is copied, never viewed lazily: an exception or an error value of such a read after the evaluation would escape the result.

## One step and the compilation cache

`CelEnvironment.Evaluate<T>(expression, bindings, compileOptions, evalOptions, cancellationToken)` is `Compile<T>` followed by `CelProgram<T>.Evaluate` — the same methods, not a copy of their code — so its result and errors are those of the two calls by construction:

1. check the arguments for `null`; `cancellationToken.ThrowIfCancellationRequested()`;
2. `Compile<T>(expression, compileOptions)`; its exceptions pass through, its errors are the result;
3. `cancellationToken.ThrowIfCancellationRequested()`;
4. `program.Evaluate(bindings, evalOptions, cancellationToken)`.

Compilation is not charged, so a cost limit applies to step 4 only.

`CelCompilationCache(environment, maxEntryCount, maxTotalTextLength)` holds compile results of one environment. Both capacities are required and at least 1; the second is the sum of `string.Length` of the kept texts.

- **Key** — the text (ordinal), `typeof(T)`, and the values of the compile options: `ExpectedType` by `CelType.Equals`, `SourceLocation` by value; `null` options equal options with both properties `null`. A hit returns the same `CelCompileResult<T>` instance, a failed one included: failed compilations are cached too.
- **`GetOrCompile<T>`** — a hit, or `environment.Compile<T>` and an insertion. An exception of `Compile<T>` (`ArgumentException`) passes through and inserts nothing. A text longer than `maxTotalTextLength` is compiled and returned, not kept.
- **`Evaluate<T>`** — the four steps of the one-step call with `GetOrCompile<T>` in step 2.
- **Eviction** happens in the insertion, on the calling thread: while the count or the total length is over its capacity, an entry other than the new one is removed. Which entry is the developer's choice — an approximation of least recently used, such as the clock algorithm, is recommended — under three conditions: amortized constant work per insertion, no thread or timer, and no effect on results: an evicted text is compiled again with the same result.
- **Threads.** A lookup takes no lock (`ConcurrentDictionary` or an equal structure); insertions and evictions take one lock of the cache. Two threads that miss the same key may both compile; the first insertion wins, and both return the entry in the cache.
- **Recommended capacities** come from the calibration of stage 0, item 6: the bytes that a cache entry keeps, measured as `a + b · L` (entry count, text length L) by `GC.GetTotalMemory(true)` before and after filling a cache with programs of a fixed set of texts from 20 to 2 000 code units; the recommendation keeps a full cache within 64 MiB — `maxEntryCount` ≤ 32 MiB / a and `maxTotalTextLength` ≤ 32 MiB / b, rounded down to a number with one significant digit. The task that measures puts a, b and the recommendation into its report; the XML documentation of the constructor and the limits page state the recommendation.

## Environment members

- `CelEnvironmentBuilder.UseContainer(container)` sets the container that [checking](checking.md) resolves names in; `ToBuilder()` returns a builder with the declarations of the environment ([declarations](declarations.md), "The environment and `ToBuilder`").
- `CelEvalOptions` holds `CostLimit` (`ulong?`, `null` — no limit); the token is a separate argument of every evaluating member. `CelCompileOptions` and `CelEvalOptions` are immutable after initialization.

## Internal entry points

The assembly gives `InternalsVisibleTo` to `Great.Parts.Cel.Tests`, `Great.Parts.Cel.Conformance` and `Great.Parts.Cel.Properties` only. Three internal members of `CelEnvironment` give the stages one by one; a fourth, static one is the handler of the compilation boundary of parsing:

```text
CelEnvironment, internal members:
    ParseResult Parse(string expression, SyntaxOptions? syntax = null)
    CheckResult Check(ParsedExpression parsed, CelType? expectedType = null)
    CelEvalResult<CelValue> EvaluateWithoutTypeCheck(ParsedExpression parsed, CelBindings bindings)
    static ParseResult ParseWithinBoundary(Parser parser, Func<Parser, ParseResult> parse)

internal sealed record SyntaxOptions(bool OptionalSyntax, MacroTable Macros)   // Great.Parts.Cel.Syntax
internal sealed class ParseResult   { bool IsSuccess; ParsedExpression? Expression; ImmutableArray<CelError> Errors }
internal sealed class CheckResult   { bool IsSuccess; CheckedExpression? Expression; ImmutableArray<CelError> Errors }
```

- **Parse only** is the parse step of `Compile<T>`, the boundary included; `syntax` replaces the syntax options of the environment for one call, for the tests and the conformance runner ([syntax](syntax.md#syntax-options)).
- **Boundary of parsing.** `ParseWithinBoundary` is the handler of the "Compilation boundary" above, and `Parse` is its only caller in the library: it passes `new Parser(expression, syntax ?? SyntaxOptions.Standard)` and the step `parser => parser.Run()`, so `Compile<T>` and `Parse` share it. The step is a parameter so that a test can make it throw. The method returns the result of the step. When the step throws an exception other than `OutOfMemoryException`, it returns the errors that `Parser.Interrupted` gives: the errors the parser found before the exception, the error that stopped parsing if there is one, and `InternalError` last, with the exception in `CelError.Exception`, at the offset of the token the parser was at. Its line and column come from the line table of the text; before the line table exists, as before the first token, the position is offset 0, line 1, column 1. `OutOfMemoryException`, `InsufficientMemoryException` included, is not caught. The method checks no arguments.
- **Check only** is the check step of `Compile<T>` with the expected type that `Compile<T>` derives, `dyn` without one; `CheckedExpression.ResultType` is what `CelProgram<T>.ResultType` reports for the same text.
- **Evaluation without type checking** plans the parsed tree without a checked expression ([evaluation](evaluation.md)) and evaluates it without a cost limit or a token; the value is returned as it is, or one error of the stage `Evaluate`.

All three throw `ArgumentNullException` for a `null` argument and can be called from any number of threads at once. Later parts add optional parameters at the end of these signatures.

The internal property `CelProgram<T>.Plan` gives the plan of a program, for the internal steps of evaluation ([evaluation](evaluation.md), "Frame"): `Plan.Evaluate`, through which the example of `InternalError` at evaluation passes bindings whose lookup throws, and `Plan.CreateFrame`, `Plan.Run` and `Frame.ChargedUnits`, which the property of cost and memory calls.

## Not in this part

Type sources (`AddTypes`), extension sets (`AddExtension`), time zones (`UseTimeZones`), the `decimal` literal flag, the rest of the environment description in JSON; the input and result rows of objects, collections of the application, `JsonElement`, time, `optional` and `decimal`. Each comes with its part; until then its members throw `NotImplementedException`.

Related pages: [declarations](declarations.md), [checking](checking.md), [evaluation](evaluation.md), [values](values.md), [types](types.md), [core](core.md).
