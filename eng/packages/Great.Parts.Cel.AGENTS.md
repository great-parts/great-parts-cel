# Great.Parts.Cel: instructions for AI coding agents

This file is for an agent that helps a developer use the package `Great.Parts.Cel` in an application. It is not for the contributors of the library; they have `AGENTS.md` in the repository. The API below is the API of version 2.0; versions 1.x are not supported.

## What it is

Great.Parts.Cel is a library for .NET 10 and .NET 11 that compiles and evaluates expressions of CEL, the Common Expression Language (https://cel.dev/). A CEL expression is a small, safe formula over the data that the application passes in, for example `total > limit && customer.tier == 'gold'`. The language is not Turing complete: an expression has no loops, no assignments and no access to anything but its inputs, so it ends and it is safe to take from users, a database or a configuration file. The library has no package dependencies, uses no reflection, and works under trimming and Native AOT.

## When to use it, and when not

Use it when:

- rules, policies, filters, validation conditions or pricing formulas are written by users or administrators, or stored in a database or a configuration file, and change without a new release of the application;
- the text of the expression is not trusted: the library limits its length and nesting and can limit the cost of an evaluation;
- the same rule runs many times on different data: compile once, evaluate many times;
- the application is published with Native AOT or trimmed.

Do not use it when:

- the rule is known at build time and does not change: write it in C#;
- you need loops, statements, I/O, or arbitrary code: CEL is not a scripting language, and a general scripting engine fits better;
- you need a template language that produces text: CEL gives a value, not a document.

## Install

```shell
dotnet add package Great.Parts.Cel
```

The package targets `net10.0` and `net11.0` only. The source generator is inside the package and needs no second package. Protobuf messages need `Great.Parts.Cel.Protobuf`; dependency injection and ASP.NET Core have their own packages, `Great.Parts.Cel.DependencyInjection` and `Great.Parts.Cel.AspNetCore`. The type `decimal` of CEL is available on `net11.0` and, from 2.0, on `net10.0` too, with a smaller range.

## Which API to take

| The task | The API |
| :- | :- |
| Evaluate one expression on a few values, once | `CelExpression.Evaluate<T>(expression, data)` |
| Evaluate the same rules many times | `CelEnvironment.Create`, `Compile<T>`, `CelProgram<T>.Evaluate` |
| Give the expression an object of the application | a `CelTypeContext` with `[CelObject]`, `UseInput<T>()`, `EvaluateInput` |
| Check an expression when a user saves it | `CelEnvironment.Validate` |
| Evaluate texts that arrive at run time, without compiling twice | `CelCompilationCache` |
| Call the code of the application from an expression | `AddFunction`, `AddMethod` |

## The simple entry

`CelExpression` evaluates in one call. Variables come from an anonymous object or a dictionary, and the type argument is the type of the result:

```csharp
using Great.Parts.Cel;

long result = CelExpression.Evaluate<long>("(x + 2) / 3", new { x = 40L }); // 14

bool allowed = CelExpression.Evaluate<bool>(
    "total > limit",
    new Dictionary<string, object?> { ["total"] = 120L, ["limit"] = 100L }); // true
```

What to know:

- Every error is thrown as `CelException`; `CelException.Errors` lists all errors. Cancellation is `OperationCanceledException`. A `null` expression or data is `ArgumentNullException`.
- Every call has a cost limit, `CelExpression.DefaultCostLimit` (1,000,000 cost units). It cannot be turned off, but a call can pass its own: `costLimit: 50_000`, or `ulong.MaxValue`.
- The standard library version 1, no extension sets, no time zone database (only `UTC` and fixed offsets), no name container. Calls are safe from any number of threads; compiled programs are kept in a hidden cache of the process.
- It reads `bool`, integers, `double`, `string`, `byte[]`, `DateTimeOffset`, a `DateTime` of the kind `Utc`, `TimeSpan`, `Guid`, `JsonElement`, `CelValue`, nullable forms of these, and lists and dictionaries with `string` keys of them. A class, a structure or an interface of the application, and an array, a `List<X>`, an `IReadOnlyList<X>`, an `ImmutableArray<X>`, a `Dictionary<string, X>` or an `IReadOnlyDictionary<string, X>` of one, are read only as a property of the anonymous object, `new { order }`: the source generator describes the type when the application is built. An object of the application as the data itself or as a value of a dictionary, `decimal` and protobuf messages are not read: the error is `TypeNotDeclared`. Build an environment instead.
- The source generator of the package finds each call with an anonymous object at build time and registers its type, so no reflection runs. It sees the static type of the argument. An anonymous object passed through a generic method or a variable of the type `object` works only if the same anonymous type (property names, types and order) is also passed directly to `CelExpression.Evaluate` elsewhere in the same assembly; otherwise the call throws `TypeNotDeclared` and the generator warns at build time. Pass the object directly, or pass a dictionary.
- The simple entry is not faster than an environment built once: the cost is always counted.

## An environment, a program, bindings

For rules that run many times, build the environment once and compile each expression once. The environment and the program are immutable and can be shared by any number of threads.

```csharp
using Great.Parts.Cel;

CelEnvironment environment = CelEnvironment.Create(b => b
    .AddVariable<long>("total")
    .AddVariable<long>("limit"));

CelCompileResult<bool> compiled = environment.Compile<bool>("total > limit");
if (!compiled.IsSuccess)
{
    foreach (var error in compiled.Errors)
    {
        Console.WriteLine(error.ToDisplayString("total > limit")); // message, the line and ^~~~ under the place
    }
    return;
}

CelProgram<bool> program = compiled.Program!;
CelEvalResult<bool> result = program.Evaluate(new CelBindings().Add("total", 120L).Add("limit", 100L));
if (result.IsSuccess)
{
    Console.WriteLine(result.Value); // Output: True
}
```

- `new CelEnvironmentBuilder()` and `CelEnvironment.Create(configure)` use the standard library version 1, and it does not change when the package is updated. Another version is named explicitly, `CelStandardLibrary.V2`, when it exists.
- A parse error, a type error and an evaluation error are returned as results with a `CelErrorCode`, a stage and a place in the expression (`CelError.Span`, `CelError.Subject`, `ExpectedType`, `ActualType`); the expression of a user is not an exception. Test the code, not the text of the message. `Get…OrThrow()` methods turn an error into `CelException` for short code and tests.
- Read `CelEvalResult<T>.Value` only after `IsSuccess`: from 2.0 it throws `CelException` for a failed result. `Cost` tells the cost spent when a limit was in force.
- `CelBindings.Add<T>(name, value)` converts by the static type and throws `ArgumentException` for a repeated name. The indexer `bindings["name"] = value` converts by the run-time type and replaces; it boxes structures. `Clear()` reuses one bindings object in a loop.
- `T` of `Compile<T>` is the .NET type of the result and the expected CEL type. The CEL type of the expression has to fit it, or the error is `ExpectedTypeMismatch`.

## Objects of the application: `UseInput<T>`

Describe the .NET types in a type context. The source generator completes it at build time and reads the members directly, with no reflection, so it works under Native AOT:

```csharp
using Great.Parts.Cel;
using Great.Parts.Cel.Values;

CelEnvironment environment = CelEnvironment.Create(b => b
    .AddTypes(AppTypes.Default)
    .UseInput<Order>());

CelProgram<bool> program = environment.Compile<bool>("total > 100 && currency == 'EUR'").GetProgramOrThrow();
bool big = program.EvaluateInput(new Order(120, "EUR")).GetValueOrThrow();
Console.WriteLine(big); // Output: True

[CelObject(typeof(Order))]
internal sealed partial class AppTypes : CelTypeContext;

public sealed record Order(long Total, string Currency);
```

In a program with top-level statements, the type declarations go after the statements.

- The fields of the input type are variables of the top level (`total`, not `order.total`). The default naming policy is camel case: `TotalAmount` is `totalAmount`. `[CelName("x")]` renames a member and `[CelIgnore]` hides it.
- One input type per environment; a different second type is the build error `InputConflict`. A type that no added context describes is `TypeNotDeclared`. A field with the name of another variable is `DuplicateVariable`.
- Other variables can be declared next to the input and bound by name: `program.EvaluateInput(order, bindings)`. With no bindings, pass the options by name: `EvaluateInput(order, options: new CelEvalOptions { CostLimit = 10_000 })`.
- `EvaluateInput` is the name of the one-step calls with an input, on `CelProgram<T>`, `CelEnvironment`, `CelCompilationCache` and `ICelEvaluator`; `Evaluate` is for bindings.
- The generator reports its warnings and errors as `GPCEL001` to `GPCEL012` (`GPCEL007` is not used); the page of the generator diagnostics says what each one means and what to do.

## Validate an expression without running it

```csharp
using Great.Parts.Cel;

CelEnvironment environment = CelEnvironment.Create(b => b
    .AddVariable<long>("total")
    .AddVariable<long>("limit"));

CelValidationResult check = environment.Validate<bool>("total > limit");
// check.IsValid, check.Errors, check.ResultType,
// check.ReferencedVariables (names, sorted), check.ReferencedFunctions (names, sorted)
Console.WriteLine(check.IsValid); // Output: True
```

`Validate` compiles with the same rules as `Compile<T>` and returns no program. Use it when a user saves a rule: show `error.ToDisplayString(text)` and, if you need to know what the rule reads, `ReferencedVariables`. Local variables of macros are not listed.

## Limits, cost and cancellation

- The text is limited in length and nesting; the page Limits has the numbers.
- An evaluation can have a cost limit that bounds both the time and the allocated memory: `CelEnvironmentBuilder.SetDefaultCostLimit(ulong)` for the environment, `CelEvalOptions.CostLimit` for one call. The error is `CostLimitExceeded`. Without a limit the cost is not counted, and nothing slows down.
- A `CancellationToken` stops an evaluation with `OperationCanceledException`.
- `matches` uses the syntax of RE2 and runs in time linear in the length of the string; do not rely on the backtracking behavior of .NET `Regex`.

## Compile once: the cache

`CelCompilationCache` keeps compiled programs for texts that arrive at run time. `new CelCompilationCache(environment)` takes the default capacities; `cache.Evaluate<T>(text, bindings)` and `cache.EvaluateInput<T>(text, input)` compile once per text. `TryRemove(text)` forgets a rule that changed in the database, and `Clear()` forgets all. Do not build a program for each request.

## Functions of the application

`AddFunction("isBlocked", (string inn) => …)` adds a function; `AddMethod<string, bool>("isKpp", s => …)` adds a method on a receiver, called as `kpp.isKpp()`. A function runs synchronously on the thread that evaluates: load the data first, put it in `CelBindings`, and never block on `.Result` or `.Wait()` inside a function.

## Common mistakes

1. Building a new environment or compiling the same text for every call. Build once, or use `CelCompilationCache`.
2. Catching `CelException` around a path that returns results, or ignoring `Errors`. Check `IsSuccess`; read `Value` only after it.
3. Passing an object of the application as the data itself, `Evaluate(text, order)`, or as a value of a dictionary, or a `decimal`, to `CelExpression.Evaluate`. Pass the object as a property of an anonymous object, `new { order }`, or declare the types in a `CelTypeContext` and use an environment.
4. Passing an anonymous object through a generic method or as `object`. The generator cannot see the type; pass it directly.
5. Describing the types of the application with reflection or by scanning assemblies at run time. The library does not read types that way; a `CelTypeContext` does it at build time and keeps the application working under trimming and Native AOT.
6. Comparing different numeric types without conversion: in CEL `1 == 1u` is allowed, but arithmetic between `int` and `double` is a type error; convert with `double(x)`, `int(x)`.
7. Forgetting the range of CEL numbers: `int` is 64 bits, `uint` literals end with `u`, a literal out of the range is `InvalidLiteral`, and arithmetic that leaves the range is the error `Overflow`, not a wrap-around.
8. Reading `order.total` when the input type is used: the fields of the input are top-level names, `total`.
9. Writing `Evaluate(order)` for an input: the call with an input is `EvaluateInput`.
10. Depending on the text of an error message. Test `CelError.Code`.
11. Turning off the limit of the simple entry for expressions of users. Pass a limit that fits the application instead.
12. Expecting `timestamp` zones other than `UTC` and fixed offsets to work without a time zone database: add a database with `UseTimeZones(CelTimeZoneDatabase.FromSystem())` or a database of the application.

## Documentation

The documentation of a release is the content of the repository at the tag of the release. Until the release of 2.0, the links on this page point to the `main` branch of the repository.

- [Getting Started](https://github.com/great-parts/great-parts-cel/blob/main/docs/getting-started.md): seven scenarios, from the simple entry to a web service
- [FluentValidation recipe](https://github.com/great-parts/great-parts-cel/blob/main/docs/recipes/fluentvalidation.md)
- [README of the repository](https://github.com/great-parts/great-parts-cel/blob/main/README.md)
- [Standard library](https://github.com/great-parts/great-parts-cel/blob/main/docs/standard-library.md)
- [Extension sets](https://github.com/great-parts/great-parts-cel/blob/main/docs/extensions.md)
- [Type mapping](https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md)
- [Error codes](https://github.com/great-parts/great-parts-cel/blob/main/docs/error-codes.md)
- [Limits](https://github.com/great-parts/great-parts-cel/blob/main/docs/limits.md) and [Cost](https://github.com/great-parts/great-parts-cel/blob/main/docs/cost.md)
- [Source generator diagnostics](https://github.com/great-parts/great-parts-cel/blob/main/docs/generator-diagnostics.md)
- [Application functions](https://github.com/great-parts/great-parts-cel/blob/main/docs/recipes/application-functions.md) and [Time zone database](https://github.com/great-parts/great-parts-cel/blob/main/docs/recipes/time-zone-database.md)
- [Decimal](https://github.com/great-parts/great-parts-cel/blob/main/docs/decimal.md)
- [API concepts](https://github.com/great-parts/great-parts-cel/blob/main/docs/api-concepts.md)
- [Versioning and compatibility](https://github.com/great-parts/great-parts-cel/blob/main/docs/versioning.md)
- [Changelog](https://github.com/great-parts/great-parts-cel/blob/main/CHANGELOG.md)

The packages are built and published by GitHub Actions with attestations of origin; how to verify them is in the README of the repository.
