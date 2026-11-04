# Checking: type checking

`Great.Parts.Cel.Checking` takes a parsed expression and the declarations of an environment and gives a checked expression: the type of every node, what every identifier, selection and call refers to, and the type of the whole expression — or the list of check errors. Type checking is always part of compilation: a program is built only from a checked expression. The planner turns the checked expression into a plan (see [evaluation](evaluation.md)). The rules are those of gradual type checking of the language definition; the type rules themselves — assignability, substitution, join — are in [types](types.md).

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Checking/Checker.cs` | the traversal, the scopes and the rules per node |
| `src/Great.Parts.Cel/Checking/DeclarationIndex.cs` | the declarations of an environment, indexed for lookups |
| `src/Great.Parts.Cel/Checking/Reference.cs` | what an identifier, a selection or a call refers to |
| `src/Great.Parts.Cel/Checking/CheckedExpression.cs` | the result: tree, reference map, type map, type of the expression |
| `src/Great.Parts.Cel/Checking/CheckResult.cs` | the result of the internal entry point `CelEnvironment.Check` (see [facade](facade.md)) |
| `src/Great.Parts.Cel/Checking/CheckErrors.cs` | the messages of the check errors |

## Declaration index

`CelEnvironmentBuilder.Build` creates one `DeclarationIndex` per environment, after the build checks of [declarations](declarations.md), and keeps it in the immutable `CelEnvironment`. It merges the table of the standard library version (see [library](library.md)) with the declarations of the application:

- **identifiers** by full name — variables, and the type identifiers of the standard library version: `bool`, `bytes`, `double`, `int`, `uint`, `string`, `null_type` and `type` have the static type `type(T)` of their type, `list` has `type(list(dyn))` and `map` `type(map(dyn, dyn))`; each holds its type value, which the planner turns into a constant. `google.protobuf.Timestamp` and `google.protobuf.Duration` join with time. `dyn` is not an identifier: it is a function;
- **functions** by name — a frozen set of function names, so that the checker tells an unknown function from a known one whose overloads do not fit;
- **overloads** by function name, call style and argument count; a receiver counts as an argument. Overloads of the standard library come first, in the order of its table; overloads of the application follow, ordered by overload identifier (ordinal). So the order of candidates, which evaluation uses to dispatch, does not depend on the order of the builder calls.

An overload in the index is an `OverloadBinding` with its implementation and cost (see [library](library.md)).

## Name resolution

The container of the environment (`UseContainer`) qualifies names as in the language definition, "Name Resolution": in the container `a.b` the name `x` is tried as `a.b.x`, `a.x`, then `x`, and the first declared one wins; a name with a leading `.` is tried only as written, without the dot. Without a container a name is tried as written.

- **Identifier.** The scopes of enclosing comprehensions come first, innermost first: their iteration variable and accumulator shadow every declaration; a leading `.` skips them. Then the declarations, by the candidates of the container. Not found is `UndeclaredReference`.
- **Selection chain.** For `a.b.c`, where the chain of selections ends in an identifier, the checker first tries the whole qualified name `a.b.c` as a declared identifier, then `a.b` with `.c` a field selection, then `a`: the longest declared prefix wins. A chain whose first name is a comprehension variable is never a qualified name. When a prefix resolves, the reference map records the selection node as a reference to that declaration, and the planner reads it like an identifier.
- **Global call** `f(…)`: the function name by the candidates of the container.
- **Receiver call** `a.b.f(…)`: when the target is a chain of names, `a.b.f` is first tried as a qualified function name by the candidates of the container and, if declared, the call is a global call of that function with the arguments only. Otherwise the target is checked and `f` is looked up among receiver overloads, also by the candidates of the container.

`namespace.textproto` needs all of these; its unchecked cases go through the internal entry point without type checking, whose name resolution is in [evaluation](evaluation.md).

## Rules per node

The checker visits the tree depth first, children before the node, except that a comprehension adds its scopes as described below. It records the type of every node in the type map and the reference of every identifier, resolved selection and call in the reference map, both by node id.

| Node | Type | Reference |
| :- | :- | :- |
| constant | by the kind of the value: `null_type`, `bool`, `int`, `uint`, `double`, `string`, `bytes` | — |
| identifier | the type of the declaration or of the comprehension variable | the declaration or the local |
| selection resolved as a name | the type of the declaration | the declaration |
| selection `e.f` | `map(K, V)`: `V`; `dyn`, `error` or a type parameter: `dyn` (a parameter is bound to `dyn`); any other type: `NoSuchField` at the selection, type `error` | — |
| presence test `has(e.f)` | `bool`, after the same check of `e` | — |
| call | by its candidates, below | the candidates |
| list, map literal | the join of the element, key and value types ([types](types.md)); empty: free type variables | — |
| message literal `T{…}` | message literals come with object types; until then no name is an object type, so the name is `UndeclaredReference` | — |
| comprehension | the type of the result | — |

The key types of a map literal are not checked: a key that cannot be a map key is an evaluation error (see [evaluation](evaluation.md)).

**Optional elements and entries** `[?e]`, `{?k: v}` (syntax option `OptionalSyntax`). The element or the value, after the substitution, must have the type `optional_type(T)`, which gives `T` to the join, or `dyn`, which gives `dyn`. Any other type, a free type variable included, is `TypeMismatch` at the element or the value with the expected type `optional_type` of the found type — `[?1]` and `{?'a': 1}` are `expected type 'optional_type(int)' but found 'int'` — and the list or map gets the type `error`; an element of the type `error` gives no second error. cel-go v0.32.0 checks the same, but goes on with `T`, so `[?1] + 1` gives it a second error at `_+_`: the outcome is the same, the number of errors differs. In this part no expression has an `optional_type`: a declaration with it is `TypeNotDeclared`, the functions of `optional` come with their part, and the syntax option is set only through the internal syntax options; so only `dyn` passes.

**`&&` and `||`.** Each operand must be assignable to `bool`; an operand that is not is `TypeMismatch` at that operand, as cel-go does, and the call has the type `error`. Otherwise the type is `bool`.

**Index `e[i]`.** It is the call `_[_]` with the overloads `(list(A), int) -> A` and `(map(K, V), K) -> V` of the library, checked like any call below: `['foo'][0]` is `string`, `{'abc': 123}['abc']` is `int`, a `dyn` target with an `int` index matches both overloads, so the type is `dyn`; an index of another type on a list, such as `[1][1u]`, is `NoMatchingOverload`.

**Comprehension.** The checker checks the range and the accumulator initializer in the enclosing scopes. The iteration variable has the element type of a `list(E)` range, the key type of a `map(K, V)` range, and `dyn` for a `dyn`, `error` or type parameter range (a free parameter is bound to `dyn`, as for a selection, so `[].map(x, x.map(y, y))` is `list(list(dyn))`); another range type is `InvalidComprehensionRange` at the range, and the variable gets the type `error`. In a new scope with the accumulator, of the type of the initializer, and then the iteration variable, the checker checks the loop condition, which must be assignable to `bool`, and the loop step, which must be assignable to the type of the accumulator; a mismatch is `TypeMismatch` at that part. The result is checked with the accumulator but without the iteration variable. The type of the comprehension is that of the result after the substitution. An invalid range does not give the comprehension the type `error`: its type is still that of the result, as in cel-go v0.32.0, so a call around it is looked up. `size(1.all(x, true))` is `InvalidComprehensionRange` at the range and `NoMatchingOverload` for `size(bool)` at the call; `size(1.map(x, x))`, whose result is a list, is only `InvalidComprehensionRange`. A second iteration variable comes with the two-variable macros.

## Overload candidates

For a call the checker takes the overloads of the function with its call style and argument count, in index order. For each overload it replaces the type parameters of the declaration by fresh type variables and tries the argument types against the parameter types with `IsAssignable` — the receiver first, then the arguments — on the substitution of the expression, with these fresh variables as the only ones that a trial inside a type value may bind (see [types](types.md), assignability, rule 6). A successful trial makes the overload a candidate and keeps its bindings, so later overloads are tried with them.

An overload that the library table marks *only with `dyn`* — the 24 overloads of ordering across `int`, `uint` and `double`, as cel-go filters them without `CrossTypeNumericComparisons` — is a candidate only when at least one argument has the static type `dyn` after the substitution: `1 < 1.5` is `NoMatchingOverload`, `dyn(1) < 1.5` has candidates for evaluation to choose from.

All candidates are recorded in the reference map of the call in index order; the checker does not choose among them. The type of the call is the result type of the first candidate after the substitution; when a later candidate's result type differs, the type is `dyn`. No candidate is `NoMatchingOverload` with the argument types after the substitution, free variables written as `dyn`. A call with an argument of type `error` is not looked up and has the type `error`.

## The type `error` and the list of errors

A node with an error gets the type `error`, assignable to every type, and produces no further errors; a list, a map or a call with such a part gets the type `error` too. Checking goes on after an error, so a compilation returns every check error of the text. The list is ordered by offset; errors at one offset keep the order in which the checker found them. This is not always the order of the traversal: a call is checked after its arguments, so `size(1.all(x, true))` gives `NoMatchingOverload` at the call (offset 4) before `InvalidComprehensionRange` at the range (offset 5), though the range fails first. cel-go v0.32.0 orders its issues the same way, stably by line and column. A checked expression exists only when there is no error. All have the stage `Check`.

| Code | Message | Position |
| :- | :- | :- |
| `UndeclaredReference` (2000) | `undeclared reference to '{name}' (in container '{container}')` | the identifier or the message literal; for an unknown function, the call |
| `NoMatchingOverload` (2001) | `found no matching overload for '{function}' applied to '({argumentTypes})'` | the call |
| `TypeMismatch` (2002) | `expected type '{expected}' but found '{actual}'` | the operand of `&&` or `\|\|`, the loop condition or the loop step |
| `ExpectedTypeMismatch` (2003) | `expression of type '{actual}' is not assignable to the expected type '{expected}'` | the root |
| `NoSuchField` (2004) | `undefined field '{field}' in type '{type}'` | the selection; here only for a type without fields, object fields come with object types |
| `InvalidComprehensionRange` (2005) | `expression of type '{type}' cannot be the range of a comprehension (must be list, map, or dyn)` | the range |
| `InsufficientStack` (9000) | `insufficient execution stack to continue type checking` | the node not checked |
| `InternalError` (9001) | `internal error during type checking; the original exception is in CelError.Exception` | the node being checked |

Types in messages use the text form of [types](types.md). The container in 2000 is empty without a container.

## Checked expression

`CheckedExpression` holds the parsed expression (tree, positions and line table), the reference map, the type map with every type parameter replaced — free ones by `dyn` — and `ResultType`, the type of the root, which `CelProgram<T>.ResultType` reports.

## Expected type

The expected type comes from `T` of `Compile<T>` by the expected-type table of the type mapping page, or from `CelCompileOptions.ExpectedType`; the facade derives it and rejects an incompatible pair (see [facade](facade.md)). After the traversal, if there is no error, the type of the root must be assignable to the expected type by `IsAssignable`; otherwise compilation fails with `ExpectedTypeMismatch`. `dyn` expects anything, and an expression of type `dyn` passes any expected type: its value is converted, or refused with `ResultConversion`, at evaluation.

## The stack and the compilation boundary

Before each recursive step the checker calls `RuntimeHelpers.TryEnsureSufficientExecutionStack()`; `false` is `InsufficientStack` at the node it was about to check, and checking stops there: the compilation returns the errors found so far and this one, ordered by offset as above. `CelEnvironment.Check`, which `Compile<T>` and the internal entry point share, runs the checker inside one handler: an unexpected exception other than `OutOfMemoryException` becomes `InternalError` with the stage `Check` at the node the checker was at, and the result holds it with the errors found before it, in the same order. No node throws `NotImplementedException`.

## Not in this part

Object types, their fields and message literals, with `NotAType` (2006); the presence test on objects; `optional` selections and indexes; the second iteration variable; constant arguments prepared at planning (`matches`). Each comes with its part.

Related pages: [types](types.md), [declarations](declarations.md), [library](library.md), [evaluation](evaluation.md), [values](values.md), [facade](facade.md).
