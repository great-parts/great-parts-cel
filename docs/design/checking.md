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

- **identifiers** by full name — variables, and the type identifiers of the standard library version: `bool`, `bytes`, `double`, `int`, `uint`, `string`, `null_type` and `type` have the static type `type(T)` of their type, `list` has `type(list(dyn))` and `map` `type(map(dyn, dyn))`; each holds its type value, which the planner turns into a constant. `google.protobuf.Timestamp` and `google.protobuf.Duration` have the static types `type(timestamp)` and `type(duration)` and are qualified identifiers; `timestamp` and `duration` alone are functions. `dyn` is not an identifier: it is a function;
- **types of the application**, from the type index of [objects](objects.md): each object type by its full name, an identifier of the static type `type(T)` that holds its type value, and each enumeration constant by its full name, an identifier of the type `int` that holds its value. The name of an enumeration type is no identifier. One candidate name is tried as a variable first, then as an object type, then as a constant, as cel-go v0.32.0 `Env.LookupIdent` does; a variable may have the name of a type;
- **fields** of each object type, by `CelObjectType.TryGetField`;
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
| selection `e.f` | `map(K, V)`: `V`; an object type `T`: the type of the field `f` of `T`; `dyn`, `error` or a type parameter: `dyn` (a parameter is bound to `dyn`); any other type: `NoSuchField` at the selection, type `error` | on an object type, the field: the description of `T` and the number of `f` |
| presence test `has(e.f)` | `bool`, after the same check of `e` and `f` | the same |
| call | by its candidates, below | the candidates |
| list, map literal | the join of the element, key and value types ([types](types.md)); empty: free type variables | — |
| message literal `T{…}` | below; type `error` in this part | the type, when the name resolves |
| comprehension | the type of the result | — |

The key types of a map literal are not checked: a key that cannot be a map key is an evaluation error (see [evaluation](evaluation.md)).

**Fields of objects.** For `e.f` and `has(e.f)` where `e` has an object type `T` after the substitution, the field is looked up in the description of `T` by its name, ordinally. Found: the type of the selection is the declared type of the field, and the reference map records the description and the field number, which the planner binds ([evaluation](evaluation.md)); `has(e.f)` is `bool`. Not found: when `GetExcludedFieldReason(f)` gives a reason, `FieldNotAccessible` (2007), which only descriptions of the generator give; otherwise `NoSuchField` (2004) with the field and the full name of `T`, at the selection, type `error`. Selection chains resolve by the longest declared prefix, as in "Name resolution", with type names and constants among the identifiers: `acme.crm.Deal` is the type identifier, `acme.sales.Status.Approved` the constant, and `deal.amount` with the variable `deal` the field `amount`.

**Message literals** `T{f: v, …}`. The name resolves by the container like an identifier, with the order of the declaration index; the field values are checked in order, as expressions, whatever the literal gives:

1. no candidate is declared — `UndeclaredReference` (2000) at the literal;
2. the name resolves to a variable, a constant or a type identifier that is not an object type (`int`, `list`) — `NotAType` (2006) `'{name}' is not a type`, `{name}` the resolved full name, at the literal. The literal node is the position: the name has no node of its own, and cel-go reports there too;
3. an object type whose description is not constructible — all types of this part: types described at run time, and .NET types — `TypeNotConstructible` (2008) `type '{type}' cannot be created by an expression: object literals are supported only for protobuf messages`, at the literal. Its field names are not looked up.

The literal has the type `error` after each of these. Literals of constructible types — protobuf messages — check their fields with their part.

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
| `UndeclaredReference` (2000) | `undeclared reference to '{name}' (in container '{container}')`, `{name}` as written | the identifier or the message literal; for an unknown function, the call |
| `NoMatchingOverload` (2001) | `found no matching overload for '{function}' applied to '({argumentTypes})'` | the call |
| `TypeMismatch` (2002) | `expected type '{expected}' but found '{actual}'` | the operand of `&&` or `\|\|`, the loop condition or the loop step |
| `ExpectedTypeMismatch` (2003) | `expression of type '{actual}' is not assignable to the expected type '{expected}'` | the root |
| `NoSuchField` (2004) | `undefined field '{field}' in type '{type}'`, `{type}` the text form of the type: the full name of an object type, `int`, `list(int)` | the selection: a type without fields, or an object type without the field |
| `InvalidComprehensionRange` (2005) | `expression of type '{type}' cannot be the range of a comprehension (must be list, map, or dyn)` | the range |
| `NotAType` (2006) | `'{name}' is not a type` | the message literal |
| `TypeNotConstructible` (2008) | `type '{type}' cannot be created by an expression: object literals are supported only for protobuf messages` | the message literal |
| `InvalidRegex` (2009) | `invalid regular expression at offset {offset}: {reason}`, from the preparation of a constant argument, below | the string literal of the pattern |
| `InsufficientStack` (9000) | `insufficient execution stack to continue type checking` | the node not checked |
| `InternalError` (9001) | `internal error during type checking; the original exception is in CelError.Exception` | the node being checked |

Types in messages use the text form of [types](types.md). The container in 2000 is empty without a container.

## Checked expression

`CheckedExpression` holds the parsed expression (tree, positions and line table), the reference map, the type map with every type parameter replaced — free ones by `dyn` — and `ResultType`, the type of the root, which `CelProgram<T>.ResultType` reports.

## Constant arguments

An overload may name one argument that the checker prepares at compilation: `OverloadBinding.ConstantArgument`, `null` for every overload but `matches` and `matches_string`, holds the index of the argument — the receiver counts as an argument, so it is 1 for both — and a preparer ([declarations](declarations.md), "Constant arguments"). The checker calls it in `Resolve`, after the candidates of a call are recorded and the call has its type, so only a call that type checking accepted is looked at: `matches(1, '(')` is `NoMatchingOverload` and its pattern is not read.

- **When.** Every candidate of the call has a constant argument, at the same index — an overload of the application of the same name does not, and then nothing is prepared and the call is left to evaluation, which must choose among the overloads by the values — and the node at that index is a string literal. A string literal is a constant node of the kind `string` in any of its forms: quoted, raw, triple-quoted, between parentheses (the tree has no node for the parentheses). Nothing else is a constant: not a variable, not a call, even `dyn('(')` and `string('(')`, not a concatenation, a `?:`, an element of a list or of a map, or the variable of a macro. The checker does no folding, so the class of the outcome — compile error or evaluation error — is decided by the form of the text alone.
- **What it does.** `Prepare(value, cache)` takes the value of the literal, after the escapes of CEL are read, and the cache of patterns of the environment, and returns either a prepared object or the text of an error. On an error the checker adds `InvalidRegex` (2009), stage `Check`, with that text and the position of the literal: its first character, the `r` or the quote of a raw or triple-quoted one, the character after the parenthesis for a literal in parentheses. The call keeps its type `bool` and its candidates, since type checking of the call succeeded; only the preparation failed. On success the reference of the call keeps the prepared object, which the planner takes ([evaluation](evaluation.md), "Constant arguments").
- **Where in the walk.** After the arguments of the call have been checked, so every literal of the text is looked at once, whether or not the call is ever evaluated: in a branch that is never taken, in a macro body, in an operand of `&&` or `||`. The errors join the others of the check in the list ordered by offset: `matches('a', '(') && matches(1, 'b')` has `InvalidRegex` at the first pattern and `NoMatchingOverload` at the second call, in the order of the text.
- **What it never does.** It does not run for a call that has an argument of type `error`, which is not looked up, and `EvaluateWithoutTypeCheck` does not run it at all ([evaluation](evaluation.md), "Evaluation without type checking").

The cache of patterns comes from the declaration index, which the environment created with it ([declarations](declarations.md)); the checker only hands it to the preparer. Preparation is not charged and has no limit of its own besides those of the pattern and of the expression ([regex-syntax](regex-syntax.md)); a literal is at most as long as the expression.

## Expected type

The expected type comes from `T` of `Compile<T>` by the expected-type table of the type mapping page, or from `CelCompileOptions.ExpectedType`; the facade derives it and rejects an incompatible pair (see [facade](facade.md)). After the traversal, if there is no error, the type of the root must be assignable to the expected type by `IsAssignable`; otherwise compilation fails with `ExpectedTypeMismatch`. `dyn` expects anything, and an expression of type `dyn` passes any expected type: its value is converted, or refused with `ResultConversion`, at evaluation.

## The stack and the compilation boundary

Before each recursive step the checker calls `RuntimeHelpers.TryEnsureSufficientExecutionStack()`; `false` is `InsufficientStack` at the node it was about to check, and checking stops there: the compilation returns the errors found so far and this one, ordered by offset as above. `CelEnvironment.Check`, which `Compile<T>` and the internal entry point share, runs the checker inside one handler: an unexpected exception other than `OutOfMemoryException` becomes `InternalError` with the stage `Check` at the node the checker was at, and the result holds it with the errors found before it, in the same order. No node throws `NotImplementedException`.

## Not in this part

Literals of protobuf messages and their fields; `FieldNotAccessible` (2007), with the generator and `decimal`; `optional` selections and indexes; the second iteration variable. Each comes with its part.

Related pages: [types](types.md), [declarations](declarations.md), [objects](objects.md), [library](library.md), [evaluation](evaluation.md), [values](values.md), [facade](facade.md).
