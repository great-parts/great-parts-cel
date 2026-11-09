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
| `ConstantPlanNode` | a literal; a type identifier, an object type of the application included, whose type value the planner takes from the declaration index; an enumeration constant | literal 0; type identifier and constant 1, as a name read |
| `VariablePlanNode` | an identifier or a selection that names a declared variable | 1; at the first read also the check of the binding ([values](values.md), "Fits a type") |
| `LocalPlanNode` | an iteration variable or the accumulator of a comprehension | 1; see comprehensions for the reads that cost 0 |
| `FieldPlanNode` | `e.f` and `has(e.f)` whose operand has an object type, with the description and the field number of the reference map | 1 |
| `SelectPlanNode` | any other selection `e.f` that names no declaration; `has(e.f)` | 1 + ⌈L/10⌉ on a map, L the length of `f`; on a JSON object the key lookup of the cost table; 1 otherwise |
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

**Variable.** The first read of a slot looks the name up in the bindings, checks the value with `ValueTypeRules.Fits` against the declared type of the variable at any depth ([values](values.md)) and keeps the value, or the error `BindingTypeMismatch`; later reads take it from the slot and check nothing. No value is `Unbound`. So a list of strings bound to a variable of type `list(int)` is `BindingTypeMismatch` at the first read of the variable, whatever the expression does with it — `size(x)` included — and never reaches an overload or the result conversion. A variable of an object type applies the place rule of [objects](objects.md) at the first read, with and without type checking: a bare instance that belongs to the type is kept, or paired with the description of the type, in the slot.

**Selection and presence.** The operand comes first; an error value is the result. On a map, `e.f` is the value of the string key `f`, and no such key is `EvalNoSuchField`; `has(e.f)` is whether the key is present; a JSON object uses its counting lookup ([values](values.md), "`JsonElement`"). On an object value the field is found by name in the description of the value ([objects](objects.md)) at each evaluation, then read as below; no such field is `EvalNoSuchField` with the full name of the type, for `has()` too (the language definition, "Field Selection"). Any other value, which only an operand of static type `dyn` or evaluation without type checking can give, is `FieldSelectionNotSupported` for both, after the constant unit only. For `has()` the language definition requires the error ("Field Selection", case 5). cel-go v0.32.0 returns `false` only without `EnableErrorOnBadPresenceTest`, which the oracle and the cel-go conformance runner set; with it both sides fail, so there is no difference.

**`&&` and `||`.** The operands are evaluated from left to right, and the first operand that decides the result — `false` for `&&`, `true` for `||` — is the result, without evaluating the rest. Otherwise the first operand that is an error value, or that is not a `bool` and so gives `EvalNoMatchingOverload` with all operand types of the chain (`no such overload: _&&_(int, bool)`), is the result; when no operand decides it and none fails, the result is `true` for `&&` and `false` for `||`. So `x || true` is `true` and `x && false` is `false` even when `x` is unbound.

**`?:`.** The condition first: `true` evaluates only the second operand, `false` only the third; an error value is the result; another kind is `EvalNoMatchingOverload` with the type of the condition (`no such overload: _?_:_(int)`).

**`@not_strictly_false`.** A `bool` operand is the result; any other value, an error included, is `true`.

## Fields of objects

`FieldPlanNode` holds the description `T` and the field number that type checking recorded, so it reads a field without looking up a name and without converting the rest of the object. It evaluates the operand, then:

1. An error value is the result. An object value whose description ([objects](objects.md)) is `T` — a pair of `T`, or a bare instance that belongs to `T`, by an instance test of the `ClrType` or the carrier class, not by the type index — is read with the bound number. Any other value is taken as by `SelectPlanNode`, by name: a static type is no guarantee of the value ([values](values.md), "Fits a type"), so `([deal] + dyn([other]))[1].amount` reads `amount` of whatever `other` is, or fails as there.
2. It charges 1 unit, counts one event (see cancellation), since it calls code of the application, then calls `GetField(instance, number)` for `e.f` or `HasField(instance, number)` for `has(e.f)` inside the exception boundary of "Application functions": an exception other than `OutOfMemoryException`, the internal `CostLimitException` and the `OperationCanceledException` of the canceled token is `ApplicationException` with the callee `field '{field}' of type '{type}'` or `the presence test of field '{field}' of type '{type}'`.
3. The value of a field enters by the place rule of [objects](objects.md) when the field has an object type. For a type described at run time `GetField` gives `FieldNotSet` and `FieldTypeMismatch` itself ([objects](objects.md), "Types described at run time"). An error value without a position — from a field, a converter or an element of application data — gets the position of the selection.

`SelectPlanNode` on an object value does steps 2 and 3 with the number it found. Neither node checks the value of a field at depth: the elements of a list field are checked where an operation reads them.

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

**Results that are copied.** A typed delegate whose .NET result type is `IReadOnlySet<T>` or `ISet<T>`, or another `IReadOnlyCollection<T>` without an index (`Queue<T>`, `Stack<T>`, `LinkedList<T>`), returns a collection that the input rows copy ([values](values.md), "Input rows"). The copy is part of step 3, after the implementation returns and before step 4:

- **Set.** A first pass over the source, which converts nothing, gives n and Lmax, the greatest length of the `string` and `bytes` elements (0 without them). With a cost limit the wrapper then charges the row of the cost table for a set read as a list, 1 + c · n · ⌈log₂(n + 1)⌉ with c = 1 + ⌈2 · Lmax/10⌉, which is 1 for numbers and `bool`. Only then does it convert the elements into an array of n values and sort it in place by `OrderingRules`, strings by code points. Every refusal of `CelList.CreateSorted` — `null`, an element that converts to an error value, two kinds, a kind without an order, a NaN, two elements equal in CEL — is one error value, after the charge: `InvalidArgument` (3008) `invalid argument to '{overloadId}': a result set whose elements CEL cannot sort`, at the call. The text names no element, so it does not depend on the order of enumeration of the set, which depends on hash codes.
- **Collection without an index.** With a cost limit the wrapper charges 1 + n, then copies the elements in the order of enumeration; an element that converts to an error value is kept as an element, as by `CelList.CreateCopy`.

The copy holds n values of 32 bytes, and the sort runs in place, so the memory stays within B per unit of the row. The result fits its declared type by construction, unless its element type is `dyn` (`ISet<object>`), whose check costs nothing at the top level (step 4).

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

**Application data** — the rows of the cost table for application data, charged by the node or the implementation that does the work, before it:

| Operation | Units | Where |
| :- | :- | :- |
| `e.f` and `has(e.f)` on an object | 1, whatever the field holds; the pair of the place rule and the adapter of a JSON value read are inside it | `FieldPlanNode`, `SelectPlanNode` |
| enumeration of a map view ordered by ascending key, n entries | 1 + c · n · ⌈log₂(n + 1)⌉ before the first entry, at each enumeration, also when the view keeps its sorted keys; c is 1 for integer, `bool` and enumeration keys, 1 + ⌈2 · Lmax/10⌉ for `string` and `Guid` keys (`Guid`: Lmax = 36) | the operation that enumerates: a comprehension over the map, equality, the check against a declared type; the result conversion, which is not charged, enumerates without it |
| key lookup of length L in a JSON object of n properties: index, selection, `has` | 1 + n · (1 + ⌈L/10⌉), instead of the map rows | `SelectPlanNode`, `index_map` |
| `size`, enumeration of a JSON object | 1 + Σ (1 + ⌈Lᵢ/10⌉), Lᵢ the length of the raw UTF-8 name, the term of a property before it; instead of `size_map` and `map_size` | the size functions, the comprehension, equality |
| `in` on a JSON object, key of length L | the row above + ⌈L/10⌉; instead of `in_map` | `in_map` |
| equality of JSON objects | the row of two maps, the lookup in the second by the JSON lookup row, after the check for repeats of both | `EqualityRules` |
| index of a JSON array of n elements | 1 + n, instead of the unit of `index_list` | `index_list` |
| a set of n elements that an application function returns, read as a list | 1 + c · n · ⌈log₂(n + 1)⌉, c as above for `string` and `bytes`, 1 otherwise ("Application functions") | the call, after the implementation, before the sort |
| a collection without an index of n elements that an application function returns | 1 + n | the call, after the implementation, before the copy |
| a JSON string value of length L read | 1 + ⌈L/10⌉ at each read, before the string is created; L is the length of the raw UTF-8 token without its quotes, escape sequences included, the upper bound that is known without decoding, as Lᵢ of a name; it is the decoded length for a string without escapes and without non-ASCII characters. The property names of an enumeration are inside the terms of their row and are not charged again | the node that reads it: the selection, the index of an array, the element of a range or of a comparison, and the identifier of a variable bound to a JSON string |
| equality of two objects | 1 + Σ over the fields compared: 1 for each of the two reads + eq of the values | `EqualityRules` |
| check against a declared type | n before descending into a list of n elements or a map of n entries; a typed view that already fits, 0 | as for the bindings of this part |

The memory of each row stays within B per unit: a pair or a JSON adapter is one object of at most 64 bytes inside the unit of its read, and the sorted keys of a map view, n references, are inside the n · ⌈log₂(n + 1)⌉ units of the sort. The rows of sets and of collections without an index read as members, of struct members, and of the unpacking of `google.protobuf.Any` come with the generator and protobuf; a set and a collection without an index bound as a variable are converted by `CelBindings.Add`, before the evaluation, and cost nothing; returned by an application function they are copied at the call by the two rows above.

## Cancellation

When the token can be canceled, the frame counts events: the steps of comprehensions, the calls of application functions and the field reads and presence tests of objects. Before each event, `Event()` checks the token when the count is a multiple of 64 — so before the first event and then every 64 — and increments the count; a canceled token throws `OperationCanceledException` out of the evaluation. After the signal at most 64 events begin. With `CancellationToken.None` nothing is counted.

## Errors as values

An error inside evaluation is a value of the kind `Error` of the stage `Evaluate`; nodes return it like any value. Its position is the offset of the node that failed. The value keeps the code, the offset and the parts of the message by reference; the text and the line and column are formed only when a `CelError` is read, after the evaluation or through `CelValue.TryGetError`, so an error that `||` absorbs costs no text. Creating an error value allocates at most 64 bytes: one object, with no string, no boxed number and no array. The parts of its message are references to objects that already exist — a name of the text, a string argument, a list, a type, an overload, a node of the plan, a type that a check charged for found — or a number in a field of the object. The one allowance is the kinds of the arguments of `EvalNoMatchingOverload` for a call of more than 16 arguments, which only an application function can have: they take an array of one byte per argument, bounded by the text of the program. The public members of `CelError` and `CelValue`, the messages and the positions do not depend on this form.

| Code | Message | Position |
| :- | :- | :- |
| `EvalNoMatchingOverload` (3000) | `no such overload: {function}({argumentTypes})`, types of the values by name, an error value named `error` | the call |
| `EvalNoSuchField` (3001) | `no such field '{field}' in {type}`; for an object value without the field `{type}` is the full name of its type | the selection |
| `ConversionRange` (3007) | for application data: a JSON number that is not a finite `double`, an enumeration key above `long.MaxValue` ([values](values.md)) | the node that read the value |
| `InvalidArgument` (3008) | for application data: an undefined `JsonElement` ([values](values.md)); a set that an application function returns and CEL cannot sort ("Application functions") | the selection or the call |
| `Unbound` (3009) | `no value is bound to variable '{name}'` | the identifier |
| `DuplicateMapKey` (3010) | `duplicate map key: {key}`, the later of two keys equal by CEL equality, written as a CEL literal: an `int` in decimal, a `uint` with the suffix `u`, `true` or `false`, a string in double quotes with `\\`, `\"`, `\n`, `\r`, `\t` and `\u00HH` for other control characters; the same form serves `NoSuchKey`. A repeated property name of a JSON object is written as a string | the literal; for JSON, the operation: the selection, the index or the call of `size`, `in`, `==`, the range of a comprehension |
| `BindingTypeMismatch` (3011) | `value of variable '{name}' has type '{actualType}', which does not match the declared type '{declaredType}'` | the identifier |
| `TypeNotAccessible` (3012) | `.NET type '{clrType}' is not available to expressions: no type source of the environment describes it`, `{clrType}` by `Type.ToString()` | for an object without a description, the node that needs the description: a selection or presence test by name, the call of `type`, `==`, `!=` or `in` ([objects](objects.md)); for a .NET collection that no source describes, read from the application ([objects](objects.md), "Reading a value of the application"), the node that read the value: the identifier, the selection, the call; an instance that a source describes, by its type or a base class, is an object though it is enumerable |
| `CostLimitExceeded` (3013) | `evaluation cost limit of {limit} exceeded` | the node of the operation |
| `ApplicationException` (3014) | `application code threw {exceptionType} in {callee}`; `{callee}` is `overload '{overloadId}' of function '{function}'`, `the cost function of overload '{overloadId}' of function '{function}'`, `field '{field}' of type '{type}'`, `the presence test of field '{field}' of type '{type}'`, `a list of the application`, `a map of the application` or `a JSON element` | the call or the selection; for an element of a collection of the application or a `JsonElement`, the node that read it: the index or the selection, the call of `size`, `in` or `==`, the range of a comprehension, the identifier or the call whose value is checked against a declared type |
| `FieldNotSet` (3018) | `field '{field}' of type '{type}' has no value` | the selection |
| `FieldTypeMismatch` (3019) | `field '{field}' of type '{type}' has a value of type '{actualType}', expected '{declaredType}'` | the selection |
| `ArgumentConversion` (3024) | `argument '{parameter}' of overload '{overloadId}' of function '{function}' cannot be converted from '{celType}' to .NET type '{clrType}'`, `{parameter}` the position from 1 | the call |
| `FieldSelectionNotSupported` (3025) | `value of type '{type}' does not support field selection of '{field}'`, for every kind but a map and an object | the selection |
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
- a selection on a value is a `SelectPlanNode`, which finds a field of an object by name; variables of an object type apply the place rule at the first read; a message literal is planned with protobuf literals and is `InternalError` until then;
- comprehensions and the special calls are planned as with type checking; any other call becomes a `DispatchPlanNode` over all overloads of its name, call style and argument count in the declaration index, the overloads only with `dyn` included; without static types every argument is marked. A function without such overloads is `EvalNoMatchingOverload` once the arguments are evaluated without error.

So `f_unknown(17)` evaluates to `EvalNoMatchingOverload`, and `x` without a binding to `Unbound`. The value of the root is returned as it is, without conversion to a .NET type.

## This part and later parts

| Part | This part | Later |
| :- | :- | :- |
| nodes | all nodes above, fields of objects and presence on them included | message literals of protobuf; `optional` selection and index; comprehensions of two variables; constant arguments prepared at planning (`matches`) |
| values read | bindings by name, checked against the declared type at any depth; objects of types described at run time and of descriptions with a .NET type; collections of the application; `JsonElement` | members of .NET types and their place rule inside collections (generator), protobuf messages, time, `decimal` |
| calls | the standard library, application functions | extension sets |
| cost and cancellation | the meter, the event counter, the rows of application data above | the rows of later parts |

Related pages: [checking](checking.md), [library](library.md), [values](values.md), [objects](objects.md), [types](types.md), [declarations](declarations.md), [facade](facade.md), [core](core.md).
