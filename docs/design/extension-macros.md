# Macros of extension sets

The parser expands the macros of an environment, and the standard ones are on [syntax](syntax.md#macros). The extension sets bring more: `math.greatest` and `math.least`, `sortBy`, `cel.bind`, and the macros of two iteration variables. This page says how a set puts its macros into the table of the parser, the shape of each expansion with the texts and places of its errors, and what the second iteration variable changes in the tree, the checker and the evaluator. Macros are parser code and are not core ([core](core.md)); the nodes that evaluate their expansions are.

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Syntax/ExtensionMacros.cs` | the macros of the sets as `Macro` objects, grouped by set |
| `src/Great.Parts.Cel/Syntax/Macros.cs` | `MacroTable`, `Macro` and the standard macros, extended as below |
| `src/Great.Parts.Cel/Syntax/ComprehensionNode.cs`, `NodeFactory.cs`, `SyntaxTreeComparer.cs` | the second iteration variable |

## The model

A macro is its key, an applicability rule and an expansion. The key is the name, whether the call has a target, and the number of arguments, or *any* number; `MacroTable.Find(function, argumentCount, isReceiver)` tries the entry with that exact number first and then the entry for any number. `math.greatest` and `math.least` are the only macros for any number of arguments; all others have a number.

`Macro.Check(target, arguments)` gives one of three answers, where a standard macro gave a detail or `null`:

| Answer | Meaning |
| :- | :- |
| not applicable | the call stays a call: the target is not the identifier that the macro needs |
| error with a detail and the node of its place | `InvalidMacroCall` (1006) `invalid call of macro '{macro}': {detail}` at the node the detail names (see the table of errors); `{macro}` is the name of the called function without the receiver — `greatest`, `least`, `bind`, `sortBy`, `all`, `transformMap`; the call is not expanded, and parsing goes on, one error for a call, the first found |
| expand | `Expand(nodes, target, arguments, offset)` creates the nodes |

A macro with a `Namespace` — `math`, `cel` — applies only when the target is an `IdentNode` of that name, as in cel-go; `foo.math.greatest(1)` and `math2.greatest(1)` are not applicable, and checking then looks the call up as any other call. The arguments of the call are expanded before the call itself, and the expansion is not recursive ([syntax](syntax.md#macros)). A macro of a set is expanded only in an environment that has the set; without it `math.greatest(1, 2)` is a call, and the checker reports it as an unknown function.

Every node that an expansion creates has the offset of the `(` of the call, `ParsedExpression` records the call as written under the id of the expansion, and the creation order is the one of the standard macros: the parts in the order `AccuInit`, `LoopCondition`, `LoopStep`, `Result`, each after its children and the arguments of a call from the left, then the comprehension.

## The macro table of an environment

`Build` creates `MacroTable.Create(MacroTable.Standard.Macros ∪ the macros of the surviving sets)` and gives it to the `SyntaxOptions` of the environment. The sets enter in the order of their names and versions, and within a set in the order of its table; the table keeps no order the parser could see, since lookup is by key.

`MacroTable.Create` refuses two macros with one key with `ArgumentException`. No environment reaches this: the sets of the catalog have no common key, and an application set has no macros. A test builds the union of every macro of the catalog with the standard ones and passes the check, so a macro that a later part adds with a key already taken fails there. The macros of two-variable comprehensions with 3 and 4 arguments do not collide with the standard `all`, `exists`, `exists_one`, `map` and `filter` of 2 and 3 arguments: the number of arguments is part of the key.

A function of the application that has the name, the call style and the argument count of a macro of the environment — of the standard library or of a set — would be unreachable, since the parser expands the call. It is a build error ([declarations](declarations.md#extension-sets-in-the-declaration-checks)).

## The macros

`r` is the target, `v` and `w` identifiers, `p`, `f`, `t` expressions. A set that is not added has none of its macros.

| Set | Macro | Applies when | Expansion |
| :- | :- | :- | :- |
| `math` | `math.greatest(a, …)`, `math.least(a, …)` | the target is the identifier `math` | one argument: `math.@max(a)`; two: `math.@max(a, b)`; three or more: `math.@max([a, b, …])`, a list literal of the arguments; `math.@min` for `least`. A global call with the function name `math.@max` |
| `lists` | `r.sortBy(v, k)` | any target | below |
| `bindings` | `cel.bind(v, init, e)` | the target is the identifier `cel` | a comprehension over an empty list: `IterRange` the list `[]`, `IterVar` `#unused`, `AccuVar` `v`, `AccuInit` `init`, `LoopCondition` `false`, `LoopStep` the identifier `v`, `Result` `e` |
| two variables | `r.all(i, v, p)` | any target | `AccuInit` `true`, `LoopCondition` `@not_strictly_false(@result)`, `LoopStep` `@result && p`, `Result` `@result` |
| | `r.exists(i, v, p)` | | `false`, `@not_strictly_false(!@result)`, `@result \|\| p`, `@result` |
| | `r.existsOne(i, v, p)`, `r.exists_one(i, v, p)` | | `0`, `true`, `p ? @result + 1 : @result`, `@result == 1` |
| | `r.transformList(i, v, t)`, `r.transformList(i, v, p, t)` | | `[]`, `true`, `@result + [t]`; with a filter `p ? @result + [t] : @result`; `@result` |
| | `r.transformMap(i, v, t)`, `r.transformMap(i, v, p, t)` | | `{}`, `true`, `cel.@mapInsert(@result, i, t)`; with a filter `p ? cel.@mapInsert(@result, i, t) : @result`; `@result` |
| | `r.transformMapEntry(i, v, t)`, `r.transformMapEntry(i, v, p, t)` | | `{}`, `true`, `cel.@mapInsert(@result, t)`; with a filter as above; `@result` |

All macros of two variables have the node `ComprehensionNode` with `IterVar` `i`, `IterVar2` `v`, `IterRange` `r` and `AccuVar` `@result`. `existsOne` and `exists_one` are two entries with one expansion. `cel.@mapInsert` is a global call with that function name, like `math.@max`.

**`sortBy`.** The expansion binds the target to the hidden name `@__sortBy_input__` and sorts it by the keys of a `map`:

```text
comprehension (IterRange [], IterVar #unused, AccuVar @__sortBy_input__, AccuInit r,
               LoopCondition false, LoopStep @__sortBy_input__,
               Result @__sortBy_input__.@sortByAssociatedKeys(<map>))
<map> = comprehension (IterRange @__sortBy_input__, IterVar v, AccuVar @result, AccuInit [],
                       LoopCondition true, LoopStep @result + [k], Result @result)
```

`<map>` is `@__sortBy_input__.map(v, k)` as the standard macro builds it. The planner records `<map>` as the macro call `map`, so that its cost is that of the standard macro, and it reads the hidden name for nothing by the text of the name (`Planner.Local`); the name begins with `@`, so no text of an expression has it. each name that starts with `@` or `#` is one that no text can write, so the expansion cannot be reached by the expression, and a `sortBy` inside `k` binds its own name in an inner scope that shadows this one.

**Errors of the expansion**, all `InvalidMacroCall` (1006), the text `invalid call of macro '{macro}': {detail}` with the details of cel-go. The place is the node at which cel-go reports, an exception to the rule of the standard macros (`syntax.md` puts their error at the `(` of the call): the argument that has the fault, the receiver for a call of `math.greatest` with no argument and for a `sortBy` on a receiver of a kind it does not take. The checks of a macro run in the order of its rows, and the first failure is the error:

| Macro | Condition | Detail | Place |
| :- | :- | :- | :- |
| `math.greatest`, `math.least` | no argument | `math.greatest() requires at least one argument` | the receiver `math` |
| | one argument that is neither numeric nor a non-empty list literal of numeric elements | `math.greatest() invalid single argument value` | that argument |
| | two or more arguments, one not numeric — the first such | `math.greatest() simple literal arguments must be numeric` | that argument |
| `sortBy` | the receiver is not a list literal, an identifier, a selection, a comprehension or a call | `sortBy can only be applied to a list, identifier, comprehension, call or select expression` | the receiver |
| | the first argument is not an identifier | `argument is not an identifier` | that argument |
| | the first argument is `__result__` or `@result` | `iteration variable overwrites accumulator variable` | that argument |
| `cel.bind` | the first argument is not an identifier | `cel.bind() variable names must be simple identifiers` | that argument: `cel.bind(1, 2, 3)` is offset 9, `cel.bind(a.b, 2, 3)` is 10, the dot of the selection |
| two variables | the first or the second argument is not an identifier — the first checked first | `argument must be a simple name` | that argument |
| | the two names are equal | `duplicate variable name: {name}` | the second argument |
| | the first, then the second name is `__result__` or `@result` | `iteration variable overwrites accumulator variable` | that argument |

*Numeric* is decided by the kind of the node, not by the type: a constant `int`, `uint` or `double`, or any node that is not a constant and not a list, map or message literal — an identifier, a selection, a call (`dyn(5.4)` included), an operator, a comprehension. A constant of another kind (`'a'`, `true`, `null`, `b'x'`), a list, a map and a message literal are not numeric. So `math.greatest(x)` and `math.greatest([dyn(1), 2.5])` expand and leave the types to the overloads of `math.@max`, while `math.greatest([])`, `math.greatest('a')`, `math.greatest([1, 'a'])` and `math.greatest(1, [2])` are errors. A call with the macro name but another number of arguments (`cel.bind(x, 1)`, `[1].transformList(i, v)`) is no macro and stays a call.

## `optional` and `block`

The set [optional](extension-optional.md) brings `optMap` and `optFlatMap`; the internal set `block` ([extensions](extensions.md#the-sets-optional-and-block)) brings four macros that only the runner of the cel-spec cases has. They join the table above:

| Set | Macro | Applies when | Expansion |
| :- | :- | :- | :- |
| `optional` | `r.optMap(v, e)` | any target, two arguments | below |
| | `r.optFlatMap(v, e)` | any target, two arguments | below |
| `block` | `cel.block(l, e)` | the target is the identifier `cel`, two arguments | the global call `cel.@block(l, e)` |
| | `cel.index(N)` | the target is `cel`, one argument | the identifier `@index` and the digits of `N` |
| | `cel.iterVar(N, M)` | the target is `cel`, two arguments | the identifier `@it:N:M` |
| | `cel.accuVar(N, M)` | the target is `cel`, two arguments | the identifier `@ac:N:M` |

A call with another number of arguments is no macro and stays a call: `x.optMap(v)` and `cel.index(1, 2)`. Without the set a macro is an ordinary call, and the checker reports it ([extensions](extensions.md#the-sets-optional-and-block)). Macros of the two sets have no key in common with each other or with the earlier ones: `cel.bind` and `cel.block` are different names, `optMap` and `optFlatMap` have the receiver form with two arguments that no standard macro has by name.

**`optMap` and `optFlatMap`.** `r` is the target, `v` an identifier, `e` an expression. For an identifier target `x`, the tree is a conditional over the optional:

```text
x.optMap(v, e)  -->  x.hasValue() ? optional.of(<c>) : optional.none()
x.optFlatMap(v, e)  -->  x.hasValue() ? <c> : optional.none()
<c> = comprehension (IterRange [], IterVar #unused, AccuVar v, AccuInit x.value(),
                     LoopCondition false, LoopStep v, Result e)
```

For any other target `t` the target is evaluated once and bound to the hidden name `@target`, which no text can write: the whole is one more comprehension over the empty list,

```text
comprehension (IterRange [], IterVar #unused, AccuVar @target, AccuInit t,
               LoopCondition false, LoopStep @target, Result <the conditional above with x written @target>)
```

`hasValue`, `value` are receiver calls and `optional.of`, `optional.none` global calls with those function names, as `math.@max` is: the checker finds them among the qualified function names of the index. `<c>` binds `v` to the held value as the accumulator, and no step runs; `e` is checked and evaluated with `v` in scope, so `optional.of(1).optMap(y, y + 1)` is `optional.of(2)` and `optional.none().optMap(y, y + 1)` is `optional.none()`, and `e` is not evaluated for an empty optional. The type is that of the conditional: `optional_type(R)` for `e` of the type `R`, and for `optFlatMap` the type of `e`, which has to be an optional too, else the branches have no common type and the check gives `NoMatchingOverload` for `_?_:_`.

**Order of creation.** The nodes are created in the order in which cel-go builds them, each with the offset of the `(` of the call: for a target that is not an identifier the identifier `@target`; the call `hasValue`; the empty list of the inner comprehension, the copy of the target identifier — a new node, not the target — and the call `value`, the literal `false`, the identifier `v` of the step, the inner comprehension; the call `optional.of` for `optMap`; the call `optional.none`; the conditional; and for a target that is not an identifier the empty list, the literal `false`, the identifier `@target` and the outer comprehension. `ParsedExpression` records the call as written under the id of the expansion, as for the other macros.

**Errors**, `InvalidMacroCall` (1006) `invalid call of macro '{macro}': {detail}`, `{macro}` the name of the called macro, at the first argument; the first argument that is not an identifier is the only fault a macro checks:

| Macro | Detail |
| :- | :- |
| `optMap` | `optMap() variable name must be a simple identifier` |
| `optFlatMap` | `optFlatMap() variable name must be a simple identifier` |

**Cost** by the rules of the expansion ([library](library.md#cost-of-optional-and-block)): the target and the body are the expressions of the author, `c(t)` and `c(e)`; everything that the expansion adds — the hidden `@target`, the copy of the target identifier, the identifier `v` of the step — is a read that costs nothing; each call, `hasValue`, `value`, `optional.of`, `optional.none`, is 1, the empty list that is the range of a comprehension is 1 although the planner creates none, and no step runs.

| Macro | Target | Empty optional | Optional with a value |
| :- | :- | :- | :- |
| `optMap` | an identifier | c(t) + 2 | c(t) + 4 + c(e) |
| `optMap` | any other | c(t) + 3 | c(t) + 5 + c(e) |
| `optFlatMap` | an identifier | c(t) + 2 | c(t) + 3 + c(e) |
| `optFlatMap` | any other | c(t) + 3 | c(t) + 4 + c(e) |

`x.optMap(y, y + 1)` with `x` bound to `optional.of(1)` is 7: the read of `x` 1, `hasValue` 1, `optional.of` 1, the empty list 1, `value` 1, and the body 2 (the read of `y` and the sum); with `x` bound to `optional.none()` it is 3: the read of `x`, `hasValue`, `optional.none`. `optional.of(42).optMap(y, y + 1)` has a target that is not an identifier, whose cost is the call 1: the outer empty list 1, the target 1, `hasValue`, `optional.of`, the inner empty list and `value`, 4, and the body 2, which is 8. The planner recognizes the nodes that the author wrote by the call that `ParsedExpression` records, as for `cel.bind`.

**The macros of `block`**, all with the namespace `cel` as `cel.bind` has, and all in the table of an environment that has the set `block`, which only the runner has. The errors are `InvalidMacroCall` with `{macro}` `block`, `index`, `iterVar` and `accuVar`; a *number* is an `int` literal with a value of at least 0, and `1u`, `1.0`, `-1` and a name are not numbers:

| Macro | Condition | Detail | Place |
| :- | :- | :- | :- |
| `cel.block` | the first argument is not a list literal | `cel.block requires the first arg to be a list literal` | that argument |
| `cel.index` | the argument is not a number | `cel.index requires a single non-negative int constant arg` | that argument |
| `cel.iterVar` | an argument is not a number — the first such | `cel.iterVar requires two non-negative int constant args` | that argument |
| `cel.accuVar` | an argument is not a number — the first such | `cel.accuVar requires two non-negative int constant args` | that argument |

`cel.block([1, cel.index(0) + 1], cel.index(1))` is `cel.@block([1, @index0 + 1], @index1)` and `cel.iterVar(1, 2)` the identifier `@it:1:2`; the digits are those of the value, so `cel.index(007)` is `@index7` and `cel.index(0x10)` is `@index16`. The arguments of a macro are expanded before it, so `cel.iterVar(0, 0)` as the iteration variable of `exists` or `map` is already an identifier and the standard macro takes it; the hidden accumulator `@result` and its name `__result__` are the only names that a macro of iteration refuses. A list literal with an optional element passes the macro and is refused after the traversal by the validator of `cel_block_list` ([checking](checking.md#calls-that-the-checker-validates)). The cost of `cel.block` is that of `cel.@block` and of the reads that the author wrote: a slot is read once per entry of the block, and each `cel.index(N)` is the read of a variable, 1.

## The second iteration variable

`ComprehensionNode` has the new property `string? IterVar2`, `null` for every node that a macro of one variable creates. `NodeFactory.Comprehension` has an overload with the second name; the old one passes `null`, so no code of the standard macros changes. `SyntaxTreeComparer` compares `IterVar2` as it compares `IterVar`: `null` equals only `null`. `Printer` prints a comprehension as its recorded macro call and needs nothing more; a tree without the record throws as before. Nodes of the tree reach the planner only through the factory, so the node classes carry nothing else.

**Checking.** The scope of the loop declares both names, then the accumulator ([checking](checking.md#rules-per-node)); a comprehension of two variables types them by the range:

| Range | First variable | Second variable |
| :- | :- | :- |
| `list(E)` | `int`, the index | `E` |
| `map(K, V)` | `K`, the key | `V` |
| `dyn`, `error`, a type parameter | `dyn` | `dyn` |
| another type | `error`, with `InvalidComprehensionRange` (2005) at the range | `error` |

Both names shadow every declaration, and a name equal to an enclosing iteration variable shadows it for the body. The loop condition, the step and the result are checked as for one variable.

**Evaluation.** The planner gives such a comprehension three slots: the two iteration variables and the accumulator ([evaluation](evaluation.md#comprehensions-of-two-variables)).

## Cost

The cost of every macro is that of its expansion by the rules of the cost table, and the rows are in [library](library.md#cost-of-the-extension-sets). In short, the expressions the author wrote cost by their own rows; each executed step costs 1; the hidden accumulators cost nothing to read; the empty list that stands as the range of `cel.bind` and `sortBy` costs 1, charged also when the planner does not create it; `transformList` charges 1 for the empty list and 1 for each element it adds, `transformMap` and `transformMapEntry` 1 for the empty map and 2 for each pair they insert, with ⌈L/10⌉ more for a string key of length L.

Related pages: [syntax](syntax.md), [checking](checking.md), [evaluation](evaluation.md), [extensions](extensions.md), [library](library.md).
