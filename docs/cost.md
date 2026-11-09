# Cost

This page is the table of cost units of an evaluation. An application that evaluates expressions it does not trust sets a cost limit in `CelEvalOptions.CostLimit`: the evaluation charges the units of every operation before it runs, and an operation whose charge would take the total above the limit does not run; the evaluation ends with the error `CostLimitExceeded` (3013), whose position is the node of the operation.

The table is a contract: the units of an operation are the same in every version of the library that has the operation, and after 1.0 a row changes only in a major version. The numbers are the numbers of this library, not those of the CEL implementation in Go, so a cost limit does not carry over between the two.

## Rules

- **One unit** is a constant number of elementary actions of the evaluator and at most B bytes of memory allocated beyond the frame of the call; B is 64. An operation whose time or memory grows with its data has a formula of the size of the data, not a constant.
- **Charged before.** The cost of an operation is known before it runs, or it is charged before each step of it. A total equal to the limit is not over it.
- **Lengths.** The length of a string is counted in UTF-16 code units (`string.Length`) and the length of `bytes` in bytes. The number of code points is not used: it is known only after a pass over the string.
- **Only evaluation is charged.** Parsing, type checking and planning are bound by the [limits](limits.md) and are not charged. A one-step call, `CelEnvironment.Evaluate<T>`, charges only the evaluation.
- **No limit, no counting.** Without a cost limit nothing is counted: the formulas are not computed and the cost functions of the application are not called.
- **Text and data decide, not caches.** A hit in the cache of compilations, the folding of constants and other optimizations of the planner do not change what is charged. A macro is expanded by the parser and the cost is of the expanded tree: the expressions that the author wrote are charged by their own rows, and what the expansion adds is part of the step, see [Macros](#macros).
- **An invalid argument is not charged.** An argument that is invalid without any work on the data, such as the sign or the range of a number or an index outside a list, gives its error before the formula of the operation is charged; only the constant part, 1, is charged.
- **No overflow.** A total that does not fit the counter is over every limit.
- **The type of the counter** is `ulong`, both for `CelEvalOptions.CostLimit` and for the value of a `CelCostFunction`.

## Notation

| Quantity | Meaning |
| :- | :- |
| ⌈x⌉ | x rounded up to an integer |
| Lₐ | the sum of the lengths of the string and `bytes` arguments, the receiver included |
| Lᵣ | the length of a string or `bytes` result; it is charged before the result is created, or part by part as the result is built, so that the charges add up to ⌈Lᵣ/10⌉ |
| n | the number of elements of a list or of pairs of a map |
| L | the length of a string key of a map |
| eq(a, b) | the cost of comparing two values, see [Equality and order](#equality-and-order) |

## Nodes of an expression

| Class | Operation | Units |
| :- | :- | :- |
| Literal | a literal of `int`, `uint`, `double`, `bool`, `string`, `bytes` or `null` | 0 |
| Read | the read of a variable, a type identifier such as `int`, `list` or `type`, an index of a list, an index or a selection in a map with a key that is not a string, `in` on a map with a key that is not a string | 1 |
| Read with a string key | an index, a selection, `has()` or `in` on a map with a string key of length L | 1 + ⌈L/10⌉ |
| Logic | `&&`, `\|\|`, `?:` | 0 beyond the operands; only the operand that is needed is evaluated |
| List | creating a list of n elements: a literal, also the empty `[]`, a concatenation, a copy | 1 + n |
| Map | creating a map of n pairs: a literal, a copy | 1 + 2n, and ⌈L/10⌉ for each string key of length L |
| Map insertion | the insertion of a pair into a map that the expansion of a macro builds (`@mapInsert`) | 2 for each pair, and ⌈L/10⌉ for a string key of length L |
| Macro step | one iteration of a macro | 1 beyond the body; the body is the expressions that the user wrote |

A pair of a map is two values, a key and a value, as two elements of a list, hence 1 + 2n. A string key is charged because a key is found in constant time by its hash, and the hash of a string is a pass over it. The memory of a map of n pairs is at most 64 · (1 + 2n) bytes for any n, and an empty map is a shared instance.

## Calls

| Class | Operation | Units |
| :- | :- | :- |
| Constant | a call of constant time without memory that grows with the data: arithmetic, comparison of numbers and `bool`, `!`, conversion of numbers, the `size` of a list, a map and `bytes` | 1 |
| Text | an operation on strings or `bytes` that passes over its arguments and creates a result | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉ |
| Equality and order | `==`, `!=`, ordering, `in` on a list | eq(a, b), see below |
| Application function | a call of a function of the application | 1 + c, c the value of the `CelCostFunction` of the overload, 0 without one |

The arguments and the result are rounded separately: the part of the result is charged before the result is created or while it is built, so the sum differs from ⌈(Lₐ + Lᵣ)/10⌉ by at most 1.

## Equality and order

The charge of every pair is made before the pair is compared, so a comparison that stops at the first unequal pair charges only the pairs it reached, and the sum depends only on the data.

| Pair of values | eq(a, b) |
| :- | :- |
| values of different kinds; `null`, `bool`, `int`, `uint`, `double` and values of the type `type` | 1 |
| two strings | 1 + ⌈(La + Lb)/10⌉, with the lengths of the two strings |
| two `bytes` | 1 + ⌈(Ba + Bb)/10⌉, with the lengths of the two in bytes |
| two lists | 1 when the sizes differ; otherwise 1 + Σ eq(aᵢ, bᵢ) by index, in order, up to the first unequal pair |
| two maps | 1 when the sizes differ; otherwise 1 + Σ over the keys k of a in its order: the lookup of k in b (the classes Read and Read with a string key) + eq(a[k], b[k]), up to the first unequal pair. For a JSON object the lookup is the row "key lookup in a JSON object" of [Data of the application](#data-of-the-application), and its size and enumeration are the row "`size`, enumeration of a JSON object" |
| two objects of the application | 1 + Σ over the fields compared, in the order of the description: the reads of the field on both sides (1 each) + eq of the two values, up to the first unequal pair. Two objects of different descriptions are 1 |

Ordering (`<`, `<=`, `>`, `>=`) of numbers and `bool` is 1, of strings 1 + ⌈(La + Lb)/10⌉, of `bytes` 1 + ⌈(Ba + Bb)/10⌉.

## Standard library

The identifiers are those of the page [Standard library](standard-library.md).

| Overloads | Class | Units |
| :- | :- | :- |
| `conditional`, `logical_and`, `logical_or` | Logic | 0 beyond the operands |
| `not_strictly_false` | part of the macro step | 0 |
| `logical_not` | Constant | 1 |
| `equals`, `not_equals` | Equality | eq(a, b) |
| `add_int64`, `add_uint64`, `add_double`, `subtract_…`, `multiply_…`, `divide_…`, `modulo_int64`, `modulo_uint64`, `negate_int64`, `negate_double` | Constant | 1 |
| `add_string`, `add_bytes` | Text | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉, with Lᵣ = Lₐ |
| `add_list` | List | 1 + n, with n the size of the result |
| ordering of `bool` and numbers, among them the 24 overloads across numeric types | Constant | 1 |
| ordering of `string` and `bytes` | Order | 1 + ⌈Lₐ/10⌉ |
| `index_list` | Read | 1 |
| `index_map`, `in_map` | Read, Read with a string key | 1; for a string key of length L, 1 + ⌈L/10⌉ |
| `in_list` | Equality | 1 + Σ eq(x, eᵢ) over the elements in order, up to the first equal one |
| `size_bytes`, `bytes_size`, `size_list`, `list_size`, `size_map`, `map_size` | Constant | 1 |
| `size_string`, `string_size` | Text | 1 + ⌈Lₐ/10⌉: the size counts code points, which takes a pass over the string |
| `contains_string`, `ends_with_string`, `starts_with_string` | Text | 1 + ⌈Lₐ/10⌉ |
| `type`, `to_dyn` | Constant | 1 |
| `bool_to_bool`, `bytes_to_bytes`, `double_to_double`, `int64_to_int64`, `string_to_string`, `uint64_to_uint64` | Constant | 1: the identity, the value is not copied |
| `int64_to_double`, `uint64_to_double`, `double_to_int64`, `uint64_to_int64`, `double_to_uint64`, `int64_to_uint64` | Constant | 1 |
| `string_to_bool`, `string_to_double`, `string_to_int64`, `string_to_uint64` | Text | 1 + ⌈Lₐ/10⌉ |
| `string_to_bytes`, `bytes_to_string` | Text | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉ |
| `bool_to_string`, `int64_to_string`, `uint64_to_string`, `double_to_string` | Text | 1 + ⌈Lᵣ/10⌉; the length of the result is bounded and known before the string is created |

The search for a substring (`contains`) takes time linear in the lengths of the string and the substring in the worst case, so 1 + ⌈Lₐ/10⌉ holds for every input.

## Macros

A macro is charged by its expansion. The expressions that the author wrote — the range, the predicate, the transformation — are charged by their own rows. The macro itself charges only:

- 1 for each step that is executed; the steps that a short circuit skips are not executed;
- the initial value of the accumulator: `[]` of `map` and `filter` is 1 (the class List with n = 0), the other initial values are literals and cost 0;
- 1 for each element that `map` or `filter` adds to the result.

Everything else that the expansion adds is part of the step and costs 0: the reads of the accumulator, the condition of the loop with `@not_strictly_false`, the `!`, `&&`, `||` and `?:` of the step, the `+ 1` of `exists_one`, the expression of the result, and the read of the loop variable that `filter` adds to the list.

| Macro | Units |
| :- | :- |
| `has(e.f)` | as the selection of a field: Read; on a map, Read with a string key |
| `all`, `exists` | 1 for each executed step; the steps after a short circuit are not executed |
| `exists_one` | 1 for each step, without a short circuit |
| `map` (2 and 3 arguments), `filter` | 1 for each step; the result is a list: 1 for the empty list and 1 for each element added |

## Data of the application

The rows below are charged by the node or the implementation that does the work, before it, as the rows of the sections above. The rows for a JSON object replace the cost of the same operations on a map, they are not added to it: the key lookup (an index, a selection, `has()`) instead of the class Read with a string key and the index of a map, `in` instead of `in` on a map, and "`size`, enumeration" instead of the `size` of a map. The row for the index of a JSON array replaces the unit that the index of a list costs.

| Operation | Units |
| :- | :- |
| `e.f` and `has(e.f)` on an object of the application: the read of a field | 1, whatever the field holds |
| the enumeration of a map view of an application dictionary that is ordered by ascending key ([Objects and collections](type-mapping.md#objects-and-collections)), n entries | 1 + c · n · ⌈log₂(n + 1)⌉ before the first entry, at each enumeration, also when the view keeps its sorted keys; c is 1 for integer, `bool` and enumeration keys, and 1 + ⌈2 · Lmax/10⌉ for `string` and `Guid` keys (a `Guid` key is a string of 36 characters), Lmax the length of the longest key. The conversion of a result to a .NET type enumerates without this charge, as it is not charged |
| the key lookup of a key of length L in a JSON object of n properties: an index, a selection, `has()` | 1 + n · (1 + ⌈L/10⌉). The lookup passes over all n properties and counts the matches, so that a repeated name is found |
| `size` and the enumeration of a JSON object of n properties | 1 + Σ (1 + ⌈Lᵢ/10⌉) over the properties, the 1 being the unit of the call for `size` and charged by the enumeration itself for a comprehension and for an equality, Lᵢ the length of the name of the property as it is written in the document, in UTF-8 bytes; the term of a property is charged before the property is processed. The operation checks the names for a repeat, and an escaped name is decoded for it |
| `in` on a JSON object, a key of length L | the row above for `size` + ⌈L/10⌉ |
| the equality of two JSON objects | the row of two maps in [Equality and order](#equality-and-order): the lookup in the second object is the row of the key lookup, after the check of both objects for repeated names |
| the index of a JSON array of n elements | 1 + n, instead of the unit of the index of a list: the array finds an element in constant time only when it has no nested values, otherwise it walks to the index |
| a string of JSON, at each read | 1 + ⌈L/10⌉, charged before the string is created. L is the length in UTF-8 bytes of the string as it is written in the document, without its quotes and with its escape sequences; for a string without escape sequences and without characters outside ASCII it is the length of the string. The node that reads the string charges it: a selection or an index that finds it, the index of an array, the element of a range or of a comparison, and the identifier of a variable bound to a JSON string. The names of an enumeration are inside the row above and are not charged again; `has()` and `in` read no value |
| the equality of two objects of the application | the row of two objects in [Equality and order](#equality-and-order) |
| a set that an application function returns, read as a list, n elements | 1 + c · n · ⌈log₂(n + 1)⌉, charged by the call after the implementation and before the sort; c is as above for `string` and `bytes` elements (Lmax the longest) and 1 for the others |
| a collection without an index that an application function returns, n elements | 1 + n, charged by the call after the implementation and before the copy |
| the check of a value against a declared type | the section [Values checked against their declared type](#values-checked-against-their-declared-type); a typed view that already fits costs 0 |

A set and a collection without an index that are bound to a variable are converted by `CelBindings.Add` before the evaluation and cost nothing at the evaluation.

Not in this version of the page: the rows of the members of the types of .NET that the source generator reads (a structure member, a set or a collection as a member) and of the unpacking of `google.protobuf.Any` and a protobuf message literal. They come with their parts.

## Values checked against their declared type

A value is checked against a declared type, at any depth, where it enters or leaves the expression. The check descends into a list or a map only when the parameter of the declared type is neither `dyn` nor a type parameter, and charges n before it descends into a list of n elements or a map of n pairs; the nested ones in the same way. `[[1], [2, 3]]` against `list(list(int))` is 2 + 1 + 2 = 5.

| Where | Units |
| :- | :- |
| a binding, at its first read | n for a collection of n elements or pairs, nested ones too |
| each argument of an application function, and its result | the same |
| an argument of an overload of the library whose static type does not cover the parameter, such as `list(dyn)` against `list(int)`, or a wrapper | the same |

The top level and the parameters `dyn` and type parameters cost 0. A value whose CEL type is already assignable to the checked one without `dyn` is not checked and costs 0: the .NET adapter `IReadOnlyList<long>` for `list(int)` is that case, and a copy or a `CelList` is not.

Examples:

- `x` of the type `list(int)` bound to a `CelList` of 3 elements: 1 + 3 = 4;
- `h([1, 2, 3])` with `h(list(int)) -> int` of the application and no cost function: the literal 4, the check of the argument 3, the call 1, 8 in all;
- `g()` with `g() -> list(int)` and a result of 3 elements: the call 1 and the check of the result 3, 4 in all.

The conversion of the result of an evaluation to a .NET type, including the immutable copy of a collection and the check before a lazy view, is not charged: it runs after the evaluation.

## Functions of the application

| Rule |
| :- |
| A call costs 1 + c. c is the number of units that the `CelCostFunction` of the overload returns, and 0 for an overload without one. |
| The delegate receives the arguments of the call and is called before the implementation; the charge 1 + c is made before the implementation is called. If the total is over the limit, the implementation is not called. |
| The time and memory of the implementation are not limited by the library: it is code of the application. A function whose work grows with its arguments declares that in its cost function. |
| Without a limit the delegate is not called. |
| An exception of the delegate is the error `ApplicationException` (3014) with the original exception, as an exception of the implementation is. |
| Each argument is checked, at any depth, against the declared type of its parameter before the cost function is called; the result is checked against the declared type of the result after the implementation. Both checks are charged by the previous section. |

**Arguments passed as copies.** An argument reaches the delegate by the result rows of the [type mapping](type-mapping.md#results). A copy is charged before it is passed:

| Copy | Units |
| :- | :- |
| an immutable copy of a list of n elements | 1 + n |
| a copy of a map of n pairs | 1 + 2n, and ⌈L/10⌉ for each string key of length L (a key of the type `string`, or `object` holding a string); the charge of a key is made before the key is put into the copy |
| each `bytes` value of length L that the passing puts into a `byte[]` (an argument, an element or a value of a copy) | 1 + ⌈L/10⌉ |

The copies nested in a copy are charged in the same way. A lazy view and a `ReadOnlyMemory<byte>` are not charged: what a view creates when the implementation reads it is the time and memory of the implementation. A copy allocates at most B bytes for each unit of its row for any n and L: an empty map is a shared instance, and the memory of a map does not depend on the length of its keys, since the string of a key is shared with the source.

## Calibration

| Quantity | Value |
| :- | :- |
| B, the bytes allocated beyond the frame for one unit | 64 |
| the divisor of a pass over a string | 10 |

The units are calibrated by a property test that compares `GC.GetAllocatedBytesForCurrentThread` with the charged units. B is 64 until 1.0, when the final number is published; after 1.0 the numbers change only in a major version. The largest numbers of bytes per unit that the calibration of this release measured, for the sizes 0, 1, 10, 100 and 1 000 (`net10.0`, Debug):

| Operation | Bytes per unit |
| :- | :- |
| a list literal | 26.7 |
| `add_list` | 32.0 |
| `add_string` | 10.0 |
| `add_bytes` | 6.9 |
| `string_to_bytes` | 9.6 |
| `bytes_to_string` | 12.0 |
| `int64_to_string` | 14.0 |
| `uint64_to_string`, `double_to_string` | 16.0 |
| `bool_to_string`, the parsing of numbers from strings | 0 |
| `map` and `filter` | 28.0 |
| `contains` with a pattern longer than 256 | 9.2 |

A map literal did not fit 1 + n: a pair is two values of 32 bytes, so the index that finds a key in constant time did not fit 64 bytes for each unit; the unit count of a map is therefore 1 + 2n. The creation of an error value allocates at most 64 bytes, one object without strings or arrays, whose text and position are built when the error is read, so a refusal of an operation of 1 unit, such as `1 / 0`, fits. An error value itself is not charged.
