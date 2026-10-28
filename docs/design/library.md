# Library: the standard library

`Great.Parts.Cel.Library` holds the standard library of each version: the declarations of its functions and overloads, their implementations and their costs. An environment names its version in `new CelEnvironmentBuilder(CelStandardLibrary.V1)`; the public `CelStandardLibrary.V1` is a handle to the table of version 1 in this namespace (see [core](core.md)).

The walking skeleton has the overloads that the cel-spec test files `basic` and `plumbing` and the end-to-end example need: `_+_`, `_/_` and `-_` for `int`, and `_||_`. Extension sets and the namespaces under `Library` come later.

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Library/StandardLibraryV1.cs` | the table of version 1 |
| `src/Great.Parts.Cel/Library/OverloadIds.cs` | the overload identifiers as constants |
| `src/Great.Parts.Cel/Library/IntArithmetic.cs` | the implementations for `int` |
| `src/Great.Parts.Cel/Declarations/OverloadBinding.cs` | one overload with its implementation and cost |
| `src/Great.Parts.Cel/Declarations/OverloadImplementation.cs` | the internal delegate of an implementation |
| `src/Great.Parts.Cel/Declarations/CallSite.cs` | the call that an implementation serves, for its errors |

The three types in `Declarations` are internal. They live there, not in `Library`, because the layer table lets `Checking` and `Evaluation` refer to `Declarations` but not to `Library`: the checker records overloads as candidates and the evaluator calls them, while only the facade sees the tables of `Library`.

## Tables

A version of the standard library is a static, immutable table, created once per process when it is first used. The table of a version never changes: a later version is a new table, and a fix of behavior changes an implementation that all versions share. The table is an `ImmutableArray<OverloadBinding>` in the order of the reference implementation, cel-go, and a frozen dictionary from the function name to its overloads. `CelEnvironmentBuilder.Build` merges it with the declarations of the application into the declaration index of the environment (see [checking](checking.md)); the table itself holds no state of an environment.

`OverloadBinding` holds:

- `Function` — the name of the function in the syntax tree, such as `_+_`;
- `Declaration` — the public `CelOverloadDecl`: the overload identifier, the call style, the parameter types and the result type;
- `Implementation` — an `OverloadImplementation`, or `null` for a special form that the planner evaluates itself;
- `FixedCost` and `ArgumentCost` — the cost of a call: fixed units, and a `CelCostFunction` of the arguments or `null`;
- `Source` — the source that build errors name, such as `standard library v1`.

Application functions use the same type: the builder wraps their declarations and public `CelFunctionImplementation` delegates into it when application functions arrive. Their cost is 1 fixed unit plus the cost function of the declaration.

## Overloads of the walking skeleton

| Identifier | Function | Signature | Semantics | Errors | Cost |
| :- | :- | :- | :- | :- | :- |
| `add_int64` | `_+_` | `(int, int) -> int` | sum | `Overflow` | 1 |
| `divide_int64` | `_/_` | `(int, int) -> int` | integer division, truncated toward zero | divisor 0 — `DivisionByZero`; `-2^63 / -1` — `Overflow` | 1 |
| `negate_int64` | `-_` | `(int) -> int` | negation | `-(-2^63)` — `Overflow` | 1 |
| `logical_or` | `_\|\|_` | `(bool, bool) -> bool` | `true` absorbs an error of the other operand | an error when the result is not determined | 0 beyond the operands |

The identifiers are those of cel-go, letter for letter, so that the overload lists of the library and of the oracle compare without exceptions. The costs are the units of the cost table: a call of constant time without allocation is 1 unit; a logical operator costs nothing beyond the operands it evaluates. Each overload carries its cost from the moment it appears, although evaluation starts counting cost only with the cost limit (see [evaluation](evaluation.md)); a test compares the costs in the table with the cost table of the documentation.

| Code | Message | Position |
| :- | :- | :- |
| `DivisionByZero` (3004) | `division by zero` | the call of `_/_` |
| `Overflow` (3006) | `integer overflow` | the call |

`logical_or` has no implementation: the planner turns a chain of `||` into one node with its own rules for errors (see [evaluation](evaluation.md)). The declaration is still in the table, because type checking and the overload lists need it.

## Implementations

```text
namespace Great.Parts.Cel.Declarations
internal delegate CelValue OverloadImplementation(ReadOnlySpan<CelValue> arguments, in CallSite site);

internal readonly struct CallSite
    string Function                                   // the function name, for messages
    CelValue Error(CelErrorCode code, string message) // an error value at the position of the call
    CelValue NoMatchingOverload(ReadOnlySpan<CelValue> arguments)
```

- An implementation receives the argument values in the order of the parameters and returns the result or an error value. It never receives an error value as an argument: calls are strict, and the evaluator returns the error of an argument before it calls the implementation.
- An error is a value of the kind `Error` created through `CallSite`, which knows the position of the call in the expression and its line table. So the error has the position of the call that failed, and the implementation stays free of positions.
- An implementation reads its arguments with the `TryGet…` methods of `CelValue` and returns `EvalNoMatchingOverload` (3000) for a kind its overload does not take. Type checking and the planner normally rule this out; the check keeps a binding of the wrong kind from becoming a wrong value, because the check of bindings against the declared types of their variables comes after the walking skeleton (see [facade](facade.md)).
- Implementations do not throw. Overflow is detected without exceptions: a sum overflows when both operands have the same sign and the result has the other; division overflows only for `-2^63 / -1`; negation only for `-2^63`.
- Implementations are static methods without state, so a program calls them from any number of threads at once.

The implementations decide the value or the error of an expression, so this namespace is part of the core: a change here is checked against the reference implementation with `eng/difftest`.

## Later parts

The complete library adds the other functions and operators of version 1 with their identifiers, costs and errors, the type identifiers such as `int` and `list`, and the standard macros, which the parser expands. Time functions go to `Library.Time`, `matches` to `Library.Regex` and `Library.Unicode`, extension sets to their own tables. Build errors about conflicts with the standard library come with application functions.

Related pages: [checking](checking.md), [evaluation](evaluation.md), [values](values.md), [core](core.md).
