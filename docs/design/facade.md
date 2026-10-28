# Facade: from the builder to the result

This page describes how the public types of `Great.Parts.Cel` drive the internal stages, and the internal entry points that the tests and the conformance runner use. It covers the path of the walking skeleton:

```text
new CelEnvironmentBuilder(CelStandardLibrary.V1)   the builder of an environment
    .AddVariable<long>("x")                         x has the CEL type int
    .Build()                                        CelBuildResult holding the CelEnvironment env
env.Compile<long>("x + 1")                          CelCompileResult<long> holding the CelProgram<long> program
program.Evaluate(bindings)                          bindings: new CelBindings().Add("x", 41L)
                                                    CelEvalResult<long> with the value 42
```

## Stages

| Stage | Code | Input | Output | Errors |
| :- | :- | :- | :- | :- |
| collect | `CelEnvironmentBuilder` | calls of the application or of `CelEnvironmentJson.Apply` | a set of declarations and the standard library version | exceptions for violated call conditions only |
| build | `CelEnvironmentBuilder.Build` | the declarations | an immutable `CelEnvironment`: the declaration tables of the standard library version from `Library`, the variables by name, the index "function name and argument count → overloads" — `Checking.DeclarationIndex`, which `Build` creates from the table of `Library` and the declarations of the application ([checking](checking.md)) | stage `Build` |
| parse | `Syntax`, see [syntax](syntax.md) | the text | `ParsedExpression`: syntax tree, positions, line table | stage `Parse` |
| check | `Checking` | the parsed expression and the environment | `CheckedExpression`: the tree, the reference map with overload candidates, the type map and the type of the expression | stage `Check` |
| plan | `Evaluation` | the checked expression | an immutable tree of plan nodes, held by `CelProgram<T>` with the result type and the line table | none in the walking skeleton |
| evaluate | `Evaluation` | the plan and the bindings | a `CelValue`, or an error value | stage `Evaluate` |
| convert | value rules in `Values` | the value of the root | `T` | stage `Evaluate` |

`Compile<T>` is parse, then check, then plan. Each stage runs only when the previous one found no errors. Evaluation creates a new frame for each call, so a program is evaluated from any number of threads at once; the frame and the plan are described in `evaluation.md`.

`CelBindings.Add<T>` converts a value by its static type `T` through the input rows of the type mapping table, using the conversion rules of `Values` (see [core](core.md)); a `CelValue` is taken as it is. `Evaluation` sees neither the facade nor `Library`, so `CelBindings` hands its values to it through an internal member as an `IReadOnlyDictionary<string, CelValue>` ([evaluation](evaluation.md)). The program reads a variable from the bindings by name when the expression reads it; a variable without a value is the error `Unbound` at that moment.

## Errors

Every error is a `CelError`, created through its internal constructor:

```text
internal CelError(CelErrorCode code, CelErrorStage stage, string message,
                  CelSourceLocation location, CelSourceLocation? sourceLocation, Exception? exception)
```

- The message is English text by the template of the error code table.
- `location` is the position in the expression: the offset in UTF-16 code units, the line and the column from 1. The parser, the checker and the evaluator compute it from the offset of a node and the line table of the text (`LineTable` in `Diagnostics`, see [syntax](syntax.md)), and create it with the internal constructor `CelSourceLocation(int offset, int line, int column)`, which leaves the source name empty. Build errors have no position in an expression and pass `default`.
- `sourceLocation` is the position in a source document: the JSON text of an environment description (see [environment description in JSON](configuration.md)). Positions of an expression inside a document arrive with the compile options and are `null` in the walking skeleton.
- Compilation returns every error of the stage that failed, in the order in which they were found; a text with parse errors is not checked.
- Evaluation returns one error. An error inside evaluation is a value of the kind `Error` that travels like any value; `_||_` can absorb it. When it reaches the root, it becomes the single error of `CelEvalResult<T>`.

**Compilation boundary.** `CelEnvironment` runs parsing and checking each inside one handler: an unexpected exception other than `OutOfMemoryException` becomes the error `InternalError` with the stage of that step and the original exception. The handler for parsing arrives with the complete grammar, the handler for checking with complete type checking; until then such an exception escapes `Compile<T>`.

## Result conversion

The value of the root is converted to `T` by the result rows of the type mapping table, in `ConversionRules.Result.cs`. The walking skeleton supports these `T`:

| CEL type of the value | `T` |
| :- | :- |
| `bool` | `bool` |
| `int` | `long`; `int` with a range check |
| `uint` | `ulong`; `uint` with a range check |
| `double` | `double` |
| `string` | `string` |
| `bytes` | `ReadOnlyMemory<byte>` without a copy; `byte[]` as a copy |
| `null_type` | `null` for `string` and `byte[]`; the `null` value for `CelValue` |
| any | `CelValue`, as it is |

A value whose type has no row for `T`, or that a row does not convert — such as `uint` for `T = long` or an `int` outside the range of `Int32` for `T = int` — is the error `ResultConversion` at the position of the root node. There is no conversion between numeric types.

In the walking skeleton the expected type that `T` implies is not checked at compilation; a mismatch shows up only as `ResultConversion`. That check and the explicit expected type of the compile options arrive with complete type checking; the other result rows arrive with the kinds of values they convert. `Compile<T>` with a `T` outside the table above throws `NotImplementedException`, like the other members of the public API that are not implemented yet.

## Internal entry points

The assembly gives `InternalsVisibleTo` to the test projects `Great.Parts.Cel.Tests`, `Great.Parts.Cel.Conformance` and `Great.Parts.Cel.Properties`, and to no other assembly. Three internal members of `CelEnvironment` give the stages one by one:

```text
namespace Great.Parts.Cel
CelEnvironment, internal members:
    ParseResult Parse(string expression)
    CheckResult Check(ParsedExpression parsed)
    CelEvalResult<CelValue> EvaluateWithoutTypeCheck(ParsedExpression parsed, CelBindings bindings)

namespace Great.Parts.Cel.Syntax
internal sealed class ParseResult
    bool IsSuccess                       // [MemberNotNullWhen(true, nameof(Expression))]
    ParsedExpression? Expression
    ImmutableArray<CelError> Errors      // stage Parse

namespace Great.Parts.Cel.Checking
internal sealed class CheckResult
    bool IsSuccess                       // [MemberNotNullWhen(true, nameof(Expression))]
    CheckedExpression? Expression        // Expression.ResultType: the CEL type of the expression
    ImmutableArray<CelError> Errors      // stage Check
```

- **Parse only.** `Parse` is the parse step of `Compile<T>`: it uses the syntax of the environment, so a test of the parser sees what compilation sees. The parsed expression does not refer to the environment.
- **Check only.** `Check` is the check step of `Compile<T>`. `CheckedExpression.ResultType` is the type that `CelProgram<T>.ResultType` reports for the same text; the conformance runner compares it with the type a test case expects.
- **Evaluation without type checking.** `EvaluateWithoutTypeCheck` serves the conformance cases that disable type checking. It plans the parsed expression without a checked expression: names are looked up in the bindings when they are evaluated, and a function is chosen among all overloads of its name and argument count by the kinds of the argument values. The value is returned as it is, without conversion, or with one error of the stage `Evaluate`. There is no public counterpart.

All three throw `ArgumentNullException` for a `null` argument and can be called from any number of threads at once, as `Compile<T>` can. Later parts of the library add optional parameters at the end of these signatures — compile options, evaluation options, a cancellation token — so that existing calls stay valid.

## Not in the walking skeleton

- `CelEnvironment.Evaluate<T>`, `CelCompilationCache`, `CelEnvironment.ToBuilder`;
- functions, type sources, extension sets, the name resolution container and time zones in the builder, and the build errors about declarations: conflicts, names, unknown types;
- the check of a binding against the declared type of its variable;
- compile options, the cost limit and cancellation. A non-null `CelCompileOptions`, a cost limit or a token that can be canceled throws `NotImplementedException` instead of being ignored.

The input types of the walking skeleton for `AddVariable<T>` and `CelBindings.Add<T>` are the simple rows of the type mapping table: `long`, `int`, `short`, `sbyte`, `nint`, `ulong`, `uint`, `ushort`, `byte`, `nuint`, `double`, `float`, `Half`, `bool`, `string`, `Guid`, `byte[]`, `ReadOnlyMemory<byte>`, and `CelValue` for bindings. Other types throw `NotImplementedException`.
