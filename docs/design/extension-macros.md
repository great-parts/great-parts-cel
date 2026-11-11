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
