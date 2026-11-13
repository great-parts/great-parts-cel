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
| selection `e.f` | `map(K, V)`: `V`; an object type `T`: the type of the field `f` of `T`; `dyn` or a type parameter: `dyn` (a parameter is bound to `dyn`); `error`: `error`, with no error of its own (a selection over a node with an error is a node with an error, so a call around it is not looked up; cel-go v0.32.0 gives `dyn` there and may report a second error — the outcome is the same, the number of errors differs); any other type: `NoSuchField` at the selection, type `error` | on an object type, the field: the description of `T` and the number of `f` |
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

## Extension sets in the index

`DeclarationIndex` merges the table of the standard library, the surviving sets of the environment and the declarations of the application ([extensions](extensions.md#what-an-environment-keeps-of-the-sets)):

- **overloads** by function name, call style and argument count, as above, in this order: the standard library in the order of its table; the sets by name (ordinal) and version, each in the order of its table; then the application by overload identifier. The order of the candidates, which evaluation uses to dispatch and which decides the type of a call whose candidates differ, does not depend on the order of the builder calls;
- **function names** — the frozen set also holds the names of the sets, qualified ones included: `math.ceil`, `math.@max`, `strings.quote`, `lists.range`, `sets.contains`, `base64.encode`, `json.encode`, `ip.isCanonical`, `cel.@block`, `cel.@mapInsert`. The receiver-call rule of "Name resolution" finds them: in `math.ceil(x)` and `ip.isCanonical(s)` the chain `math` or `ip` is first tried as the prefix of a qualified function name, and when that is declared the call is a global call, even where a variable has the name of the prefix;
- **type identifiers of the sets** — `net.IP` and `net.CIDR`, each an identifier of the static type `type(T)` with its type value, as `int` is. A name is tried as a variable first, so a variable of the same name hides the identifier; a selection chain such as `net.IP` resolves by the longest declared prefix;
- **variables of internal sets**, which none of the sets of this part has.

## Optional syntax and optional types

This section is the part of the checker for the set [optional](extension-optional.md). It supersedes the last sentence of "Optional elements and entries" above ("In this part no expression has an `optional_type` …") and the mention of `optional` selections and indexes in "Not in this part". The type `optional_type(T)` is declared by the set ([extension-optional](extension-optional.md#the-type-optional_typet)), the rules of the types are in [types](types.md#optional-types-in-the-rules), and the syntax option is set by the set: without it the parser refuses every form below with `SyntaxNotEnabled` and the checker never sees them.

**The operand.** For a selection and for the forms below the checker takes the head of the type of the operand after the substitution; when the head is `optional_type(U)` the operand is *optional*, and the rule goes on with the head of `U`. This is the rule of cel-go (`maybeUnwrapOptional`) and the reason why `optional.of(m).c` is not `NoSuchField` for a `map` or an object `m`.

| Form | Rule | Type | Reference |
| :- | :- | :- | :- |
| `e.?f`, the call `_?._(e, 'f')` | the selection rule of the table below for the head of `e`, then wrapped | `optional_type(R)` | the one candidate `select_optional_field`; no field reference |
| `e.f` with an optional operand | the same rule | `optional_type(R)` | none |
| `has(e.f)`, `e.f` a presence test, any operand | the same rule for the existence of the field; the optional is not wrapped | `bool` | none for an optional operand; as before otherwise |
| `e[?k]` | the overloads `list_optindex_optional_int`, `optional_list_optindex_optional_int`, `map_optindex_optional_value`, `optional_map_optindex_optional_value` of the call `_[?_]`, checked as any call; a key of the type `dyn` fits the `int` of the overloads over a list and the key type of those over a map, and the kind of the value decides at run time ([extension-optional](extension-optional.md#indexes)) | the result of the first candidate, `optional_type(V)` | the candidates |
| `e[k]` with an optional operand | the overloads `optional_list_index_int` and `optional_map_index_value` of `_[_]`, after `index_list` and `index_map`: `optional.none()[0]` and `optional.of(m)['k']` match them, `[1][0]` does not | `optional_type(V)` | the candidates |
| `[?e]`, `{?k: v}` | as "Optional elements and entries"; `optional_type(T)` now passes, and gives `T` to the join | as there | — |
| `T{?f: v}` | below | the type of the literal | as in "Message literals of constructible types" |

**The selection rule**, the head `H` of the operand after the unwrapping, with `R` the result before it is wrapped:

| `H` | `R` | Error |
| :- | :- | :- |
| `map(K, V)` | `V` | — |
| an object type | the type of the field `f` of its description | no such field: `NoSuchField` (2004) at the selection, the call `_?._` at its `.`; the type of the node is `error` |
| `dyn` or `error` | `dyn` | — |
| a type parameter | `dyn`, the parameter is bound to `dyn` | — |
| any other type, `optional_type` of an optional included | — | `NoSuchField` as above, with the shown type |

`{'a': 1}.?a` is `optional_type(int)`, `{'a': 1}.?a.?b` is `NoSuchField` for `int` at the second `.?`, `x.?f` with `x` of `dyn` is `optional_type(dyn)`, and `optional.of({'c': 1}).c` is `optional_type(int)`. The `.?` form needs no optional operand: a plain `e.?f` makes any `map` or object an optional, and an optional operand is unwrapped once, not twice. A presence test keeps the type `bool` and is not wrapped, so `has(optional.of(m).c)` is `bool` and its operand is the optional.

**`_?._` and its validator.** `select_optional_field` has a `CallValidator`, as `cel_block_list` has: the second argument must be a string constant, which the parser always gives; a tree that `NodeFactory` built with another argument is `NoMatchingOverload` (2001), with its usual text, after the traversal. The candidates of the call are recorded for the planner, which does not choose among them: there is one. The type of the call is that of the table above and not the `optional_type(V)` of the declaration, which only stands for the identifier of the overload; this is cel-go's own rule for `OptSelect`, and the reason why the field type decides.

**The reference map.** For a selection whose operand is optional, and for the call `_?._`, the checker records no field reference, even when the head is an object type: the description of the held value is known only at evaluation, and the planner reads the type of the operand from the type map to choose the node ([evaluation](evaluation.md#optional-values-and-the-optional-syntax)). The checker does check the name against the description, as above, so `NoSuchField` is a check error and not an evaluation error. A selection with an object operand that is not optional is as before.

**Optional entries of a message literal.** For `T{?f: v}` of a constructible type, after the rules of "Message literals of constructible types", an entry whose field has the type `F` is checked like a plain one with the expected type `optional_type(F)`: `IsAssignable(type of v, optional_type(F))`, so an `optional_type(U)` with `U` assignable to `F` and `dyn` pass, and any other type is `TypeMismatch` (2002) `expected type 'optional_type(F)' but found '{valueType}'` at the entry. A `null` is one of the others: its static type is assignable to `optional_type(F)` by the legacy rule of assignment, but a `null` is no optional, and cel-go checks the value of an optional entry as it checks `[?null]` — an `optional_type` or `dyn`, else `TypeMismatch` — before it looks at the field. So `TestAllTypes{?single_int32: null}` is `TypeMismatch` at the check, as `[?null]` and `{?'a': null}` are, and only a `dyn` value that holds `null` reaches the evaluation, where it is `EvalNoMatchingOverload` with the function `?` ([evaluation](evaluation.md#optional-values-and-the-optional-syntax)). A `dyn` value is checked at evaluation. A message literal with an optional entry has the type of the literal as before.

**What is not new.** A range of the type `optional_type(T)` is `InvalidComprehensionRange` (2005): it is not a list, a map or `dyn`. An operand of `&&` and `||` that is an optional is `TypeMismatch`. `[?1]` and `{?'a': 1}` are one error each, as in "Optional elements and entries"; cel-go goes on with `T` and gives a second error for `[?1] + 1`, which this checker does not. `has(a.?b)` never reaches the checker: the macro `has` refuses an argument that is not a selection, and `a.?b` is a call ([extension-optional](extension-optional.md#the-syntax-and-has)).

**Generalization.** The join of `[optional.none(), optional.of(1)]`, the type of `true ? optional.of(dyn(1)) : optional.of(1)` and the cases of `type_deduction` that the part closes follow the rules of [types](types.md#optional-types-in-the-rules); the checker adds no rule of its own to them.

## Comprehensions of two variables

The macros of two iteration variables create `ComprehensionNode`s with `IterVar2` ([extension-macros](extension-macros.md#the-second-iteration-variable)). The rule for a comprehension above holds with these changes: the range is checked in the enclosing scopes; the scope of the loop declares the first variable, the second one and the accumulator, in this order; and the variables get their types from the range:

| Range | First variable | Second variable |
| :- | :- | :- |
| `list(E)` | `int` | `E` |
| `map(K, V)` | `K` | `V` |
| `dyn`, `error`, a type parameter | `dyn` | `dyn` |
| another type | `error` | `error` — `InvalidComprehensionRange` (2005) at the range |

`[1, 2].all(i, v, v > i)` has `i` of the type `int` and `v` of the type `int`; `{'a': 1}.all(k, v, k == v)` is `NoMatchingOverload` for `_==_(string, int)` at the comparison. Everything else — the loop condition, the step, the result, the type of the comprehension — is as for one variable. A comprehension with two variables over a `map` literal or a variable uses the key and the value the way the expansion reads them; `cel.@mapInsert(map(K, V), K, V)` and `cel.@mapInsert(map(K, V), map(K, V))` are overloads like any other, so the accumulator `{}`, of the type `map(_var0, _var1)`, gets `K` and `V` from the first insertion.

## `decimal` without the set

The set [decimal](extension-decimal.md) needs no rule in the traversal: its overloads are candidates like any other, its identifier `decimal` comes with the abstract type of the set into the declaration index, and `1m + 1`, `price > 0` and `price == 0` for a `price` of type `decimal` have no candidate, `NoMatchingOverload` (2001) at the operator, as the table of the overloads in [extension-decimal](extension-decimal.md#the-18-overloads) implies. Without the set `decimal('1')` and `type(x) == decimal` are `UndeclaredReference` (2000) at the name, as for any name nothing declares; that outcome is the existing one in both targets. This section supersedes the sentence of "Not in this part" that leaves `FieldNotAccessible` with `decimal` to a later part. One case needs a rule: a field of an object type whose CEL type is `decimal`, in an environment that has no set.

**A field of the kind `Decimal`.** A type source can describe a field of type `decimal` on `net11.0` — a generated type, a type built at run time with `CelType.Decimal`, a carrier of `CelObjectTypeBuilder` — without the environment having the set that declares the type. `Build` accepts such a description ([declarations](declarations.md#decimal-without-the-set-and-the-literal-option)): the type is usable only when it is declared, and a selection is where that is found out. `ObjectField`, after it has found the field by its name, asks whether the type of the field mentions the kind `Decimal` at any depth — `decimal`, `list(decimal)`, `map(string, decimal)`, `optional_type(decimal)` — and whether the environment declares it. The environment declares it exactly when its type identifiers have `decimal`, which only the abstract type of a surviving set gives; the type index keeps it as `TypeIndex.DecimalDeclared`, set once when `Build` makes the index, and the checker reads it through `_declarations.Types`. If the type is mentioned and not declared, the selection, and a presence test `has(x.price)` with it, is the error `FieldNotAccessible` (2007) at the selection: `field 'price' of type 'acme.sales.Order' is not available to expressions: the CEL type decimal needs the extension set 'decimal'`. The node has no type and no reference is recorded, as for `NoSuchField`; the rest of the expression is checked as for any error node, so other errors are still reported. Nothing is checked about a field that no expression selects: an object type with a decimal field is a valid description in every environment.

`CheckErrors.FieldNotAccessible(string field, string typeName, string reason, CelSourceLocation location)` is the one constructor of the code, with the message `field '{field}' of type '{type}' is not available to expressions: {reason}`; the generator part uses it for a member that the generator excluded, whose reason comes from `CelObjectType.GetExcludedFieldReason`, and this part for the kind `Decimal`. The part that is merged into `main` first adds it, and the other uses it with that signature. On `net10.0` the kind does not exist, so the generator does not describe a `System.Decimal` member as a field: the member is excluded with the reason that `decimal` needs .NET 11, and the same code comes from the generator's list of reasons.

**Evaluation without type checking.** The planner binds a field by its number and reads it as any other; it asks nothing about the set, so a program that skipped the check ([evaluation](evaluation.md#evaluation-without-type-checking)) reads a decimal field as a value. The outcome without a check is not a contract.

## Calls that the checker validates

An overload binding may carry a `CallValidator` ([extension-format](extension-format.md#check-at-compilation)): a function of a neutral description of a call, `CallShape`, which answers `Ok` or an error code with a detail. The checker keeps for each call whose chosen candidates include such a binding the pair of the node and the binding, and runs them after the traversal of the tree, when it has found no error, in the order of the nodes by offset; each gets the **final** types of its parts, with the substitution applied and the variables that are still free replaced by `dyn`. The validators run before the check against the expected type. Their errors join the list in the order of offsets, with the stage `Check` and the place of the call, or the offset that the answer names; a call that a validator rejected keeps the type that the candidates gave it, since the traversal is over.

```text
namespace Great.Parts.Cel.Declarations
internal delegate CallValidation CallValidator(CallShape call)
internal readonly record struct CallValidation(CelErrorCode Code, string Detail, int? Offset)    // Code = 0: nothing wrong; Offset null: the call
internal readonly record struct CallShape(ValueShape? Receiver, ImmutableArray<ValueShape> Arguments)
internal sealed record ValueShape(
    int Offset,                                                // the offset of its node
    CelType Type,                                              // the final static type
    string? StringConstant,                                    // the value of a string literal, else null
    ImmutableArray<ValueShape>? Elements,                      // the elements of a list literal, else null
    bool HasOptionalElements,                                  // a list literal with an optional element
    ImmutableArray<(ValueShape Key, ValueShape Value)>? Entries)    // the entries of a map literal, else null
```

Two bindings of this part have a validator:

| Overload | What it checks | Code |
| :- | :- | :- |
| `string_format` | a string literal as the receiver and a list literal as the only argument: the format string against the elements ([extension-format](extension-format.md#check-at-compilation)) | `InvalidFormatString` (2010), `invalid format string: {detail}` |
| `cel_block_list` | the first argument is a list literal without optional elements; the planner reads the slots from it ([evaluation](evaluation.md#blocks)) | `NoMatchingOverload` (2001), with its usual text `found no matching overload for 'cel.@block' applied to '({argumentTypes})'`: no overload fits an argument that is not a list literal, and no new code is needed |

`cel.@block` cannot be written in text, so this error is only for a tree built with `NodeFactory`, as the tests of the set do.

**Slot references.** The checker has no rule for `@index`N: it is an identifier like any other. The variables `@index0` to `@index29`, of the type `dyn`, are declared by the internal set `block` together with the macro `cel.block`, and that set is not in the library yet. In this part a tree with slot references is built with `NodeFactory` and planned by `Planner.PlanWithoutTypeCheck` ([evaluation](evaluation.md#blocks)); a tree with `@index`N that goes through the checker is rejected as any undeclared identifier, by the rule of [name resolution](#name-resolution). The type of a block is the type of its second argument, the parameter `T` of `cel.@block`; the type of a slot is the type of its expression.

## The type `error` and the list of errors

A node with an error gets the type `error`, assignable to every type, and produces no further errors; a list, a map, a selection or a call with such a part gets the type `error` too. A selection `e.f` and a presence test over an operand of the type `error` give no error and have the type `error` (`bool` for `has`), so `a.b + 1` with `a` undeclared is the one error of `a`. Checking goes on after an error, so a compilation returns every check error of the text. The list is ordered by offset; errors at one offset keep the order in which the checker found them. This is not always the order of the traversal: a call is checked after its arguments, so `size(1.all(x, true))` gives `NoMatchingOverload` at the call (offset 4) before `InvalidComprehensionRange` at the range (offset 5), though the range fails first. cel-go v0.32.0 orders its issues the same way, stably by line and column. A checked expression exists only when there is no error. All have the stage `Check`.

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

## The type `Any` in the checker

An expression of the type `Any` — a field of a message, a variable declared with `CelType.Any`, a parameter of an application function — has a value whose type is that of the content it holds, which only evaluation knows ([types](types.md#any)). The checker therefore treats `Any` as `dyn` wherever it looks at the type of an operand:

- **Selection and presence** `e.f` and `has(e.f)` with `e` of the type `Any`: the type is `dyn` (and `bool` for `has`), as for `dyn`, and no reference is recorded; the field is found by name at evaluation ([evaluation](evaluation.md#nodes)). It is not `NoSuchField`, which is the answer for a type that cannot have fields.
- **Calls and operators.** An argument of the type `Any` is assignable to every parameter and takes the overloads that a `dyn` argument takes, `dyn`-only overloads included: the rule of "Overload candidates" that lets a `dyn` argument reach an overload marked *only with `dyn`* applies to `Any` as well. A call whose candidates differ in the result type has the type `dyn`.
- **Comprehensions.** A range of the type `Any` is a `dyn` range: the iteration variable and the second one have the type `dyn`.
- **Operands of `&&`, `||` and the condition of `?:`** are assignable to `bool`, since `Any` is assignable to everything; the value is a `bool` or `EvalNoMatchingOverload` at evaluation.
- **Messages** write the type as `any`, in the text form of [types](types.md#any), where a message names a type that contains it: `list(any)`.

## Message literals of constructible types

The first three steps of "Rules per node" — an undeclared name, a name that is no type, a type that is not constructible — hold as they are. The test of the third step is made through the type index: the name is a candidate that the index has as an *object type description*, whatever the `Type` of the description is. The `Kind` of the type that the identifier stands for is no test, since the description of `google.protobuf.Int32Value` has a `wrapper` type ([types](types.md#well-known-names)). The order of the candidates is that of the declaration index, a variable first, then an object type, then a constant.

A description that is constructible has these rules; the field values were already checked in order, as expressions. Each entry is `field: value`, with `field` the name as the parser read it, an escaped name by the text between the backquotes (`` T{`in`: true} ``, `` T{`pkg.ext`: 1} ``):

| Case | Error | Text | Place |
| :- | :- | :- | :- |
| the description has no field of that name and `GetExcludedFieldReason(name)` gives nothing | `NoSuchField` (2004) | `undefined field '{field}' in type '{type}'` | the entry |
| the same, and a reason is given | `FieldNotAccessible` (2007) | `field '{field}' of type '{type}' is not available to expressions: {reason}` | the entry |
| the type of the value is not assignable to the type of the field by `IsAssignable` | `TypeMismatch` (2002) | `expected type '{fieldType}' but found '{valueType}'` | the entry |

- **The place of an entry** is the id of the entry, which the parser places as cel-go does, at the `:` that follows the field name; a literal has no node for the name.
- **The rules of assignment are those of any type**: an `int` is not assignable to a `double` field, `null` to a field of an object type, a `timestamp`, a `duration` and a wrapper, and everything to a `dyn` and an `Any` field; a wrapper type takes its primitive and `null`; a `list(dyn)` — the join of `[1, 'a']` — takes a `repeated` field of any element type, since `dyn` elements are assignable, and fails at evaluation where an element does not fit. `{1: 'uno'}` for a `Struct` field, whose type is `map(string, dyn)`, is `TypeMismatch`.
- **A repeated field** in one literal is not an error; the planner keeps the last value, as [evaluation](evaluation.md#message-literals) says.
- **The type of the literal** is the `Type` of the description: the object type named by the resolved full name for a message, and the CEL type of the name for a known type — `wrapper(int)` for `google.protobuf.Int32Value{value: 1}`, so that `google.protobuf.Int32Value{value: 1} == 1` and `… != null` type check — `dyn` for `Value`, `map(string, dyn)` for `Struct`, `list(dyn)` for `ListValue`, `Any` for `Any`. It is the type even when an entry has an error, so the errors of the rest of the expression are not lost.
- **The reference map** records, for the literal node, the resolved full name and the description; for each entry, by the id of the entry, the number of the field; the planner binds both ([evaluation](evaluation.md#message-literals)). An entry whose field was not found has no reference.
- **Optional entries**, `T{?f: v}`, come with the optional syntax of their part: such a literal is planned by it.

An enumeration constant is an `int`, so `TestAllTypes{standalone_enum: TestAllTypes.NestedEnum.BAZ}` and `TestAllTypes{standalone_enum: 1}` type check, and `TestAllTypes{standalone_enum: 5000000000}` fails at evaluation with `ConversionRange` ([protobuf-messages](protobuf-messages.md#literals)).

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

The expected type comes from `T` of `Compile<T>` by the expected-type table of the type mapping page, or from `CelCompileOptions.ExpectedType`; the facade derives it and rejects an incompatible pair (see [facade](facade.md)). After the traversal, if there is no error, the type of the root must be assignable to the expected type by `IsAssignable`; otherwise compilation fails with `ExpectedTypeMismatch`. The assignment binds the free type variables of the type of the root: `[]` against `list(int)` has the type `list(int)` and `optional.none()` against `optional_type(int)` has the type `optional_type(int)`, which `ResultType` reports; against `dyn` or a type parameter the variables stay free and `ResultType` has `dyn` for them. `dyn` expects anything, and an expression of type `dyn` passes any expected type: its value is converted, or refused with `ResultConversion`, at evaluation.

## The stack and the compilation boundary

Before each recursive step the checker calls `RuntimeHelpers.TryEnsureSufficientExecutionStack()`; `false` is `InsufficientStack` at the node it was about to check, and checking stops there: the compilation returns the errors found so far and this one, ordered by offset as above. `CelEnvironment.Check`, which `Compile<T>` and the internal entry point share, runs the checker inside one handler: an unexpected exception other than `OutOfMemoryException` becomes `InternalError` with the stage `Check` at the node the checker was at, and the result holds it with the errors found before it, in the same order. No node throws `NotImplementedException`.

## Not in this part

`FieldNotAccessible` (2007), with the generator and `decimal`. It comes with its part. The `optional` selections and indexes are in "Optional syntax and optional types" above, which supersedes the mention of them here, and the second iteration variable is in "Comprehensions of two variables".

Related pages: [types](types.md), [declarations](declarations.md), [objects](objects.md), [library](library.md), [evaluation](evaluation.md), [values](values.md), [facade](facade.md).
