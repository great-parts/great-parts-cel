# Evaluation: planner and evaluator

`Great.Parts.Cel.Evaluation` turns a checked expression into a plan once, at compilation, and evaluates the plan on bindings at each call. A plan is a tree of immutable nodes; the state of one evaluation lives in a frame created for that call. There is no compilation to expression trees, byte code or closures.

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Evaluation/Planner.cs` | plans a checked expression, or a parsed one without type checking |
| `src/Great.Parts.Cel/Evaluation/Plan.cs` | the root node, the variable slots, the line table; the entry of an evaluation |
| `src/Great.Parts.Cel/Evaluation/PlanNode.cs` and one file per node class | the nodes |
| `src/Great.Parts.Cel/Evaluation/Frame.cs` | the state of one evaluation |
| `src/Great.Parts.Cel/Evaluation/KindMatch.cs` | whether argument values fit the parameter types of an overload |
| `src/Great.Parts.Cel/Evaluation/EvaluationErrors.cs` | the messages of the evaluation errors |

## Plan nodes

`PlanNode` is abstract: it keeps the offset of its syntax node and has one method, `CelValue Evaluate(Frame frame)`. A node never changes after planning, so one plan serves any number of threads at once.

| Node | Planned from | Evaluation | Cost, charged before the operation |
| :- | :- | :- | :- |
| `ConstantPlanNode` | a literal | returns the value of the literal | 0 |
| `VariablePlanNode` | an identifier | reads the slot of the variable (see below) | 1 |
| `CallPlanNode` | a call with one candidate | evaluates the arguments and calls the implementation of the candidate | `FixedCost` of the overload plus its `ArgumentCost` |
| `DispatchPlanNode` | a call with several candidates, or none | evaluates the arguments and chooses an overload by the kinds of the values | as for the chosen overload |
| `OrPlanNode` | a chain of `_\|\|_` calls | evaluates the operands as one node | 0 beyond the operands |
| `ListPlanNode` | a list literal | evaluates the elements, creates a list | 1 + n for n elements |
| `MapPlanNode` | a map literal | evaluates the entries, creates a map in the order of insertion | 1 + n for n entries, and ⌈L/10⌉ for each string key of length L |

The planner gives each distinct variable of the expression a slot number, and every identifier of that variable reads the same slot. The parser builds a chain of `||` as a balanced tree of `_||_` calls; the planner flattens the nested calls into one `OrPlanNode` with all operands in the order of the text and the position of the outermost call.

## Frame

`Plan.Evaluate` receives the values of the bindings as a read-only dictionary by name — the facade passes the dictionary inside `CelBindings`, a type that this namespace cannot refer to — creates a `Frame`, evaluates the root and returns its value or error value. The frame holds:

- the slots: a value and a filled flag per variable. The first read of a slot looks the name up in the bindings and keeps the value; later reads of the same variable take it from the slot;
- an argument stack, which calls use instead of an array per call; a call pushes its argument values and gives the implementation a span over them;
- the cost meter and the cancellation counter, which come after the walking skeleton.

## Calls: from candidates to an implementation

Type checking records the candidates of each call in the reference map (see [checking](checking.md)); each candidate is an `OverloadBinding` with its implementation (see [library](library.md)).

1. The planner plans a call of `_||_` as part of an `OrPlanNode`. Any other call with one candidate becomes a `CallPlanNode` bound to that candidate's implementation; a call with several candidates becomes a `DispatchPlanNode` with all of them.
2. Both nodes evaluate the arguments from left to right. Calls are strict: the first argument that is an error value is the value of the call, and no implementation runs.
3. A `CallPlanNode` whose arguments all have the parameter types statically calls the implementation directly. When an argument has the static type `dyn`, the node first checks with `KindMatch` that the kinds of the values fit the parameter types; a mismatch is the error `EvalNoMatchingOverload`.
4. A `DispatchPlanNode` calls the first candidate, in the order of the reference map, whose parameter types fit the kinds of the values. `dyn` and type parameters fit any kind, `list(…)` fits a list, and a primitive type fits a value of the same kind. When no candidate fits, the call is `EvalNoMatchingOverload` at the position of the call.

So evaluation calls only an overload that type checking accepted.

## Errors as values

An error inside evaluation is a value of the kind `Error` with a `CelError` of the stage `Evaluate`; nodes return it like any value. Its position is the offset of the node that failed, turned into a line and a column with the line table of the plan.

| Code | Where |
| :- | :- |
| `EvalNoMatchingOverload` (3000) | no overload fits the kinds of the values; a chain of `\|\|` with an operand that is not a `bool`, when no operand is `true`: `{function}` is `_\|\|_`, `{argumentTypes}` the types of all operands of the chain in order |
| `Unbound` (3009) | a variable without a value in the bindings, at its identifier: `no value is bound to variable '{name}'` |
| `DuplicateMapKey` (3010) | a map literal with two keys equal by CEL equality, such as `1` and `1u`, at the literal |
| `UnsupportedMapKeyType` (3027) | a key of a map literal that is not an `int`, `uint`, `bool` or `string`, at the key; checked before `DuplicateMapKey` |

The errors of the standard library functions are in [library](library.md).

`OrPlanNode` evaluates its operands from left to right and returns `true` at the first operand that is `true`, without evaluating the rest. Otherwise the first operand that is an error value, or that is not a `bool` and so gives `EvalNoMatchingOverload`, is the value of the chain; when all operands are `false`, it is `false`. So `x || true` is `true` even when `x` is unbound. List and map literals are strict: the first error value of an element or an entry is the value of the literal.

When the root value is an error, the facade returns it as the single error of the result.

## Cost

Each node charges its cost from the table above, which follows the cost table of the documentation, before its operation runs.

- The frame has a cost meter only when the evaluation options set a limit. Without a limit nothing is counted, and a node only tests one flag of the frame.
- When the total after a charge would be greater than the limit, the operation does not run, and the evaluation ends with `CostLimitExceeded` at the node of that operation. A total equal to the limit is allowed; a total that does not fit the counter exceeds any limit.
- Exceeding the limit is not an error value: `||` would absorb it and let the evaluation go on. The meter throws an internal exception, and `Plan.Evaluate` turns it into the single error of the evaluation.

## Evaluation without type checking

`Planner.PlanWithoutTypeCheck(ParsedExpression, DeclarationIndex)` serves the internal entry point `CelEnvironment.EvaluateWithoutTypeCheck` (see [facade](facade.md)). It plans the parsed tree with the same nodes and no checked expression:

- every identifier is a variable read by name: any bound name has a value, an unbound name is `Unbound`;
- every call of `_||_` is part of an `OrPlanNode`; any other call becomes a `DispatchPlanNode` over all overloads of its name and argument count in the declaration index. A function without such overloads gives a `DispatchPlanNode` with no candidates, which is `EvalNoMatchingOverload` once the arguments are evaluated without error.

So `f_unknown(17)` evaluates to `EvalNoMatchingOverload`, and `x` without a binding to `Unbound`. The value of the root is returned as it is, without conversion to a .NET type.

## Walking skeleton and later parts

| Part | Walking skeleton | Later, with the core of evaluation |
| :- | :- | :- |
| nodes | constant, variable, call with one candidate, dispatch without candidates or with one, `\|\|` chain, list, map | field selection, index, `&&`, `?:`, macro loops with an accumulator slot, constant folding |
| frame | slots, argument stack | cost meter, cancellation counter |
| calls | overloads of the standard library | application functions, with the exception boundary `ApplicationException` |
| errors | 3000, 3009, 3010, 3027 and the library errors | the check of a binding against the declared type, `InsufficientStack` before each recursive step, `InternalError` |
| cost | costs are in the tables | the meter and `CostLimitExceeded` |
| cancellation | — | events are macro steps and application calls; the token is checked before the first event and after every 64 |

In the walking skeleton a cost limit in `CelEvalOptions` or a cancellation token that can be canceled throws `NotImplementedException`, and so do a non-null `CelCompileOptions` and a `T` outside the result table of the facade. They are not ignored: an untrusted expression evaluated without the limit that the application believes is set would be worse than an exception.

Related pages: [checking](checking.md), [library](library.md), [values](values.md), [facade](facade.md), [core](core.md).
