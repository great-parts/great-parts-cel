# Unknown values, partial evaluation and the residual expression

A program compiled with `CelCompileOptions.PartialEvaluation` evaluates with parts of the data unknown: `CelProgram<T>.EvaluatePartial(bindings, patterns)` gives the value, the errors, or the unknown parts the result depends on (`CelPartialEvalResult<T>`, `CelUnknown`), and `GetResidual()` gives the expression with the known parts replaced by their values. Members and documentation are in the XML documentation; this page describes the implementation. The asynchronous driver, which uses the same unknown values for calls not yet made, is on [async-evaluation](async-evaluation.md).

## Unknown values in the error channel

An unknown value is a `CelValue` of the kind `Error` whose payload is an internal `UnknownSet` (`Values/UnknownSet.cs`) instead of a `CelError`: the ordered entries, one for each id of the node where values became unknown, and for each entry either the trail of an attribute (a variable and the qualifiers of the access, none of which matches any) or the description of an asynchronous call (function, overload, arguments). `UnknownSet` is immutable; `Merge(a, b)` returns the union and reuses an operand when the other adds nothing. `CelValue.TryGetError` returns `false` for an unknown value; the internal `IsUnknown` and `TryGetUnknown` test the payload.

`Evaluation` and `Values` cannot reference the public `CelUnknown` and `CelAttributePattern` of the root namespace ([layers](core.md#layout-of-srcgreatpartscel)): inside they use the internal `UnknownEntry` and `UnknownPattern`, and the facade translates the entries of a result into `CelUnknown` (with the span of the node from the tree) and the patterns of a call into `UnknownPattern`.

Every node already returns an `Error` operand as its result without doing its operation, so unknown values propagate with no new code on the path of values. New code is only in the branches that handle `Error`:

| Place | Rule | Code |
| :- | :- | :- |
| Strict call: `PlanNode.TryPushArguments` and the nodes that evaluate operands themselves: the literals of lists, maps (`MapPlanNode`, `OptionalMapPlanNode`) and messages, `OptionalListPlanNode`, `AppendPlanNode` and `MapInsertPlanNode` (the steps of `map`, `filter` and `transformMap`) | At an `Error` argument that is unknown, keep evaluating the remaining arguments: the first real error wins; otherwise the union of the unknowns. A real error found first returns at once, as before. An unknown accumulator of `AppendPlanNode` and `MapInsertPlanNode` does not stop the element from being evaluated | `IsUnknown` inside the existing `Kind == Error` branch and `PlanNode.Defer` |
| `LogicalPlanNode` (`&&`, `\|\|` chains) | A deciding operand wins, as before; otherwise, when any operand is unknown, the union of the unknowns; otherwise the first error or `NoMatchingOverload`, as before | `LogicalPlanNode.Failure`, reached only when no operand decided and some failed |
| `ConditionalPlanNode`, comprehension range, `NotStrictlyFalsePlanNode` | An unknown condition or range is the result; `@not_strictly_false` gives `true` for an unknown, as for an error | none: the existing `Error` handling applies |
| `OptionalSelectPlanNode`, `OptionalOrPlanNode` | An unknown operand is the result | none |
| `EvaluationErrors.Placed`, the result check of an application function (`Invocation.CheckResult`, `TypeFit.ElementError`), `ConversionRules.TryToResult` | Pass an unknown value through unchanged: never place it, wrap it or report it as an error | audited |

An error to the right of an unknown operand is the result of a strict call, as the language definition says: `x + 1 / 0` with `x` unknown is `DivisionByZero`. cel-go returns the unknown for a binary function and the error for a function of three or more arguments. The same rule makes `fail(1) + 1 / 0`, where `fail` is an asynchronous function whose call is not made yet, `DivisionByZero` at the first pass, while the synchronous evaluation gives the error of `fail`.

**Cost.** An operation not done because of an unknown argument charges nothing for itself, as with an error argument. An unknown attribute charges 1.

## Attribute patterns

The planner of a program compiled with `PartialEvaluation` (`PartialPlanner`) wraps the outermost node of each attribute chain in an `AttributePlanNode`. An attribute chain is a variable read with the selections, optional selections, indexes, optional indexes and presence tests applied to it, as far as each node applies to the result of the previous one. `AttributeShape` finds the chain statically: the full name of the variable (after container resolution), and for each qualifier either a constant (field name, constant key) or the plan node of the key expression. Locals of comprehensions, `cel.bind` and `@block` slots are not variables and start no chain.

`AttributePlanNode.Evaluate`:

1. if the frame has no partial state, or no pattern names the variable, evaluate the wrapped node;
2. evaluate the keys that are expressions, from left to right, each through its `KeyPlanNode`; an error or an unknown is the result; each value is stored in the `PartialState` of the frame under the slot of its key, and the wrapped nodes of the chain take it from there instead of evaluating the key again;
3. match the patterns in the order given: the variable name equal, then each qualifier that both have — a string pattern equals a field name or a string key; numbers of the kinds `int`, `uint`, `double` by CEL equality; `null` matches any. The first matching pattern gives an unknown with the trail of the access up to the length of the pattern, the id of the chain node at that length, or of the last node when the access is shorter, and charges 1;
4. no match: evaluate the wrapped chain, which reads the stored keys.

Patterns are matched by a linear scan. The number of patterns is small in practice.

**A chain that starts at a conditional expression.** In `(c ? a : b).f[k]` cel-go appends the qualifiers `.f` and `[k]` to both branches (`conditionalAttribute.AddQualifier`, recursively when a branch is a conditional expression itself), so the access that a pattern meets is that of the branch the condition chooses, with the qualifiers after it. A chain with a qualifier applied to a conditional expression is therefore one chain, and its outermost node is a `ConditionalAttributePlanNode`; a branch is described to it (`AttributeSource`: an attribute of a variable, a conditional expression of its own, or neither) and is not wrapped in an `AttributePlanNode` itself, which would match the short access of the branch alone. The node evaluates as follows:

1. if the frame has no partial state, or no pattern names the variable of any branch, evaluate the wrapped node;
2. evaluate the condition once, through its `KeyPlanNode`, and store the value in the partial state, where the `ConditionalPlanNode` of the wrapped chain takes it; a condition that is not a `bool` is left to the wrapped node, which returns the unknown value, the error or `NoMatchingOverload` as `_?_:_` does;
3. take the branch the condition chooses, and as `AttributePlanNode` does evaluate the keys that are expressions from left to right (an error or an unknown is the result), first those of the branch, then those of the qualifiers of the chain, with the nested conditions of a branch that is a conditional expression evaluated first; a chosen branch that is no attribute of a variable with a pattern has no access;
4. match the patterns against the access of the branch followed by the qualifiers of the chain, as above, with the id of the chain node at the matched length; a match is an unknown that charges 1, otherwise the wrapped chain is evaluated and reads the stored values.

The condition and the keys are evaluated, and charged, once. A plan without the flag has none of these nodes.

## Partial programs and their evaluation

`CelCompileOptions.PartialEvaluation` is a field of the key of the compilation cache next to the expected type and the source location. A partial program keeps:

- the plan with `AttributePlanNode`, `ConditionalAttributePlanNode`, `KeyPlanNode` and `RecordPlanNode(id, inner)` wrappers (`Plan.Tree` and `Plan.KeyCount` are set only for it); the classes of the synchronous plan get no new field and a plan made without the flag contains none of these nodes, so the memory of a synchronous plan and of a cache entry does not change;
- the parsed expression (`CelParsedExpr`) and the source text its positions belong to.

`EvaluatePartial` rents a frame as `Evaluate` does and sets `Frame.Partial` to a `PartialState`: the pattern array, the slots of the keys and the record of values, a `CelValue[]` indexed by node id and sized by `CelParsedExpr.IdCount`, rented from `ArrayPool<CelValue>` and returned before the call returns. Each `RecordPlanNode` writes the value of its node after the inner node evaluates, unless it is an error or an unknown; the partial planner puts no wrapper inside the loop condition and the loop step of a comprehension (the range and the result are wrapped). `PartialState.Release` returns the written ids and values as a compact sorted `ValueRecord`, which the result keeps for the residual. A synchronous evaluation never sets `Frame.Partial`, so every node that serves partial evaluation tests one reference and does nothing.

`CelPartialEvalResult<T>` is built from the root value: a value converts as in `CelProgram<T>.Run`; an error is the one error; an unknown gives `Unknowns`, one `CelUnknown` per entry of the set. `EvaluatePartial` of a program that an asynchronous call reaches gives an unknown that describes the call.

## The residual expression

`GetResidual()` is `ResidualSource` (`src/Great.Parts.Cel/ResidualSource.cs`, a file of the root: it needs the text of `timestamp` and `duration` from `Library.Time`). It follows `PruneAst` of cel-go (`interpreter/prune.go`) on the tree of the program and the recorded values, building the new tree with `new CelExprFactory(lines)` and `Copy(tree, root, replace)`:

- `replace(node)` returns a node of the factory when the node has a recorded value with a written form, with `Position` set to the span of the node: `Constant` for `null`, `bool`, `int`, `uint`, `string`, `bytes` and finite `double`; `Call("double", Constant("NaN"|"Infinity"|"-Infinity"))`; `Call("timestamp" | "duration", Constant(string(v)))`; `Call("decimal", Constant(text))`; `Call("date" | "time_of_day", Constant(string(v)))` for the values of the `calendar` set; `Call("optional.of", …)` and `Call("optional.none")`; `List` and `Map` of written values in iteration order. `char` and the integers wider than 64 bits are `string`, `int` and `uint` values in CEL and are written as those. Objects, messages, types, abstract values and collections that contain them have no written form, and the node is copied. The calls with a written form compile only in an environment with the set that declares them, as `decimal` and `optional.of` do;
- calls with a known operand shrink as `maybePruneFunction` of cel-go: `&&` with a known `false` becomes `false`, with a known `true` the other operand; `||` symmetrically; `?:` with a known condition the chosen branch; `!` with a known operand a constant; `in` with an empty list literal `false` — the literal is empty by its text, whether it was evaluated or not;
- a comprehension: only the range is replaced; the nodes of the loop and of its result are copied (the record keeps the last value of a node, not the value of every step), and `cel.bind` has no rule of its own;
- `Copy` drops the macro call of a replaced node and keeps the others, as the public tree defines it.

The residual is a property, not a form: compiled in the same environment and evaluated on the full data, it gives the result of the program or an error with the same code, except an error of a branch that the residual no longer has. The ids are new.

## Comparison with cel-go

`eng/oracle` answers the request `partial` with cel-go v0.33.4 (`cel.OptPartialEval`, `cel.PartialVars`), and `eng/difftest` builds the patterns of each case from the attributes of its expression and compares the class of outcome, the value and the set of trails, without node ids ([eng-oracle](eng-oracle.md#protocol), [eng-difftest](eng-difftest.md#partial-evaluation)). The differences by design are in the XML documentation and the registry of known differences: an error to the right of an unknown in a binary function (above), the choice of the first matching pattern by the order given where cel-go picks at random, and the residual as a new tree with new ids where cel-go prints and parses the expression again.
