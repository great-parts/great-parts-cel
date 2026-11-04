# Evaluation: planner and evaluator

`Great.Parts.Cel.Evaluation` turns a checked expression into a plan once, at compilation, and evaluates the plan on bindings at each call. A plan is a tree of immutable nodes; the state of one evaluation lives in a frame created for that call. There is no compilation to expression trees, byte code or closures.

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Evaluation/Planner.cs` | plans a checked expression, or a parsed one without type checking |
| `src/Great.Parts.Cel/Evaluation/Plan.cs` | the root node, the slots, the line table; `Evaluate(bindings, costLimit, cancellationToken)` |
| `src/Great.Parts.Cel/Evaluation/PlanNode.cs` and one file per node class | the nodes |
| `src/Great.Parts.Cel/Evaluation/Frame.cs` | the state of one evaluation |
| `src/Great.Parts.Cel/Evaluation/CostLimitException.cs` | the internal signal of an exceeded cost limit |
| `src/Great.Parts.Cel/Evaluation/EvaluationErrors.cs` | the messages of the evaluation errors |
| `src/Great.Parts.Cel/Values/IEvaluationMeter.cs` | `Charge(ulong units)` and `Event()`: how library implementations and value rules reach the frame; it lives in `Values`, the lowest namespace that needs it |

## Plan nodes

`PlanNode` is abstract: it keeps the offset of its syntax node and has one method, `CelValue Evaluate(Frame frame)`. A node never changes after planning, so one plan serves any number of threads at once. Every node charges its cost before its own operation, after its children; the costs are those of the cost table of the documentation.

| Node | Planned from | Cost |
| :- | :- | :- |
| `ConstantPlanNode` | a literal; a type identifier, whose type value the planner takes from the declaration index | literal 0; type identifier 1, as a name read |
| `VariablePlanNode` | an identifier or a selection that names a declared variable | 1; at the first read also the check of the binding ([values](values.md), "Fits a type") |
| `LocalPlanNode` | an iteration variable or the accumulator of a comprehension | 1; see comprehensions for the reads that cost 0 |
| `SelectPlanNode` | a selection `e.f` that names no declaration; `has(e.f)` | 1 + ⌈L/10⌉ on a map, L the length of `f`; 1 otherwise |
| `CallPlanNode`, `DispatchPlanNode` | a call with one candidate; with several, or none | the cost of the overload |
| `AndPlanNode`, `OrPlanNode` | a chain of `_&&_` or of `_\|\|_` calls | 0 beyond the operands |
| `ConditionalPlanNode` | `_?_:_` | 0 beyond the operands |
| `NotStrictlyFalsePlanNode` | `@not_strictly_false` | 0 |
| `ListPlanNode` | a list literal | 1 + n for n elements |
| `MapPlanNode` | a map literal | 1 + 2n for n entries, a key and a value each, and ⌈L/10⌉ for each string key of length L |
| `ComprehensionPlanNode` | a comprehension | 1 per executed step |
| `AppendPlanNode` | the step `@result + [e]` of `map` and `filter` | 1 per added element |

The planner gives each distinct variable of the expression a slot, and each comprehension two more, for its iteration variable and its accumulator; every reference reads its slot. The parser builds chains of `&&` and `||` as balanced trees; the planner flattens each into one node with the operands in the order of the text and the position of the outermost call. An index `e[i]` is the call `_[_]` of the library.

## Nodes

**Variable.** The first read of a slot looks the name up in the bindings, checks the value with `ValueTypeRules.Fits` against the declared type of the variable at any depth ([values](values.md)) and keeps the value, or the error `BindingTypeMismatch`; later reads take it from the slot and check nothing. No value is `Unbound`. So a list of strings bound to a variable of type `list(int)` is `BindingTypeMismatch` at the first read of the variable, whatever the expression does with it — `size(x)` included — and never reaches an overload or the result conversion.

**Selection and presence.** The operand comes first; an error value is the result. On a map, `e.f` is the value of the string key `f`, and no such key is `EvalNoSuchField`; `has(e.f)` is whether the key is present. Any other value, which only an operand of static type `dyn` or evaluation without type checking can give, is `FieldSelectionNotSupported` for both, after the constant unit only. For `has()` the language definition requires the error ("Field Selection", case 5). cel-go v0.32.0 returns `false` only without `EnableErrorOnBadPresenceTest`, which the oracle and the cel-go conformance runner set; with it both sides fail, so there is no difference.

**`&&` and `||`.** The operands are evaluated from left to right, and the first operand that decides the result — `false` for `&&`, `true` for `||` — is the result, without evaluating the rest. Otherwise the first operand that is an error value, or that is not a `bool` and so gives `EvalNoMatchingOverload` with all operand types of the chain (`no such overload: _&&_(int, bool)`), is the result; when no operand decides it and none fails, the result is `true` for `&&` and `false` for `||`. So `x || true` is `true` and `x && false` is `false` even when `x` is unbound.

**`?:`.** The condition first: `true` evaluates only the second operand, `false` only the third; an error value is the result; another kind is `EvalNoMatchingOverload` with the type of the condition (`no such overload: _?_:_(int)`).

**`@not_strictly_false`.** A `bool` operand is the result; any other value, an error included, is `true`.

## Comprehensions

`ComprehensionPlanNode` evaluates the range first: an error value is the result; a list gives its elements in order and a map its keys in the order of the map; another value is `EvalInvalidComprehensionRange` at the range. The node then evaluates the accumulator initializer into the accumulator slot and, for each element, puts it into the slot of the iteration variable, evaluates the loop condition and stops when the condition is `false`; otherwise it counts an event (see cancellation), charges 1 unit and evaluates the step into the accumulator slot. The result is evaluated after the loop. There is no early exit on an error value: the accumulator holds whatever the step gives, as the expansion of the macro says (see [syntax](syntax.md), macros), so `all` and `exists` absorb errors by the rules of `&&` and `||`, and `exists_one`, `map` and `filter` keep them.

The standard macros expand with the hidden accumulator `@result`, which no text can write, so the planner recognizes their parts. The cost of a standard macro is the row of the cost table: the range, the predicate and the transform — the expressions the author wrote — are charged by their rows; each executed step costs 1; `[]` as the initializer of `map` and `filter` is a list literal of 1 unit, the other initializers are literals of 0; each element that `map` or `filter` adds costs 1. Everything else the expansion adds — the reads of `@result`, the loop condition with `@not_strictly_false` and `!`, the `&&`, `||` and `?:` of the step, the `+ 1` of `exists_one`, the result, and the read of the iteration variable that `filter` adds to its list — is part of the step and costs nothing.

The step `@result + [e]` of `map` and `filter` becomes an `AppendPlanNode`: during the loop the accumulator slot holds a list builder, and the node evaluates `e` and appends its value, without the one-element list and the concatenation that the expansion writes. It is strict like the concatenation it replaces: an error value of `e` becomes the accumulator, and an accumulator that is an error value stays. The read of `@result` in the result turns the builder into a list over an array filled once ([values](values.md)). The array of the builder grows by doubling from 4 elements up to the size of the range, as elements are added, so that the bytes follow the units charged and a limit that stops the loop stops the allocation. So `map` and `filter` take linear time, and the memory is charged as the elements are added.

## Calls

Type checking records the candidates of each call in the reference map (see [checking](checking.md)); each candidate is an `OverloadBinding` with its implementation and cost (see [library](library.md)).

1. Calls of `_&&_`, `_||_`, `_?_:_` and `@not_strictly_false`, whose overloads have no implementation, become the nodes above. Any other call with one candidate becomes a `CallPlanNode`, a call with several candidates a `DispatchPlanNode` with all of them. A `_[_]` whose operand has the static type `dyn` is a `DispatchPlanNode` with `index_list` and `index_map`, in the order of the library, whatever candidates type checking kept: cel-go v0.32.0 indexes by the kind of the operand at run time, so `x[0u]` with `x` of type `dyn` bound to `[7]` is 7, as without type checking, though a `uint` key leaves the check only `index_map`.
2. Both nodes evaluate the arguments from left to right. Calls are strict: the first argument that is an error value is the value of the call, and no implementation runs.
3. The planner marks the arguments that the node checks at depth with `OverloadBinding.Matches` before the implementation runs ([values](values.md), "Fits a type"; `Matches` checks the top level of every argument, which costs nothing); a mismatch is `EvalNoMatchingOverload` at the call.
   - For an overload of the application every argument is marked, whatever its static type: application code gets only values that fit the declared parameter types, type parameters as `dyn`.
   - For an overload of the library an argument is marked when its static type after the substitution does not cover the parameter type: it is `dyn` or a wrapper, or it has `dyn` or a wrapper inside `list` or `map` where the parameter type has another type, as `list(dyn)` for `list(int)`. A `CallPlanNode` whose arguments are all unmarked calls the implementation directly.
   - An unmarked argument may still hold a value that does not fit its static type: a static type is no guarantee ([values](values.md), "Fits a type"), so `([1] + y)[1]` with `y` = `dyn(['a'])` is an `int` that holds `'a'`. The implementation meets a kind it does not take and returns `EvalNoMatchingOverload` itself ([library](library.md), "Implementations and cost"): `([1] + y)[1] + 1` is `no such overload: _+_(string, int)`, the outcome of cel-go v0.32.0, whose overloads guard the kinds of their arguments at run time.
4. A `DispatchPlanNode` calls the first candidate, in the order of the reference map, that `Matches` the values; none is `EvalNoMatchingOverload` at the call. For each candidate the planner marks the arguments as in step 3; `Matches` checks the top level of all of them and the depth of the marked ones only, so an argument of static type `list(int)` costs nothing against `list(int)`, and a value of another kind under a static type chooses no candidate.
5. With a cost limit the node charges `FixedCost` plus `ArgumentCost` of the arguments before the implementation runs, after the checks of step 3 or 4, which charge what they descend into. An implementation may charge more through `CallSite.Charge` while it runs: equality charges pair by pair, a conversion to text the length of the text before creating it.

So evaluation calls only an overload that type checking accepted, or the other index overload of a `dyn` operand.

## Application functions

A call of an overload of the application, `OverloadBinding.IsApplication`, goes in this order after its arguments pass the checks of step 3 of "Calls":

1. it counts one event (see cancellation);
2. with a cost limit, it calls the `CelCostFunction` of the overload and charges 1 plus its value; when the total exceeds the limit, the implementation is not called;
3. it calls the implementation: the delegate, through the wrapper of the builder when the delegate is typed; the wrapper converts the arguments by the result rows of the type mapping table (a refusal is `ArgumentConversion`, by the value only: a parameter type whose expected type is not the declared one is a build error, [declarations](declarations.md), 4011; a copy is charged first: a list of n elements 1 + n, a map of n pairs 1 + 2n and ⌈L/10⌉ for each string key of length L, as a created map, because the copy hashes each string key, the term of a key charged before the key is put into the copy; and each `bytes` value of length L put into a `byte[]` — the rows `byte[]` and `object`, also as an element or a value of a copy — 1 + ⌈L/10⌉; a copy allocates at most 64 bytes per unit it charges, so an empty map is one shared instance and a map of one pair is no `Dictionary`, and the memory of a map copy does not depend on the length of its keys; what a lazy view creates when the implementation reads it is memory of the implementation) and the result back by the input rows ([values](values.md)). Whether an argument is a lazy view or a copy follows from the pair of the declared parameter type and the .NET parameter type, by the pairs of [facade](facade.md), "Result conversion": the checks of step 3 of "Calls" made the argument fit the declared parameter type at every depth, so a lazy view of a pair that cannot fail does not fail. For `list(int)`, `IReadOnlyList<long>` is a lazy view, not charged; `IReadOnlyList<int>` a copy, charged 1 + n, whose element outside `Int32` is `ArgumentConversion`; for `bytes`, `ReadOnlyMemory<byte>` is not charged and `byte[]` is a copy;
4. an error value returned is the value of the call. Otherwise the value must fit, by `ValueTypeRules.Fits` at any depth and with the meter, the declared result type of the overload with its type parameters as `dyn`, or the call is `ImplementationResultMismatch`. The type of the call that type checking recorded is not checked: a type parameter bound by the arguments may stand for a `dyn` value, and `first(list(A)) -> A` on `dyn(['a']) + [1]` rightly returns `'a'` though the call has the type `int`. A result type `dyn` checks nothing. A result whose CEL type the input row of the typed delegate already gives — `Func<long>` for `int` — fits by construction and is not checked, except a .NET `null`: the input rows turn a `null` `string`, `byte[]`, `CelList` and `CelMap` into the `null` value, which fits none of `string`, `bytes`, `list(dyn)` and `map(dyn, dyn)`. Such a result is checked at the top level only, which costs nothing, and `AddFunction<string, string?>` whose delegate returns `null` is `ImplementationResultMismatch` with `null_type`, as is a `Func<CelList>` that returns `null` for `list(dyn)`. A `string` or `byte[]` result for a declared `wrapper(string)` or `wrapper(bytes)` ([declarations](declarations.md), 4011) fits with `null` as well, so it is not checked. Evaluation without type checking checks the same.

Steps 2 and 3 run inside the exception boundary: an exception becomes `ApplicationException`, which keeps it in `CelError.Exception`. It passes through only `OutOfMemoryException`, the internal `CostLimitException` and an `OperationCanceledException` whose token is the canceled token of the evaluation.

## Frame

`CelValue Plan.Evaluate(IReadOnlyDictionary<string, CelValue> bindings, ulong? costLimit, CancellationToken cancellationToken)` creates a `Frame` for one evaluation on one thread, evaluates the root and returns its value or error value. The bindings are a read-only dictionary by name: the facade passes the one inside `CelBindings` (see [facade](facade.md)); the internal `CelProgram<T>.Plan` gives the plan of a program to the tests. The frame holds:

- the slots of the variables, with a filled flag each, and the slots of the comprehensions;
- the argument stack, which calls use instead of an array per call; a call pushes its argument values and gives the implementation a span over them;
- the meter, which implements `IEvaluationMeter`: the remaining units and the limit, only when the options set a limit; the event counter, only when the token can be canceled;
- the offset of the node whose own operation runs, which each node records before it: the place of `CostLimitExceeded` and `InternalError`.

`Plan.Evaluate` is two internal steps, which the property of cost and memory (see [tests](tests.md), "Properties of evaluation") calls one by one: `Frame CreateFrame(IReadOnlyDictionary<string, CelValue> bindings, ulong? costLimit, CancellationToken cancellationToken)` allocates the frame with its slots, argument stack and meter, and `CelValue Run(Frame frame)` evaluates the root inside the handler of "Stack and internal errors", turning `CostLimitException` into `CostLimitExceeded`; `Evaluate` is `Run(CreateFrame(…))` and nothing else. The internal `Frame.ChargedUnits` is the limit minus the remaining units, 0 without a meter.

## Cost

- Without a limit there is no meter: nothing is counted, no formula is computed, no `CelCostFunction` is called, and a node tests one field of the frame.
- `Charge(units)` subtracts from the remaining units. More units than remain — a sum that does not fit the counter included — means the operation does not run: the meter throws `CostLimitException`, and `Plan.Evaluate` returns `CostLimitExceeded` at the recorded node as the single error. A total equal to the limit is allowed. The exception is not an error value, so `||` and `all` cannot absorb it.
- An argument that is invalid without work on the data — a list index out of range — fails after the constant unit only, before the formula of the operation.
- The frame, with its slots and argument stack, is allocated once before the root is evaluated and is not charged; everything allocated after it counts against the bound of B = 64 bytes per unit that the property test of cost checks.
- What the bound asks of the values of this part. A list of n elements holds n values of 32 bytes and costs 1 + n. A map of n entries holds 2n values, a key and a value each, so it costs 1 + 2n and takes at most 64 · (1 + 2n) bytes with its index, at every n ([values](values.md), "Lists and maps"). An error value takes at most 64 bytes ("Errors as values"), so an operation of 1 unit that fails, such as `1 / 0`, fits.

## Cancellation

When the token can be canceled, the frame counts events: the steps of comprehensions and the calls of application functions. Before each event, `Event()` checks the token when the count is a multiple of 64 — so before the first event and then every 64 — and increments the count; a canceled token throws `OperationCanceledException` out of the evaluation. After the signal at most 64 events begin. With `CancellationToken.None` nothing is counted.

## Errors as values

An error inside evaluation is a value of the kind `Error` of the stage `Evaluate`; nodes return it like any value. Its position is the offset of the node that failed. The value keeps the code, the offset and the parts of the message by reference; the text and the line and column are formed only when a `CelError` is read, after the evaluation or through `CelValue.TryGetError`, so an error that `||` absorbs costs no text. Creating an error value allocates at most 64 bytes: one object, with no string, no boxed number and no array. The parts of its message are references to objects that already exist — a name of the text, a string argument, a list, a type, an overload, a node of the plan, a type that a check charged for found — or a number in a field of the object. The one allowance is the kinds of the arguments of `EvalNoMatchingOverload` for a call of more than 16 arguments, which only an application function can have: they take an array of one byte per argument, bounded by the text of the program. The public members of `CelError` and `CelValue`, the messages and the positions do not depend on this form.

| Code | Message | Position |
| :- | :- | :- |
| `EvalNoMatchingOverload` (3000) | `no such overload: {function}({argumentTypes})`, types of the values by name, an error value named `error` | the call |
| `EvalNoSuchField` (3001) | `no such field '{field}' in {type}` | the selection |
| `Unbound` (3009) | `no value is bound to variable '{name}'` | the identifier |
| `DuplicateMapKey` (3010) | `duplicate map key: {key}`, the later of two keys equal by CEL equality, written as a CEL literal: an `int` in decimal, a `uint` with the suffix `u`, `true` or `false`, a string in double quotes with `\\`, `\"`, `\n`, `\r`, `\t` and `\u00HH` for other control characters; the same form serves `NoSuchKey` | the literal |
| `BindingTypeMismatch` (3011) | `value of variable '{name}' has type '{actualType}', which does not match the declared type '{declaredType}'` | the identifier |
| `CostLimitExceeded` (3013) | `evaluation cost limit of {limit} exceeded` | the node of the operation |
| `ApplicationException` (3014) | `application code threw {exceptionType} in {callee}`; `{callee}` is `overload '{overloadId}' of function '{function}'` or `the cost function of overload '{overloadId}' of function '{function}'` | the call |
| `ArgumentConversion` (3024) | `argument '{parameter}' of overload '{overloadId}' of function '{function}' cannot be converted from '{celType}' to .NET type '{clrType}'`, `{parameter}` the position from 1 | the call |
| `FieldSelectionNotSupported` (3025) | `value of type '{type}' does not support field selection of '{field}'` | the selection |
| `EvalInvalidComprehensionRange` (3026) | `value of type '{type}' cannot be the range of a comprehension (must be list or map)` | the range |
| `UnsupportedMapKeyType` (3027) | `unsupported map key type '{type}'`, checked before `DuplicateMapKey` | the key |
| `ImplementationResultMismatch` (3028) | `implementation of overload '{overloadId}' of function '{function}' returned a value of type '{actualType}', expected '{resultType}'` | the call |
| `InsufficientStack` (9000) | `insufficient execution stack to continue evaluation` | the node that did not evaluate its children |
| `InternalError` (9001) | `internal error during evaluation; the original exception is in CelError.Exception` | the recorded node |

Types of values in messages are the names of runtime types without parameters ([values](values.md), "Runtime type"): `list`, `map`, `null_type`; declared types use the text form of [types](types.md). In 3011 and 3028 `{actualType}` is the type that `ValueTypeRules.Fits` returns on the mismatch: the name of the type of the value at the top level — an error value, which fits no type, `dyn` included, is named `error`, as in 3000 — and deeper the checked type with the element that does not fit (`list(string)` against `list(int)`); `{resultType}` is the declared result type. `{exceptionType}` in 3014 and `{clrType}` in 3024 are written by `Type.ToString()`: `System.InvalidOperationException`, `System.Int32`. The errors of the library functions are in [library](library.md).

**Stack and internal errors.** Each node that evaluates children first calls `RuntimeHelpers.TryEnsureSufficientExecutionStack()`; `false` makes the node return `InsufficientStack`, an error value like any other. `Plan.Evaluate` runs the root inside one handler: an exception other than `OutOfMemoryException`, `CostLimitException` and the `OperationCanceledException` of the canceled token becomes `InternalError` with the original exception. Through the public API a correct library never gets there — an exception of application code is `ApplicationException`, and `CelBindings` does not throw on a lookup — so the example of 9001 at evaluation calls the internal `Plan.Evaluate` with bindings whose lookup throws: `InternalError` at the variable. When the root value is an error, the facade returns it as the single error of the result.

## Evaluation without type checking

`Planner.PlanWithoutTypeCheck(ParsedExpression, DeclarationIndex)` serves the internal entry point `CelEnvironment.EvaluateWithoutTypeCheck` (see [facade](facade.md)). It plans the parsed tree with the same nodes and no checked expression:

- names resolve at evaluation as [checking](checking.md) resolves them at compilation, with "has a value in the bindings" in place of "is declared": comprehension variables first, then the qualified names of an identifier or a selection chain, the longest first, each by the candidates of the container; for each candidate a value in the bindings, else a type identifier of the version. No candidate is `Unbound` for the name as written. Values are not checked against declared types;
- selections, comprehensions and the special calls are planned as with type checking; any other call becomes a `DispatchPlanNode` over all overloads of its name, call style and argument count in the declaration index, the overloads only with `dyn` included; without static types every argument is marked. A function without such overloads is `EvalNoMatchingOverload` once the arguments are evaluated without error.

So `f_unknown(17)` evaluates to `EvalNoMatchingOverload`, and `x` without a binding to `Unbound`. The value of the root is returned as it is, without conversion to a .NET type.

## This part and later parts

| Part | This part | Later |
| :- | :- | :- |
| nodes | all nodes above | fields of objects and presence on them, with object types; `optional` selection and index; comprehensions of two variables; constant arguments prepared at planning (`matches`) |
| values read | bindings by name, checked against the declared type at any depth | objects of the application, `JsonElement`, types described at run time, with their parts |
| calls | the standard library, application functions | extension sets |
| cost and cancellation | the meter, the event counter | the rows of later parts |

Related pages: [checking](checking.md), [library](library.md), [values](values.md), [types](types.md), [declarations](declarations.md), [facade](facade.md), [core](core.md).
