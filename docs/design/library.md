# Library: the standard library

`Great.Parts.Cel.Library` holds the standard library of each version: the type identifiers, the declarations of its functions and overloads, their implementations and their costs. An environment names its version in `new CelEnvironmentBuilder(CelStandardLibrary.V1)`; the public `CelStandardLibrary.V1` is a handle to the table of version 1 in this namespace (see [core](core.md)).

This part has version 1: twelve type identifiers and 162 overloads — the 113 of the earlier parts, the 47 of time and the 2 of `matches`. The standard macros are expanded by the parser ([syntax](syntax.md)) and evaluated by the comprehension node ([evaluation](evaluation.md)). Identifiers, signatures and semantics are those of the [standard library](../standard-library.md) page, costs those of the [cost](../cost.md) page; this page says how they are built. The time functions are in `Library.Time`, `matches` in `Library.Regex` and `Library.Unicode`, extension sets go to their own tables.

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Library/StandardLibraryV1.cs` | the table of version 1 |
| `src/Great.Parts.Cel/Library/OverloadIds.cs` | the overload identifiers as constants |
| `src/Great.Parts.Cel/Library/Arithmetic.cs` | arithmetic of `int`, `uint` and `double` |
| `src/Great.Parts.Cel/Library/Comparisons.cs` | `!`, equality and ordering over the value rules |
| `src/Great.Parts.Cel/Library/Collections.cs` | index, `in`, `size`, concatenation of lists |
| `src/Great.Parts.Cel/Library/Strings.cs` | functions of strings and bytes, concatenation |
| `src/Great.Parts.Cel/Library/SubstringSearch.cs` | the linear search for a substring, with its step counter |
| `src/Great.Parts.Cel/Library/Conversions.cs` | `type`, `dyn` and the conversions |
| `src/Great.Parts.Cel/Library/NumberText.cs` | numbers to text and text to numbers, without culture |
| `src/Great.Parts.Cel/Library/Costs.cs` | the cost functions of the arguments |
| `src/Great.Parts.Cel/Library/Time/TimeOverloadIds.cs` | the 47 overload identifiers of time as constants: `Library.Time` cannot refer to `OverloadIds` |
| `src/Great.Parts.Cel/Library/Time/TimeOverloads.cs` | the 47 bindings, which `StandardLibraryV1` adds to the functions of cel-go in its order |
| `src/Great.Parts.Cel/Library/Time/TimeConversions.cs`, `TimeReceivers.cs` | the nine conversions; the 24 receivers |
| `src/Great.Parts.Cel/Library/Time/TimeArithmetic.cs` | `+` and `-` of time; the eight relations use the ordering of `Comparisons.cs` |
| `src/Great.Parts.Cel/Library/Time/TimeCosts.cs` | the cost functions of time |
| `src/Great.Parts.Cel/Declarations/OverloadBinding.cs`, `OverloadImplementation.cs`, `CallSite.cs` | one overload with its implementation and cost; the internal delegate; the call it serves |

The three types in `Declarations` are internal. They live there, not in `Library`, because the layer table lets `Checking` and `Evaluation` refer to `Declarations` but not to `Library`.

## Tables

A version is a static, immutable table, created once per process when it is first used. It never changes: a later version is a new table, and a fix of behavior changes an implementation that all versions share. It holds the type identifiers — `bool`, `bytes`, `double`, `int`, `uint`, `string`, `null_type` and `type` with their type values, `list` with `list(dyn)` and `map` with `map(dyn, dyn)`, and `google.protobuf.Timestamp` and `google.protobuf.Duration` with the type values `CelType.Timestamp` and `CelType.Duration`; the two names are qualified identifiers and resolve as any qualified name ([checking](checking.md)); `timestamp` and `duration` alone are functions, not identifiers, and `google.protobuf.Timestamp == type(timestamp(0))` is `true` — and an `ImmutableArray<OverloadBinding>` in the order of cel-go, with a frozen dictionary from the function name to its overloads. `CelEnvironmentBuilder.Build` merges it with the declarations of the application into the declaration index ([checking](checking.md)).

`OverloadBinding` holds:

- `Function`, `Declaration` — the name in the syntax tree, such as `_+_`, and the public `CelOverloadDecl`;
- `Implementation` — an `OverloadImplementation`, or `null` for the special forms `conditional`, `logical_and`, `logical_or` and `not_strictly_false`, which the planner evaluates itself;
- `FixedCost` and `ArgumentCost` — the cost of a call known from its arguments: fixed units, and a `CelCostFunction` or `null`;
- `DynOnly` — a candidate only when an argument has the static type `dyn`: the 24 overloads of ordering across `int`, `uint` and `double` ([checking](checking.md));
- `Matches(arguments, marks, meter)` — whether values fit the overload at evaluation, by `ValueTypeRules.Fits` against the declared parameter types: the top level of every argument, and at any depth each argument that the planner marked ([evaluation](evaluation.md), "Calls"), except that `index_list` also takes an index of the kind `uint` or `double` (cel-spec `lists/index/zero_based_*`);
- `IsApplication`, `Source` — whether the overload comes from the application, and the source that build errors name.

The identifiers are those of cel-go, letter for letter, so the overload lists of the library and of the oracle compare without exceptions. Each overload carries its cost from the moment it appears; a test compares every binding with the cost page.

## Overloads of this part

The position of an error is the call; for `_[_]` it is the `[` of the index operation, as in cel-go, not the index argument: `[1, 2][2]` is offset 6. `{type}` is a runtime type ([values](values.md)); a value in a message is written as `string()` writes it, a key as in `NoSuchKey`.

| Overloads | Implementation | Errors | Cost |
| :- | :- | :- | :- |
| `conditional`, `logical_and`, `logical_or`, `not_strictly_false` | nodes of the planner | — | 0 beyond the operands |
| `logical_not` | negation | — | 1 |
| `equals`, `not_equals` | `EqualityRules` | — | eq(a, b), charged pair by pair |
| `add_int64`, `subtract_int64`, `multiply_int64`, `divide_int64`, `modulo_int64`, `negate_int64`; the same `_uint64` without negation | 64-bit arithmetic; overflow found without exceptions — by the signs of the operands and the result, by `Math.BigMul` for products; `-2^63 / -1` and `-2^63 % -1` tested before the .NET operator, which would throw | `Overflow` (3006): `integer overflow`, `unsigned integer overflow` (a negative difference of `uint` too); divisor 0 — `DivisionByZero` (3004): `division by zero`, `ModulusByZero` (3005): `modulus by zero` | 1 |
| `add_double`, `subtract_double`, `multiply_double`, `divide_double`, `negate_double` | IEEE 754: division by zero gives an infinity or NaN | — | 1 |
| `add_string`, `add_bytes` | concatenation | — | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉, Lᵣ = Lₐ |
| `add_list` | a new list of both | — | 1 + n, n the size of the result |
| `less_`, `less_equals_`, `greater_`, `greater_equals_` with `bool`, `int64`, `uint64`, `double` and the 24 across numeric kinds | `OrderingRules`; unordered — NaN — is `false` | — | 1 |
| the same with `string`, `bytes` | `OrderingRules`: code points, unsigned bytes | — | 1 + ⌈Lₐ/10⌉ |
| `index_list` | an `int` index; a `uint` index; a `double` index with an integral value as that integer | `IndexOutOfRange` (3003): `index {index} out of range [0, {size})`; a `double` that is not integral, NaN and infinities included — `InvalidArgument` (3008), detail `index {index} is not an integer` | 1 |
| `index_map` | lookup by `KeyHashRules` ([values](values.md)) | `NoSuchKey` (3002): `no such key: {key}`; a `double` key written as `string()` writes it, a key of another kind as its type | 1; a string key of length L — 1 + ⌈L/10⌉ |
| `in_list` | `EqualityRules` element by element, up to the first equal | — | 1 + Σ eq(x, eᵢ), charged pair by pair |
| `in_map` | lookup by `KeyHashRules` | — | as `index_map` |
| `size_list`, `list_size`, `size_map`, `map_size`, `size_bytes`, `bytes_size` | count | — | 1 |
| `size_string`, `string_size` | code points: a surrogate pair counts once, a lone surrogate once | — | 1 + ⌈Lₐ/10⌉ |
| `contains_string` | `SubstringSearch` below | — | 1 + ⌈Lₐ/10⌉ |
| `starts_with_string`, `ends_with_string` | ordinal comparison of UTF-16 code units, which is comparison of code points | — | 1 + ⌈Lₐ/10⌉ |
| `type`, `to_dyn` | `ValueTypeRules.TypeOf`; identity | — | 1 |
| `bool_to_bool`, `bytes_to_bytes`, `double_to_double`, `int64_to_int64`, `string_to_string`, `uint64_to_uint64` | identity, without a copy | — | 1 |
| `int64_to_double`, `uint64_to_double`, `double_to_int64`, `uint64_to_int64`, `double_to_uint64`, `int64_to_uint64` | nearest `double`, ties to even; truncation toward zero; the sign of a `double` tested before truncation, so `uint(-0.0)` is `0u` and `uint(-0.5)` an error | `ConversionRange` (3007): `{value} is out of the range of {targetType}` — a `double` NaN, infinite or outside (−2⁶³, 2⁶³) for `int`, below 0 or from 2⁶⁴ for `uint`; a `uint` above 2⁶³ − 1; a negative `int` | 1 |
| `string_to_bool`, `string_to_int64`, `string_to_uint64`, `string_to_double` | the forms of the standard library page, below | `InvalidArgument` (3008), detail `cannot parse {text} as {type}`; a number outside its type — `ConversionRange` | 1 + ⌈Lₐ/10⌉ |
| `bool_to_string`, `int64_to_string`, `uint64_to_string`, `double_to_string` | the text, below | — | 1 + ⌈Lᵣ/10⌉, charged before the string is created |
| `string_to_bytes`, `bytes_to_string` | UTF-8, a lone surrogate written as U+FFFD; strict decoding by `System.Text.Unicode.Utf8` | invalid UTF-8 — `InvalidArgument`, detail `invalid UTF-8 at byte {offset}` | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉ |

`InvalidArgument` reads `invalid argument to '{function}': {detail}`. In `cannot parse {text} as {type}` the string stands as it is, without quotes: `cannot parse abc as int`. In `ConversionRange` the `{value}` of a string is its text (`9223372036854775808 is out of the range of int`), and a number is written as `string()` writes it: `1e+19`, `+Inf`, `NaN`. Ordering of lists, maps and `null`, and arguments of the wrong kinds with `dyn`, have no overload: `EvalNoMatchingOverload` of [evaluation](evaluation.md).

## Implementations and cost

```text
namespace Great.Parts.Cel.Declarations
internal delegate CelValue OverloadImplementation(ReadOnlySpan<CelValue> arguments, in CallSite site);

internal readonly struct CallSite
    string Function                                   // the function name, for messages
    bool HasLimit                                     // whether the evaluation has a cost limit
    void Charge(ulong units)                          // charges through the frame; nothing without a limit
    IEvaluationMeter? Meter                           // for the value rules that charge pair by pair
    CelTimeZoneDatabase? TimeZones                    // the database of the environment; null when it has none
    CelValue Error(CelErrorCode code, ...)            // an error value at the position of the call
    CelValue NoMatchingOverload(ReadOnlySpan<CelValue> arguments)
```

- An implementation receives the argument values in the order of the parameters and never an error value: calls are strict. It reads them with the `TryGet…` methods and returns `EvalNoMatchingOverload` for a kind it does not take, at whatever depth it reads; the identities `int64_to_int64`, `string_to_string` and the others check the kind too. A checked call does meet such kinds: a static type is no guarantee of the value ([values](values.md), "Fits a type"), and an unmarked argument goes to the implementation unchecked ([evaluation](evaluation.md), "Calls"). With `y` of type `dyn` bound to `['a']`, `([1] + y)[1] + 1` reaches `add_int64` with a string and is `no such overload: _+_(string, int)`, and `int(([1] + y)[1])` reaches `int64_to_int64` and is `no such overload: int(string)` — the outcomes of cel-go v0.32.0, whose overloads guard the kinds of their arguments. Tests reach the rule through such expressions as well as by calling implementations directly.
- Implementations do not throw and keep no state, so a program calls them from any number of threads at once.
- The cost of a call is charged before the operation, in three ways. A cost known from the arguments — fixed units, lengths of strings and bytes, the size of a list — is `FixedCost` and `ArgumentCost`, which the call node charges before the implementation runs. Equality, `!=` and `in_list` charge eq of each pair before comparing it: `EqualityRules` takes the meter of the call site, and without a limit there is none. A result whose length is known only from its content charges its part through `CallSite.Charge` before it is created: the text of a number is formatted into a stack buffer first, `string_to_bytes` and `bytes_to_string` count the result with a pass that the part of the arguments already paid for. Without a limit nothing is computed.
- ⌈x/10⌉ is computed as `(x + 9) / 10` in `ulong`.

## Cost of the extension sets

The overloads of the sets carry their cost as those of the standard library do ([extensions](extensions.md)): `FixedCost` and `ArgumentCost` are charged by the call node before the implementation runs, the rest through `CallSite.Charge` while it runs, each part before the memory or the work it pays for. A test compares every binding of the catalog with the table below, and a snapshot records the units of each overload on the probes of [tests](tests.md#snapshots-of-the-extension-sets). The units are the rows of the cost table of the documentation; `Lₐ`, `Lᵣ`, n and k are those of its notation.

**Invalid arguments.** An argument that is invalid without any work on the data gives its error with the constant unit alone: `ArgumentCost` answers 0 for it. This holds for `lists.range` with n below 0 or above 1 000 000, for `slice` with an index that is negative, past the other or past the list, and for `flatten` with a negative depth. It does not hold for the string functions with an index or an offset: their bounds are in code points, whose check passes over the string, so the formula of the call is charged before the error.

| Overloads | Units | Charged |
| :- | :- | :- |
| `string_char_at_int`, `string_substring_int`, `string_substring_int_int`, `string_trim`, `string_lower_ascii`, `string_upper_ascii`, `string_reverse`, `strings_quote` | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉ | the first two terms as the argument cost, the receiver being Lₐ; Lᵣ known after the scan that finds the result, charged once before the string is created |
| `string_index_of_string`, `string_index_of_string_int`, `string_last_index_of_string`, `string_last_index_of_string_int` | 1 + ⌈Lₐ/10⌉ | Lₐ the lengths of the receiver and the argument; the search counts steps for its property, not units |
| `string_replace_string_string`, `string_replace_string_string_int` | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉ | Lₐ the lengths of the three strings; Lᵣ as the result grows, ⌈(T + l)/10⌉ − ⌈T/10⌉ before each piece of length l is appended; the result that equals the receiver is charged as a copy |
| `string_split_string`, `string_split_string_int` | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉ + k | Lₐ the receiver and the separator; Lᵣ the sum of the lengths of the parts and k their number, charged part by part before each is added |
| `list_join`, `list_join_string` | 1 + n + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉ | Lₐ the lengths of the elements and of the separator, one pass over the list in the argument cost; Lᵣ known from them before the string is created |
| `string_format` | 1 + n + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉ | [extension-format](extension-format.md#evaluation-and-cost) |
| `math_@max_double`, `_int`, `_uint`, the pairs and the mixed pairs, the same for `@min`; every other overload of `math` except the list forms | 1 | the fixed cost |
| `math_@max_list_double`, `_int`, `_uint`, and `@min` | 1 + n | the list size, in the argument cost |
| `list_slice` | 1 + k, k the size of the result | k from the indexes, O(1) |
| `lists_range` | 1 + n | n the argument |
| `list_reverse` | 1 + n | |
| `list_flatten`, `list_flatten_int` | 1 + v, v the sum of the sizes of the lists that `flatten` descends into: the receiver and the nested lists not deeper than the depth, which is 1 for `flatten()`. `[[1, 2], [3]].flatten()` is v = 2 + 2 + 1 = 5; `[[1, [2, 3]], [4]].flatten(2)` is v = 2 + 2 + 1 + 2 = 7 | the size of the receiver in the argument cost, the size of each nested list before its elements are viewed |
| `list_<T>_sort` | 1 + n + c · n · ⌈log₂(n + 1)⌉ | c is 1 for the kinds `int`, `uint`, `double`, `bool`, `duration`, `timestamp`; 1 + ⌈2 · Lmax/10⌉ for `string` and `bytes`, Lmax the longest element, found in one pass in the argument cost; ⌈log₂(n + 1)⌉ is the bit length of n |
| `list_<U>_sortByAssociatedKeys` | the same, n and Lmax of the list of keys, c by the kind `U` | |
| `list_distinct` | 1 + n + Σ eq | the first two terms in the argument cost; eq of each pair compared, in the reference order of [extension-library](extension-library.md#lists), before the comparison |
| `list_sets_contains_list`, `list_sets_intersects_list` | 1 + Σ (1 + Σ eq) | each looked-up element is an `in` by the row of `in_list`, in the order of [extensions](extensions.md#sets-version-0): `sets.contains([1, 2, 3], [1, 2])` is 6 |
| `list_sets_equivalent_list` | 1 + the sum of the first `contains` and, when it is `true`, of the second, each without its constant 1 | `sets.equivalent([1, 2], [2, 1])` is 11 |
| `base64_encode_bytes` | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉, Lᵣ = 4 · ⌈Lₐ/3⌉ | the whole in one step before the string is created |
| `base64_decode_string` | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉ | Lᵣ the number of bytes, known after the pass that validates the text, charged before the array is created |
| `json_encode_dyn` | 1 + ⌈Lᵣ/10⌉ | as the text grows; every node of the value gives a character, so the walk is inside the charge |
| `cel_block_list` | 0 | the slots and the result pay for themselves ([evaluation](evaluation.md#blocks)) |
| `@mapInsert_map_key_value` | 2 for the pair and ⌈L/10⌉ for a string key of length L | before the pair is put in; no constant unit |
| `@mapInsert_map_map` | 2m for m pairs and ⌈L/10⌉ for each string key | pair by pair; before the first pair, the enumeration of a map view or of a JSON object as for the range of a comprehension |
| `string_to_ip`, `string_to_cidr`, `is_ip`, `is_cidr`, `ip_is_canonical`, `cidr_contains_ip_string`, `cidr_contains_cidr_string` | 1 + ⌈Lₐ/10⌉ | the argument cost |
| `ip_to_string`, `cidr_to_string` | 1 + ⌈Lᵣ/10⌉ | Lᵣ known before the text is made |
| the other 12 overloads of `network` | 1 | the fixed cost |

**Macros.** The cost of a macro is that of its expansion ([extension-macros](extension-macros.md#cost)):

| Macro | Units beyond the expressions the author wrote |
| :- | :- |
| `math.greatest`, `math.least` | the call of `math.@max` or `math.@min`; with three arguments or more also the list literal of the arguments, 1 + n: `math.greatest(i, j, 3)` is the two reads 2, the list 1 + 3, the call of `math_@max_list_int` 1 + 3, 10 |
| `sortBy` | the receiver; the empty list that is the range of the binding, 1; the `map` of the keys as the standard macro charges it — 1 for the empty list, 1 for each step and the key expression, 1 for each key added; the call of `@sortByAssociatedKeys` by its row. The reads of the hidden names are part of the expansion and cost nothing: `[3, 1, 2].sortBy(e, e)` is 4 + 1 + 10 + 10 = 25 |
| `cel.bind` | the empty list that is the range, 1, charged although the planner may not create it; no step; each read of the bound name in the result 1: `cel.bind(x, 1, x + 1)` is 1 + 0 + (1 + 0 + 1) = 3 |
| `all`, `exists`, `existsOne`, `exists_one` with two variables | 1 for each executed step; `exists_one` has no short circuit |
| `transformList` | 1 for the empty list, 1 for each executed step, 1 for each element added |
| `transformMap`, `transformMapEntry` | 1 for the empty map, 1 for each executed step, then the rows of `cel.@mapInsert`; the read of the first iteration variable that the expansion puts into `cel.@mapInsert(@result, i, t)` costs nothing |

Measured memory is within B = 64 bytes for each unit in every row; the rows that allocate with the data — `replace`, `split`, `join`, `quote`, `slice`, `flatten`, `sort`, `range`, `distinct`, `format`, `json.encode`, the `transform` macros and the conversions to text of `network` — are listed in [tests](tests.md#cost-and-memory-of-the-extension-sets). A pooled array of the builders is rented in the steady state without allocation, and its first rental is not charged.

**Members of `OverloadBinding` for the sets.** Two optional members join the binding of [Tables](#tables): `ShallowArguments`, whether the planner marks no argument for the check at depth ([extension-library](extension-library.md)), and `Validator`, a `CallValidator` that the checker runs after the traversal ([checking](checking.md#calls-that-the-checker-validates)). Both are `false` and `null` for every overload of the standard library, and the snapshot of a version does not record them.

## Cost of `optional` and `block`

The rows of the set [optional](extension-optional.md) join those above and follow the same rules: `FixedCost` and `ArgumentCost` before the implementation, the rest through `CallSite.Charge`. A test compares each binding of the set with the table, and the snapshot of its version records the units of each overload on the probes.

| Overloads | Units | Charged |
| :- | :- | :- |
| `optional_of`, `optional_none`, `optional_value`, `optional_hasValue`, `list_first`, `list_last` | 1 | the fixed cost |
| `optional_ofNonZeroValue` | 1; an object 1 + f | the argument cost reads the kind of the argument: for an object, the number f of the fields of its description, which is known without reading a field |
| `optional_or_optional`, `optional_orValue_value` | 1 | by `OptionalOrPlanNode` after the receiver, before the argument ([evaluation](evaluation.md#optional-values-and-the-optional-syntax)); the argument is charged only when it is evaluated |
| `select_optional_field` | 1; a map 1 + ⌈L/10⌉; the rows of a JSON object | by `OptionalSelectPlanNode` as a selection is charged; no binding cost |
| `list_optindex_optional_int`, `optional_list_optindex_optional_int`, `optional_list_index_int` | 1 | the argument cost of `index_list`, over the held list for an optional operand and 1 for an empty one |
| `map_optindex_optional_value`, `optional_map_optindex_optional_value`, `optional_map_index_value` | 1; a string key of length L, 1 + ⌈L/10⌉; the rows of a JSON object | the argument cost of `index_map`, over the held map for an optional operand and 1 for an empty one |
| `optional_unwrap`, `optional_unwrapOpt` | 1 + n | the size of the list in the argument cost |
| the macros `optMap`, `optFlatMap` | by the table of [extension-macros](extension-macros.md#optional-and-block) | by the expansion |
| the optional elements, entries and fields | 1 + stored; 1 + 2m + ⌈L/10⌉ per string key stored; 1 + m + ⌈L/10⌉ | by the nodes of the literals; a dropped position costs only its own expressions |
| `cel_block_list` and the macros of `block` | as in the rows above and the rule of "Blocks" of [evaluation](evaluation.md#blocks) | each read of `cel.index(N)` is 1, a slot is paid once |

**The argument costs** are functions of `Costs.cs`. `OptionalIndexCost` takes the receiver and the key, unwraps an optional receiver, and answers what `index_list` or `index_map` answers for the held value, or 1 for an empty optional; the functions of the plain overloads are called, not copied, so the two never differ.

**`Matches` and the marks.** `OverloadBinding.Matches` reduces a parameter of the type `optional_type(E)` to its top level, `optional_type(dyn)`, as it reduces `list(E)` and `map(K, V)` ([Tables](#tables)): the check of the top level takes any optional and nothing else. The planner marks an argument for the check at depth when its static type has `dyn` or a wrapper inside `optional_type`, as inside `list` and `map`; the descent charges what the check of the held value charges ([values](values.md#optional)).

**Memory.** `optional.of` and `optional.ofNonZeroValue` allocate one box of 48 bytes inside their 1 unit, `optional.none` allocates nothing, `first` and `last` allocate the box alone, `optional.unwrap` and `unwrapOpt` a list of at most n values of 32 bytes inside their 1 + n units, and an index that finds a value allocates the box; each stays within B = 64 bytes per unit, and the rows of [tests](tests.md#tests-of-optional-and-block) measure them.

## Numbers and text

No culture takes part: the conversions use their own code or invariant formats.

- **`double` to text** is Go `%g` of cel-go: the shortest digits that read back to the same `double` (those of `double.ToString("R", CultureInfo.InvariantCulture)`) with the exponent e of the first digit; when e < −4 or e ≥ 6 the form is `d.ddde±XX`, without trailing zeros and with at least two digits of exponent, otherwise the plain decimal. `-0.0` is `-0`, the infinities `+Inf` and `-Inf`, NaN `NaN`. cel-go v0.32.0 gives `123456`, `1e+06`, `1e-05`, `0.1`, `1e+21` for `123456.0`, `1e6`, `1e-5`, `0.1`, `1e21`.
- **Text to `double`** checks the forms of the standard library page first — decimal, hexadecimal with a binary exponent, `_` between digits, `inf`, `infinity` and `nan` — then reads a decimal with `double.Parse` and `CultureInfo.InvariantCulture` after removing `_`, which rounds correctly; a finite text that reads as an infinity is `ConversionRange`. A hexadecimal form is read by own code, rounded to nearest, ties to even.
- **Integers to and from text** — own ASCII code: `[+-]?digits` for `int`, digits without a sign for `uint`, the whole string, no `_`, spaces or prefixes; `bool` takes the twelve strings of the page.

## Cost of `decimal`

The set [decimal](extension-decimal.md) carries its cost as the other sets do, with `FixedCost` and `ArgumentCost` charged before the call and one charge inside it. A value of the kind `Decimal` takes no memory beyond the 16 bytes of the payload of `CelValue`, so every overload but the two that read or write a string is a call of constant time with no allocation.

| Overloads | Units | Charged |
| :- | :- | :- |
| `add_decimal`, `subtract_decimal`, `multiply_decimal`, `divide_decimal`, `negate_decimal`, `less_decimal`, `less_equals_decimal`, `greater_decimal`, `greater_equals_decimal`, `int64_to_decimal`, `uint64_to_decimal`, `decimal_to_int64`, `decimal_to_uint64`, `decimal_to_double` | 1 | the fixed cost; the work is bounded by 34 digits and one operation on `UInt128` |
| `decimal_to_decimal` | 1 | the fixed cost; the identity returns its argument and copies nothing, as the identities of the standard library do |
| `double_to_decimal` | 4 | the fixed cost: the shortest text of a `double` has at most 24 characters, laid out and read on the stack; the number 4 is the one of the cost page, calibrated by the measurement of that page |
| `string_to_decimal` | 1 + ⌈Lₐ/10⌉ | `ArgumentCost` is `DecimalCosts.Text`, ⌈Lₐ/10⌉ of the length of the string, charged before the text is read; the string is read once, in time linear in its length, and the digits beyond the 34th are only looked at |
| `decimal_to_string` | 1 + ⌈Lᵣ/10⌉ | `CallSite.Charge` of ⌈Lᵣ/10⌉ for the text formatted on the stack, before the string is created; Lᵣ is at most 42, so the charge is at most 5 |

`Library.Decimals` cannot use `Costs` or the string helper of `Conversions`, which are in the parent namespace; `DecimalCosts` has the cost function of a string argument and the charge of a text, a few lines each, with the same result as `Costs.Text` and `Conversions.NewString`. The snapshot of the declarations records the units of each overload at its probes, a `decimal` argument being the fixed value `1.5` ([tests](tests.md#tests-of-decimal)), and the memory test of the extension sets has rows for `string_to_decimal` of a long text and for `decimal_to_string`, each within the 64 bytes of a unit.

## Time — `Library.Time`

The 47 overloads of time have the identifiers of cel-go, letter for letter: 9 conversions, 24 receivers, 6 operations `+` and `-`, 8 relations. `StandardLibraryV1` adds them to the functions `timestamp`, `duration`, `getFullYear` … `getMilliseconds`, `_+_`, `_-_`, `_<_`, `_<=_`, `_>_` and `_>=_` in the order of cel-go — the order of overloads inside a function in `common/stdlib/standard.go` of cel-go v0.32.0, so that `_+_` has `add_timestamp_duration` after `add_duration_timestamp`, `int` has `duration_to_int64` before `string_to_int64` and `timestamp_to_int64` before `uint64_to_int64`, and `string` has `duration_to_string` before `int64_to_string` and `timestamp_to_string` before `uint64_to_string`; the order of the functions themselves is not observable. They follow "Implementations and cost": each reads its arguments with the `TryGet…` methods, answers `EvalNoMatchingOverload` for another kind, throws nothing and keeps no state. The place of every error below is the call: the operator of `_+_`, the `(` after the name of a function or a receiver. The value rules — the text of time, the calendar, the zone — are in [values](values.md) and [time zones](time-zones.md); this table says which rule each overload calls and what it costs.

| Overloads | Implementation | Errors | Cost |
| :- | :- | :- | :- |
| `timestamp_to_timestamp`, `duration_to_duration` | identity, without a copy | — | 1 |
| `int64_to_timestamp` | Unix seconds, nanoseconds 0 | `ConversionRange` (3007) `{value} is out of the range of timestamp` outside −62 135 596 800 … 253 402 300 799, `{value}` the number | 1 |
| `timestamp_to_int64`, `duration_to_int64` | the `Seconds` of the timestamp, the floor of its Unix time; the nanoseconds of the duration | — | 1 |
| `string_to_timestamp`, `string_to_duration` | `TimeTextRules`, [values](values.md), "Text of time" | `InvalidArgument` (3008) `invalid argument to 'timestamp': cannot parse {text} as timestamp`, `… to 'duration': cannot parse {text} as duration`; `ConversionRange` (3007) `{value} is out of the range of timestamp` or `… of duration`, `{value}` the string | 1 + ⌈Lₐ/10⌉ |
| `timestamp_to_string`, `duration_to_string` | `TimeTextRules`, one string of at most 30 characters | — | 1 + ⌈Lᵣ/10⌉, Lᵣ from the value |
| `add_timestamp_duration`, `add_duration_timestamp`, `subtract_timestamp_duration` | shift of the moment, below | `Overflow` (3006) `timestamp overflow` | 1 |
| `subtract_timestamp_timestamp` | the difference as a duration, below | `Overflow` `duration overflow` | 1 |
| `add_duration_duration`, `subtract_duration_duration` | 64-bit sum and difference, overflow found by the signs of the operands and the result | `Overflow` `duration overflow` | 1 |
| `less_timestamp`, `less_equals_timestamp`, `greater_timestamp`, `greater_equals_timestamp`, and the same four with `duration` | `OrderingRules` | — | 1 |
| the 14 receivers without a zone, the 10 with a zone | below | with a zone: `TimeZoneNotFound` (3022), `InvalidTimeZone` (3023) | without a zone 1; with a zone 1 + ⌈L/10⌉, L the length of the zone string |

**Arithmetic.** A duration `d` splits into `ds = floor(d / 10⁹)` and `dn = d − ds · 10⁹`, `0 ≤ dn < 10⁹`, by floor division that has no overflow. `ts + d` and `d + ts`: `n = Nanoseconds + dn`, carry 1 when `n ≥ 10⁹` and then `n −= 10⁹`, `Seconds + ds + carry`. `ts − d`: `n = Nanoseconds − dn`, borrow 1 when `n < 0` and then `n += 10⁹`, `Seconds − ds − borrow`. The sums of seconds cannot overflow `long`: `|ds|` is below 9.3 · 10⁹ and `|Seconds|` below 2.6 · 10¹¹. A result outside −62 135 596 800 … 253 402 300 799 seconds is `timestamp overflow`; the nanoseconds are always inside. `ts − ts'`: `Int128` of `(Seconds − Seconds') · 10⁹ + (Nanoseconds − Nanoseconds')`; outside `long` is `duration overflow`. There is no other arithmetic of time: no sum of two moments, no product, no negation of a duration; the checker says `NoMatchingOverload`. The relations are those of "Ordering" in [values](values.md): a timestamp against a timestamp, a duration against a duration.

**Receivers.** Without a zone the offset is 0. With a zone, the second argument is a string: `TimeZoneRules` resolves it against `CallSite.TimeZones` ([time zones](time-zones.md), "Resolving a zone name") for each call, constant or not, and gives the offset at `Seconds` ([time zones](time-zones.md), "The offset of a zone at an instant"); `TimeZoneNotFound` and `InvalidTimeZone` are the errors of that step, after the kinds of the arguments are checked, and they carry the zone string and the database as parts of the message. The fields are those of the local time `Seconds + offset` ([time zones](time-zones.md), "The calendar"), unclamped. Month, day of the year and `getDayOfMonth` count from zero, `getDate` from one, the day of the week from 0 for Sunday:

| Function | Identifiers | Value |
| :- | :- | :- |
| `getFullYear` | `timestamp_to_year`, `timestamp_to_year_with_tz` | the year of the local date |
| `getMonth` | `timestamp_to_month`, `timestamp_to_month_with_tz` | month − 1 |
| `getDayOfYear` | `timestamp_to_day_of_year`, `timestamp_to_day_of_year_with_tz` | days since 1 January of that year |
| `getDayOfMonth` | `timestamp_to_day_of_month`, `timestamp_to_day_of_month_with_tz` | day − 1 |
| `getDate` | `timestamp_to_day_of_month_1_based`, `timestamp_to_day_of_month_1_based_with_tz` | the day of the month |
| `getDayOfWeek` | `timestamp_to_day_of_week`, `timestamp_to_day_of_week_with_tz` | 0 to 6, 0 Sunday |
| `getHours` | `timestamp_to_hours`, `timestamp_to_hours_with_tz`; `duration_to_hours` | the local hour 0–23; the whole hours of the duration, truncated toward zero |
| `getMinutes` | `timestamp_to_minutes`, `timestamp_to_minutes_with_tz`; `duration_to_minutes` | the local minute; the whole minutes, truncated toward zero |
| `getSeconds` | `timestamp_to_seconds`, `timestamp_to_seconds_tz` (no `with`, as in cel-go); `duration_to_seconds` | the local second; the whole seconds, truncated toward zero |
| `getMilliseconds` | `timestamp_to_milliseconds`, `timestamp_to_milliseconds_with_tz`; `duration_to_milliseconds` | `Nanoseconds / 10⁶`, which a zone does not change; for a duration `(nanoseconds % 10⁹) / 10⁶` with the sign of the dividend, so `duration('1.234s')` gives 234 and `duration('-1.234s')` gives −234 — the part within the second, as the language definition says |

**Cost.** The cost of every overload is charged before it runs, by the three ways of "Implementations and cost". `FixedCost` is 1 for all 47; the argument part is a cost function of `TimeCosts`: for `string_to_timestamp` and `string_to_duration` and for the ten receivers with a zone, ⌈Lₐ/10⌉ over the string arguments, so the zone string alone for a receiver; for `timestamp_to_string` and `duration_to_string`, ⌈Lᵣ/10⌉ with Lᵣ computed from the argument without making the string — 20 for whole seconds, else 21 plus the digits of the fraction without trailing zeros; for a duration the sign, the digits of `m / 10⁹`, and 1 plus the digits of the fraction when there is one, plus 1 for the `s`. A call that the planner folds charges the same units ([evaluation](evaluation.md), "Constants of time"). The memory of the 47 stays within B per unit: only the two `string()` overloads allocate, one string of at most 30 characters against at least 2 units, and an error value is at most 64 bytes, so the 1-unit overloads that fail fit.

## Substring search

`contains` uses `internal static int SubstringSearch.IndexOf(ReadOnlySpan<char> text, ReadOnlySpan<char> pattern, ref long steps)`, which the functions `indexOf`, `lastIndexOf`, `replace` and `split` of the extension set `strings` will share; the .NET search methods are banned in the library, because their worst case is O(n · m) and the cost formula 1 + ⌈Lₐ/10⌉ assumes linear time.

- The algorithm is Knuth–Morris–Pratt over UTF-16 code units: the prefix function of the pattern, then one pass over the text. A step is one comparison of two code units. The prefix function takes at most 2m steps and the pass at most 2n, so a call takes at most 2(n + m), half the bound 4 · (n + m) that its property test checks. An empty pattern is found at 0 without steps; a pattern longer than the text is not found without steps.
- In the state "nothing matched" the pass may jump to the next occurrence of the first code unit of the pattern with `MemoryExtensions.IndexOf(char)`, which is vectorized and linear; each code unit it passes counts one step, so the bound becomes 3n + 2m and stays under the limit.
- The prefix function of a pattern up to 256 code units lives on the stack, a longer one in an array of m `int`s: 4m bytes, within B = 64 bytes for each of the m/10 units that the pattern alone adds to the cost.
- The step counter is a parameter that the property test reads; the library passes a local and ignores it. It is a `long`: the bound 4 · (n + m) of long inputs does not fit an `int`.

## `matches` and `matches_string`

Two overloads of one function: `matches` is the global call `matches(string, string) -> bool`, `matches_string` the call from a receiver `string.matches(string) -> bool`. Both mean: some part of the subject matches the pattern, by the syntax and the semantics of RE2 ([regex-syntax](regex-syntax.md)) run by the linear engine ([regex-engine](regex-engine.md)). In the table they follow `starts_with_string`, in the order of cel-go, and the library has 162 overloads with them. The subject is the first argument in both forms, the receiver counting as one; the pattern is the second.

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Library/Matches.cs` | the implementation shared by both overloads: the three charges, the cache, the engine |

`OverloadIds` has `Matches` and `MatchesString` as constants of `matches` and `matches_string`. Each binding has `FixedCost` 0 and no `ArgumentCost`: the cost depends on what the pattern turns out to be, so the implementation charges it through `CallSite.Charge`, step by step. Each binding also carries the constant argument at index 1 ([declarations](declarations.md), "Constant arguments"), which makes a pattern that is a string literal be prepared at compilation.

**Steps.** The implementation reads the subject and the pattern as strings — another kind is `EvalNoMatchingOverload`, as for every implementation — and then:

1. **A prepared pattern.** `CallSite.Prepared` holds the program of a pattern that was a literal, so nothing is parsed or built. The only charge is the match.
2. **A pattern from data.** The pattern has L UTF-16 code units. The implementation charges the parse, `1 + ⌈L/10⌉`, and only then looks the pattern up in the cache of the environment, `CallSite.Cache`. A hit gives an entry of a program, with its size m, or an entry of an error. A miss parses the pattern ([regex-engine](regex-engine.md), "The cache of patterns"): an error is kept as an entry of the cache; a program, with the size m known after the parse and checked against the limit, goes on.
3. **A refused pattern** — an entry of an error, found or just made — is `EvalInvalidRegex` (3021). Nothing more is charged.
4. **The charges of the build and of the match.** The size m is known after the parse, and so is everything the two charges need, so both are made before the program is built: the build `⌈m/4⌉`, then the match `1 + ⌈(n + 1) · m/10⌉`, n the length of the subject in UTF-16 code units.
5. **The build and the match.** On a miss the implementation builds the program and keeps it in the cache; then it runs the engine and returns the `bool`.

Each charge comes before the work it pays for, so a limit that stops the evaluation stops it before the parse, the build or the match. Both charges of step 4 are made before the build, so a limit that the match would pass stops the call before a program is built or kept: nothing is built, nothing is kept in the cache and every rented array is returned, whichever of the three charges stops it. The order of the charges and the result are the same as with the build in between; only the work and the cache differ. Without a limit no formula is computed. The cache changes no charge: a hit pays the parse and the build as a miss does, and an entry of an error pays the parse alone, whether it was made now or by an earlier evaluation. n, L and m are the lengths and the size of the standard; the step counter of the engine is not a charge.

**Cost.** The whole table of the operations of `matches`, in units; the nodes around the call — a literal 0, a variable read 1, the other operands by their own rows — are charged by the nodes:

| Operation | Units | When |
| :- | :- | :- |
| the pattern is a string literal, prepared at compilation | the match, `1 + ⌈(n + 1) · m/10⌉` | each evaluation; the parse and the build were made at compilation and are not charged |
| the pattern is not a literal: a variable, `dyn('…')`, a concatenation, a `?:`, an element of a list or a map, any call | the parse `1 + ⌈L/10⌉`, the build `⌈m/4⌉`, the match `1 + ⌈(n + 1) · m/10⌉` | each evaluation, the cache hit included |
| the pattern from data is refused: a syntax error, a construct outside RE2, a count, the height or the size over the limit | the parse `1 + ⌈L/10⌉` only | each evaluation, from the cache as well |
| the call is evaluated without type checking, the pattern a literal or not | as the pattern that is not a literal | each evaluation |
| an operand is an error value, or an operand of another kind (`EvalNoMatchingOverload`) | nothing beyond the operands | — |

With the pattern `abc` (m = 4) and the subject `xabcx` (n = 5): as a literal the call costs the match, `1 + ⌈6 · 4/10⌉ = 4`, and 5 with the read of the subject; from a variable it costs the parse `1 + 1 = 2`, the build 1 and the match 4, which is 7, and 9 with the reads of the subject and of the pattern. The ⌈x/10⌉ and ⌈m/4⌉ are computed as `(x + 9) / 10` and `(m + 3) / 4` in `ulong`. A sum that overflows is a limit exceeded, as everywhere.

**Memory.** The parse and the engine take their arrays from `ArrayPool<int>.Shared`; what a call allocates and keeps is the program of a miss — 16 bytes for each unit of m, which the build charge pays — and the entry of the cache, the node of its dictionary and its slot in the ring, one constant amount of a few tens of bytes, which the units of the parse and the match cover. A hit allocates nothing in steady state. The allocation test of cost and memory ([tests](tests.md)) has `matches` rows on subjects of 0, 1, 10, 100 and 1 000 units and on patterns of the sizes 2, 10, 100 and 1 000; as for every case that ends in an error value, a refused pattern adds the constant `ErrorBytes` of that test: the error value, the entry of an error in the cache and the one string that a Perl error holds are a constant that does not grow with the data and is not charged.

**Errors.** `EvalInvalidRegex` (3021), at the call — its `(` for the receiver form too — with the message `invalid regular expression at offset {offset}: {reason}`; the offset and the reason are those of the `RegexError` in the entry, held by reference, so the error value allocates nothing for them and the message is made when it is read ([evaluation](evaluation.md), "Errors as values"). The same text, with the code `InvalidRegex` (2009) at the literal of the pattern, is the compile error of a prepared pattern ([checking](checking.md)); the two do not differ in text. `EvalNoMatchingOverload` (3000) as for any call, with the types of the values by name.

**Without type checking.** `EvaluateWithoutTypeCheck` plans no preparation, so a literal pattern is a pattern from data: a literal with an error is `EvalInvalidRegex` at the call; a value that is not a string is `EvalNoMatchingOverload` by the dispatch over all overloads of the name ([evaluation](evaluation.md), "Evaluation without type checking"); `matches('abc')` with one argument is `EvalNoMatchingOverload` too.

**A constant that is not a literal.** `dyn('(')`, `string('(')`, `'(' + ''` and `true ? '(' : 'a'` have the value of a literal but are not prepared: their error is `EvalInvalidRegex`, at evaluation. The planner does not fold them: folding would make the class of the outcome — compile error or evaluation error — differ from that of the oracle, which looks only at string literals.

**The snapshot.** The overloads of version 1 are kept in a file that a test compares with the table, so a later change of an identifier, a signature or a cost is seen: `tests/Great.Parts.Cel.Tests/Library/Snapshots/standard-library-v1.txt`, one line for each overload, in the order of the table: the function, the identifier, `global` or `receiver`, the parameter types and the result type in their text form, `dyn` when the overload is a candidate only with a `dyn` argument, and the cost — the units that a call charges, fixed and by arguments and through `CallSite.Charge`, on probe arguments: every argument of the kinds `string`, `bytes`, `list` and `map` of size 0, 1, 10, 11 and 100 at once, the other kinds one fixed value, written as five numbers; for `matches` the subject and the pattern are `a` repeated that many times.

The format of a line, which every increment that adds overloads keeps: six fields separated by ` | ` — the function, the identifier, `global` or `receiver`, `(parameter types, the receiver first) -> result type`, `dyn` for an overload that is a candidate only with a `dyn` argument and `-` for any other, and the five costs separated by single spaces, in the order of the sizes 0, 1, 10, 11, 100. A line that begins with `#` is a comment, and the file begins with the comments that state this format. The probes are a list of N elements, each the `int` 1; a map of N entries, the keys the `int`s 0 to N − 1 and the values the `int` 1; `string` and `bytes` of N characters `a`; `bool` is `true`, `uint` is 7, `double` is 2.5, and every other kind, a type parameter included, is the `int` 7. The cost is what the call charges whatever the probe gives, an error included: the fixed units, the cost of the arguments and what the implementation charges through the call site. `LibrarySnapshotTests.StandardLibraryV1` reads the file and the table and names every line that differs, with both texts. The developer who writes the test makes the lines from the table once and reads their numbers against the cost page; each part that adds overloads adds its lines in the order of the table. Before a release the file grows; after it a line changes only by review.

## Rows of the standard library page, section 7, to rules and examples

Every row of the tables 7.1, 7.2 and 7.3 of the [standard library](../standard-library.md) page has a rule above and an example. The examples are in `tests/Great.Parts.Cel.Tests/Examples/`; the rows of `TimeConversionTests`, `TimeReceiverTests` and `TimeArithmeticTests` are labeled by the overload identifier, so a row is found by it.

| Rows of the page | Rule | Examples |
| :- | :- | :- |
| 7.1 `timestamp_to_timestamp`, `duration_to_duration` | identity, "Time" table | `Library/TimeConversionTests` |
| 7.1 `int64_to_timestamp`, `timestamp_to_int64`, `duration_to_int64` | "Time" table | `Library/TimeConversionTests`; the range in `Library/TimeErrorTests` |
| 7.1 `string_to_timestamp`, `timestamp_to_string`, `string_to_duration`, `duration_to_string` | [values](values.md), "Text of time" | `Library/TimeConversionTests`; errors in `Library/TimeErrorTests`; the register entries `timestamp_to_string`, `duration_to_string` and, of 7.2, `duration_to_milliseconds` in `Library/TimeKnownDifferenceTests` |
| 7.2 the ten receivers of `timestamp` without a zone, `duration_to_hours`, `duration_to_minutes`, `duration_to_seconds`, `duration_to_milliseconds` | "Receivers" | `Library/TimeReceiverTests` |
| 7.2 the ten receivers of `timestamp` with a zone: `…_year_with_tz`, `…_month_with_tz`, `…_day_of_year_with_tz`, `…_day_of_month_with_tz`, `…_day_of_month_1_based_with_tz`, `…_day_of_week_with_tz`, `…_hours_with_tz`, `…_minutes_with_tz`, `…_seconds_tz`, `…_milliseconds_with_tz` | "Receivers"; [time zones](time-zones.md) | `UTC` and fixed offsets in `Library/TimeReceiverTests`; names in `Library/TimeZoneNameTests`; the zones of a database in `Library/TimeZoneOffsetTests`; dyn, constants and no database in `Evaluation/TimeZoneEvaluationTests`; `""`, `Local` and `+1:5` in `Library/TimeKnownDifferenceTests`; the codes 3022 and 3023 in `Library/TimeErrorTests` |
| 7.3 `UTC`, `[+-]?HH:MM`, any other string | [time zones](time-zones.md), "Resolving a zone name" | `Library/TimeZoneNameTests`, `Evaluation/TimeZoneEvaluationTests` |
| 7.4 limits; 6.3 time rows `add_duration_duration`, `add_duration_timestamp`, `add_timestamp_duration`, `subtract_duration_duration`, `subtract_timestamp_duration`, `subtract_timestamp_timestamp` | "Time" table, "Arithmetic" | `Library/TimeArithmeticTests`; overflow in `Library/TimeErrorTests` |
| 6.4 time rows, the eight `less_`, `less_equals_`, `greater_`, `greater_equals_` of `timestamp` and `duration`; equality | `OrderingRules`, `EqualityRules` ([values](values.md)) | `Values/TimeEqualityOrderingTests` |
| 4 `google.protobuf.Timestamp`, `google.protobuf.Duration` | "Tables" | `Library/TimeTypeIdentifierTests`; a variable of that name in `Declarations/TimeDeclarationTests` |
| the cost page, section 5, the rows of time | "Time", "Cost" | `Library/TimeCostTableTests` |

## The set `protos`

`protos`, version 0, has two macros, no function, no overload and no abstract type, and depends on no other set. It is a syntactic set: the macros turn the call into a selection of a field whose name has dots, and the field itself comes from a type provider — in practice the protobuf package, whose extension fields have the full name of the extension for their name ([protobuf](protobuf.md#proto2-extensions)). The set joins the catalog of the extension sets ([extensions](extensions.md#the-catalog)): a family `protos` with one row, in `Library/Extensions/ProtosExtension.cs`, and its name `protos` joins `ExtensionCatalog.Names`, so a description in JSON may name it ([configuration](configuration.md#extension-sets)). `CelExtensions.Protos(version)` of the skeleton is the call of the catalog, as every other member of `CelExtensions` is; a version other than 0 is the missing definition and `Build` reports `UnknownExtensionVersion` (4004), `extension 'protos' has no version {version}; available versions: 0`. The set has no check of its own.

**The macros** are in `Syntax/ExtensionMacros.cs`, by the model of [extension-macros](extension-macros.md#the-model), and apply in an environment that has the set:

| Macro | Key | Applies when | Expansion |
| :- | :- | :- | :- |
| `proto.getExt(m, a.b.c)` | the receiver call `getExt` with 2 arguments, namespace `proto` | the target is the identifier `proto` | the selection `m.a.b.c`: a `SelectNode` of `m` whose field name is the text `a.b.c`, the names of the chain joined by `.` |
| `proto.hasExt(m, a.b.c)` | the receiver call `hasExt` with 2 arguments, namespace `proto` | the same | the presence test `has(m.a.b.c)`: the same `SelectNode` as a test |

The second argument must be a chain of selections that ends in an identifier — `cel.expr.conformance.proto2.int32_ext` is a `SelectNode` of `SelectNode`s of an `IdentNode` — with at least one selection and no presence test among them: a bare identifier, a call, a constant, a list or a `has` inside the chain is not one. The names are joined as the nodes have them, so a leading `.` of the root identifier stays in the first name and gives a name that no field has. cel-go accepts exactly this. The expansion creates one node, the selection, with the offset of the `(` of the call; the argument chain is not part of it, and `ParsedExpression` records the call as written under the id of the expansion. The first argument is the operand as written. The nodes are the same as those of the call written with backquotes, `` m.`a.b.c` ``, which the parser gives for an escaped field name, so type checking and evaluation need nothing for the macros: the field is looked up by that name in the description of the type of `m` ([checking](checking.md#rules-per-node)); a type without it is `NoSuchField` at the selection, and a `dyn` operand without the field `EvalNoSuchField`.

**The error** of a second argument that is not such a chain is `InvalidMacroCall` (1006), `invalid call of macro 'getExt': invalid extension field` or `… 'hasExt': …`, at the second argument — the place cel-go reports; the call is not expanded, and parsing goes on. A call with another number of arguments, a target that is not the identifier `proto`, and the macro name in an environment without the set are no macro: the call stays a call, and the checker reports an unknown function, `UndeclaredReference` for `proto.getExt`.

**Cost.** A macro costs what its expansion costs ([extension-macros](extension-macros.md#cost)): the selection or the presence test is 1, as any field selection, besides the cost of `m`; the identifier `proto` and the chain of the extension name are not read and cost nothing. `proto.getExt(m, a.b.c)` is the cost of `m` plus 1, and the unit is the same for a message of a type described at run time, for which the field is whatever its description says. The cost table has no row for the set beyond this.

Related pages: [regex-syntax](regex-syntax.md), [regex-engine](regex-engine.md), [evaluation](evaluation.md), [checking](checking.md), [values](values.md), [time zones](time-zones.md), [declarations](declarations.md), [core](core.md).
