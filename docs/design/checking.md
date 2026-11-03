# Checking: type checking

`Great.Parts.Cel.Checking` takes a parsed expression and the declarations of an environment and gives a checked expression: the type of every node, what every identifier and call refers to, and the type of the whole expression — or the list of check errors. Type checking is always part of compilation: a program is built only from a checked expression. The planner turns the checked expression into a plan (see [evaluation](evaluation.md)).

The walking skeleton checks literals of every kind, identifiers that name variables, calls of global functions and operators, and list and map literals, by the rules of gradual type checking of the language definition.

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Checking/Checker.cs` | the traversal and the rules per node |
| `src/Great.Parts.Cel/Checking/DeclarationIndex.cs` | the declarations of an environment, indexed for lookups |
| `src/Great.Parts.Cel/Checking/Reference.cs` | what an identifier or a call refers to |
| `src/Great.Parts.Cel/Checking/CheckedExpression.cs` | the result: tree, reference map, type map, type of the expression |
| `src/Great.Parts.Cel/Checking/CheckResult.cs` | the result of the internal entry point `CelEnvironment.Check` (see [facade](facade.md)) |
| `src/Great.Parts.Cel/Checking/CheckErrors.cs` | the messages of the check errors |
| `src/Great.Parts.Cel/Types/AssignabilityRules.cs` | assignability of types, a rule file of the core (see [core](core.md)) |

## Declaration index

`CelEnvironmentBuilder.Build` creates one `DeclarationIndex` per environment and keeps it in the immutable `CelEnvironment`. It merges the declaration table of the standard library version (see [library](library.md)) with the declarations of the application:

- variables by name — a frozen dictionary from the name to its `CelVariableDecl`;
- functions by name — a frozen set of the function names, so that the checker tells an unknown function from a known one called with arguments that do not fit;
- overloads by function name and argument count — a frozen dictionary from the pair to the overloads in the order of their declaration. Receiver-style overloads, which come with the complete grammar, are kept apart from global ones; a receiver counts as an argument.

An overload in the index is an `OverloadBinding`: the declaration with its implementation and cost (see [library](library.md)), so a candidate already carries the code that evaluation calls. In the walking skeleton all overloads come from the standard library.

## Rules per node

The checker visits the tree depth first: the children of a node from left to right, then the node. It records the type of every node in the type map, and the reference of every identifier and call in the reference map, both by node id.

| Node | Type | Reference |
| :- | :- | :- |
| constant | by the kind of the value: `null_type`, `bool`, `int`, `uint`, `double`, `string`, `bytes` | — |
| identifier | the declared type of the variable | the variable |
| call | the result type of the candidate | the candidates |
| list | `list(T)` when all elements have type `T`; `list(dyn)` when the list is empty or the element types differ | — |
| map | `map(K, V)` by the same rule for the keys and for the values; `map(dyn, dyn)` when empty | — |

The key types of a map literal are not checked: a key that cannot be a map key is an evaluation error (see [evaluation](evaluation.md)).

## Overload candidates

For a call `f(a1, …, an)` the checker looks up the overloads of `f` with `n` arguments. An overload is a candidate when the type of each argument is assignable to the type of its parameter, by `AssignabilityRules`. In the walking skeleton a type is assignable to another when they are equal or when either is `dyn`; type parameters and their substitution come with the complete type checking.

All candidates are recorded in the reference map of the call, in the order of the index; the checker does not pick one of them. With static types only one overload fits, because overloads of one function do not overlap. When an argument has the type `dyn`, several overloads can fit, and evaluation chooses among them by the kinds of the values. The type of the call is the result type of its candidate; for several candidates it is their common result type, or `dyn` when they differ.

The walking skeleton has at most one overload per function name and argument count, so a call has one candidate or none. The rest of the path — direct binding of one candidate, the choice among several, the error when none fits at evaluation — is in [evaluation](evaluation.md).

## The type `error` and the list of errors

A node with an error gets the type `error`. It is assignable to every type and produces no further errors: a call with an argument of type `error` is not looked up, and gets the type `error` itself; so does a list or a map with such a part. Checking goes on after an error, so a compilation returns every check error of the text, in the order of the traversal.

The type `error` has no `CelType`: the checker keeps the type of a node as `CelType?`, where `null` stands for `error`. A checked expression exists only when checking found no error, so its type map has no `null`.

| Code | Message | Position |
| :- | :- | :- |
| `UndeclaredReference` (2000) | `undeclared reference to '{name}' (in container '{container}')` | the identifier; for an unknown function, the call |
| `NoMatchingOverload` (2001) | `found no matching overload for '{function}' applied to '({argumentTypes})'` | the call |

Both have the stage `Check`. `UndeclaredReference` names an identifier that is not a variable, or a function name with no overloads at all; the container is empty in the walking skeleton. `NoMatchingOverload` names a known function whose overloads with that argument count do not accept the argument types, or that has no overload with that argument count. Types in messages are written as in the language definition: `int`, `list(int)`, `map(string, dyn)`. The position of a node comes from the parsed expression and its line table (see [syntax](syntax.md)).

## Checked expression

`CheckedExpression` holds the parsed expression (tree, positions and line table), the reference map, the type map and `ResultType`, the type of the root, which `CelProgram<T>.ResultType` reports. Its references hold the declarations and overloads of the environment that checked it.

## Expected type

The expected type of the result follows from `T` of `Compile<T>`: `bool` from `bool`; `int` from `long` and `int`; `uint` from `ulong` and `uint`; `double` from `double`; `string` from `string`; `bytes` from `ReadOnlyMemory<byte>` and `byte[]`; `dyn` from `CelValue`, which accepts a result of any type. With the complete type checking the type of the root must be assignable to the expected type, otherwise compilation fails with `ExpectedTypeMismatch` at the root, and the compile options can give the expected type explicitly.

In the walking skeleton the expected type is not checked: a mismatch shows up at evaluation as `ResultConversion` (see [facade](facade.md)). `Compile<T>` with a `T` outside the table of the facade, and a non-null `CelCompileOptions`, throw `NotImplementedException` instead of being ignored.

## Not in the walking skeleton

Type parameters, receiver-style calls, field selection, macros, type identifiers, the name resolution container, the expected type and `ExpectedTypeMismatch`, the stack check before each recursive step with `InsufficientStack`, and the compilation boundary that turns an unexpected exception into `InternalError` come with the complete type checking. Until then the depth of the checker's recursion is bounded only by the limits of the parser ([syntax](syntax.md#limits-and-the-stack)), and a node that the checker does not know yet, such as a selection or a comprehension, throws `NotImplementedException`.

Related pages: [library](library.md), [evaluation](evaluation.md), [values](values.md), [facade](facade.md).
