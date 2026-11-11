# Extension sets `math`, `lists` and `encoders`

The functions of three sets: `math` version 3, `lists` version 4 and `encoders` version 1. Identifiers, signatures and versions are on the `docs/extensions.md` page; the costs are the rows of [library](library.md#cost-of-the-extension-sets); the macros `math.greatest`, `math.least` and `sortBy` are on [extension-macros](extension-macros.md); the model of a set is on [extensions](extensions.md). This page gives the behavior at the edges, the errors, and the algorithms where the result depends on them.

| File in `src/Great.Parts.Cel/Library/Extensions/` | Content |
| :- | :- |
| `MathExtension.cs`, `MathFunctions.cs` | the table of `math` (58 overloads) and the implementations |
| `ListsExtension.cs`, `ListFunctions.cs`, `ListSort.cs` | the table of `lists` (22 overloads), the implementations, the sort (`IndexSort`, shared with the JSON encoder) and `Saturated`, the arithmetic of units that stops at the largest value |
| `EncodersExtension.cs`, `Base64Codec.cs`, `JsonEncoder.cs` | the table of `encoders`, the two-attempt decoder of base64, the encoder of JSON |

The constants of the limits live one layer below, in `src/Great.Parts.Cel/Library/ExtensionLimits.cs` (namespace `Great.Parts.Cel.Library`), because the sets `strings` and `lists` both read them and the layer rules do not let `Library.Extensions.Strings` use `Library.Extensions`: `MaxRangeSize = 1_000_000`, `MaxFormatPrecision = 100` (used by [extension-format](extension-format.md)).

**Shallow arguments.** `OverloadBinding` gets a flag `ShallowArguments`. The planner never marks an argument of such an overload for the check at depth ([evaluation](evaluation.md#calls)); `Matches` takes the top level only, and the implementation reads the elements and refuses a kind it does not take with `EvalNoMatchingOverload`. Eight overloads have it: the six list forms of `math.@max` and `math.@min`, whose three overloads per function share one implementation that reads any list of numbers, and `list_flatten` and `list_flatten_int`. cel-go does the same, since a function of its extensions checks no more than the kind of the list, and the cel-spec cases depend on it: `math.greatest([dyn(5.4), dyn(10), dyn(3u)])` has a `list(dyn)` that fits none of `list(int)`, `list(uint)` and `list(double)` by element, and is `10`; `dyn([1, 2]).flatten()` is `[1, 2]`. The other overloads of the three sets are checked at depth as usual: a `dyn` list that does not fit `list(string)` is `EvalNoMatchingOverload`, where cel-go gives an error of its own — both are errors.

## `math`

`math.@max` and `math.@min` have 15 overloads each: 3 of one argument, 3 pairs of one kind, 6 mixed pairs with the result `dyn`, and 3 list forms. Errors of a function are `InvalidArgument` (3008) unless named otherwise, at the call. The macros are expanded by the parser; they produce calls of `math.@max` and `math.@min`, which text cannot write.

| Overloads | Behavior |
| :- | :- |
| `math_@max_double`, `_int`, `_uint`, and `@min` | the argument itself, whatever it is: `math.greatest(nan)` is NaN |
| `math_@max_double_double`, `_int_int`, `_uint_uint`, six mixed pairs with the result `dyn`, and `@min` | `@max(a, b)` is `b` when `a < b`, otherwise `a`; `@min(a, b)` is `b` when `a > b`, otherwise `a`. A tie gives the first argument with its own kind: `math.greatest(1, 1.0)` is the `int` `1`, `math.greatest(-0.0, 0.0)` is `-0`. The comparison is `OrderingRules`, so a pair across kinds compares as `<` does, `int` with `double` by rounding the `int` to `double` as cel-go does ([values](values.md)). A NaN in either argument is an error: `invalid argument to 'math.@max': NaN values cannot be ordered` |
| `math_@max_list_double`, `_list_int`, `_list_uint`, and `@min` | a fold over the elements in order by the pair rule above; one element is returned without a comparison, a NaN among two or more elements is the error of the pair rule at the first comparison that meets it; the empty list is `invalid argument to 'math.@max': math.@max(list) argument must not be empty`; an element that is not an `int`, `uint` or `double` is `EvalNoMatchingOverload`. The elements may be of different kinds: the result is the winning element with its own kind |
| `math_ceil_double`, `math_floor_double`, `math_trunc_double` | `Math.Ceiling`, `Math.Floor`, `Math.Truncate`; the sign of a zero result stays: `math.ceil(-0.5)` is `-0` |
| `math_round_double` | half away from zero, `Math.Round(x, MidpointRounding.AwayFromZero)`: `2.5` is `3`, `-2.5` is `-3`, `0.49999999999999994` is `0`, `-0.4` is `-0` |
| `math_isInf_double`, `math_isNaN_double`, `math_isFinite_double` | `double.IsInfinity`, `double.IsNaN`, and neither of them |
| `math_abs_double` | the magnitude; `-0.0` gives `0` |
| `math_abs_int` | the magnitude; `-9223372036854775808` is `Overflow` (3006), `integer overflow` |
| `math_abs_uint` | the argument |
| `math_sign_double` | NaN for NaN, `1.0` above 0, `-1.0` below, `0.0` otherwise — also for `-0.0`, which gives the positive zero |
| `math_sign_int`, `math_sign_uint` | `-1`, `0`, `1` as an `int`; `0`, `1` as a `uint` |
| `math_sqrt_double`, `_int`, `_uint` | `Math.Sqrt` of the value as a `double`; a negative number gives NaN, not an error |
| `math_bitAnd_int_int`, `_uint_uint`, `math_bitOr_…`, `math_bitXor_…` | the operators `&`, `\|`, `^` |
| `math_bitNot_int_int`, `_uint_uint` | `~` |
| `math_bitShiftLeft_int_int`, `_uint_int` | the shift of the first argument by the second, an `int`: negative — `math.bitShiftLeft() negative offset: {n}`; 64 and above — 0. The `int` is shifted as its 64 bits, so `1 << 63` is `-9223372036854775808` |
| `math_bitShiftRight_int_int`, `_uint_int` | the same errors; a **logical** shift also for the `int`: `math.bitShiftRight(-1, 1)` is `9223372036854775807`; 64 and above — 0 |

C# masks the count of a shift: `1L << 64` is `1`. The code tests `n >= 64` first and answers 0; a test pins `1 << 64` and `-1 >> 64` as `0`. Every function but the list forms costs 1; the list forms 1 + n.

## `lists`

Errors are `InvalidArgument` (3008) at the call, `invalid argument to '{function}': {detail}`, with the detail given below. The results are new lists of an array filled once ([values](values.md)); a list that is the argument and the result of an empty case may be returned itself.

| Overload | Behavior | Errors |
| :- | :- | :- |
| `list_slice` | the elements from the first index to the second, the end excluded | checked in this order: a negative index — `cannot slice with a negative index {i}`, `i` the start if it is negative, else the end; start above end — `cannot slice: start index {start} must be less than or equal to the end index`; end above the length — `cannot slice: end index {end} is above the list length {n}`, `n` read from the list by its reference when the error is read |
| `list_flatten` | each element that is a list replaced by its elements, one level; an element that is not a list stays: `[[1, [2]], [3]]` gives `[1, [2], 3]`; the cost is 1 + v with v the sum of the sizes of the lists descended into, 5 for `[[1, 2], [3]]` | — |
| `list_flatten_int` | the same to the given depth: 0 copies the list, 1 is `list_flatten`, 2 also flattens the lists inside those, and so on | a negative depth — `level must be non-negative` |
| `list_<T>_sort`, 8 overloads | the elements ordered ascending; see the sort below | below |
| `list_<U>_sortByAssociatedKeys`, 8 overloads | the receiver ordered by the associated list of keys, which has the type `list(U)` | below |
| `lists_range` | `[0, 1, …, n − 1]` | n below 0 — `invalid argument to 'lists.range': size must be non-negative, got {n}`; n above `MaxRangeSize` — `invalid argument to 'lists.range': size {n} exceeds maximum allowed (1000000)` |
| `list_reverse` | the elements in reverse order | — |
| `list_distinct` | the first of each group of equal elements, in the order of first appearance | — |

`<T>` and `<U>` are the eight types `int`, `uint`, `double`, `bool`, `google.protobuf.Duration`, `google.protobuf.Timestamp`, `string`, `bytes`. The overloads of `duration` and `timestamp` need the time types of the part that brings them.

**`flatten`** walks the list with an explicit stack in an array, not by recursion: the depth of a list nested in the application data is not bounded by the expression. The size of every list is charged before its elements are viewed.

**Sort.** `list_<T>_sort` is `@sortByAssociatedKeys` with the list as its own keys. For the receiver and the keys:

1. the lengths differ — `@sortByAssociatedKeys() expected a list of the same size as the associated keys list, but got {a} and {b} elements respectively`;
2. the empty list is returned as it is;
3. the first key is not one of the eight comparable kinds — `list elements must be comparable`; a key of a kind other than that of the first — `list elements must have the same type`, so `int` and `uint`, or `int` and `double`, are never mixed. From the text of an expression these two errors cannot be reached: the overloads are checked by element, so a list of mixed or incomparable elements is `EvalNoMatchingOverload` at the call; the checks guard a direct call;
4. the order is that of `OrderingRules` for the kind — numbers by value, strings by code points, `bytes` by unsigned bytes, `false` before `true`, time by instant — and is **stable**: elements with equal keys keep their order. For `string` and `bytes` keys the length of the longest key is found by a pass of its own that charges before the keys are materialized; the sort reads them again.

A NaN among keys is no error; the comparison `less(a, b)` is false when either is a NaN, and the order that results is the deterministic one of the algorithm below, not a sorted one: `[3.0, 1.0, nan, 2.0].sort()` is `[1.0, 3.0, nan, 2.0]`, as in cel-go for lists of up to 12 elements. The algorithm is the code's own, with no `Array.Sort`, `List.Sort` or LINQ — their comparers may throw on an inconsistent order: for 12 elements or fewer an insertion sort over an `int[]` of indices, taken from a pool, which for each `i` from 1 moves the element left while `less(element, previous)`; above 12 a top-down merge sort over the indices with a second pooled array, taking the right element only when it is less than the left. Both are stable and both leave a list with a NaN in a fixed order. Lists above 12 elements with a NaN or with equal keys of different elements may be ordered by cel-go differently, since its sort is not stable there. This is a deliberate difference, recorded in the registry of known differences as nine records: `list_double_sort` and the eight `list_<U>_sortByAssociatedKeys`; the profile of the differential run keeps lists with such keys to 12 elements ([eng-difftest](eng-difftest.md#profile-of-the-extension-sets)).

**Distinct** compares each element, in order, with the elements kept so far, in the order they were kept, up to the first equal one, by `EqualityRules` with the meter of the call site, which charges each pair: CEL equality, so `1`, `1u` and `1.0` are one element and NaN equals nothing, each NaN staying. Time is quadratic in the number of different elements, which the charge shows.

**Sizes and memory.** The arrays of indices and of the merge are pooled; the result takes 32 bytes an element. `lists.range(n)` takes 32n bytes for 1 + n units. A request above the limit is refused before anything is charged beyond the constant unit: the argument cost is `n` for `0 ≤ n ≤ 1 000 000` and 0 otherwise, so the error comes with the 1 unit of the call.

## `encoders`

| Overload | Behavior | Errors |
| :- | :- | :- |
| `base64_encode_bytes` | the standard alphabet with `=` padding, no line breaks; empty bytes give `""` | — |
| `base64_decode_string` | the two attempts below | `invalid argument to 'base64.decode': illegal base64 data at input byte {i}`, `{i}` the offset in UTF-16 units of the input at which the second attempt stopped |
| `json_encode_dyn` | the JSON text of the value, below | `invalid argument to 'json.encode': {detail}` |

**Decoding.** The first attempt decodes with padding, the second without; the second runs when the first fails and its error is the error of the call. Both skip the characters `\r` and `\n` anywhere and accept nothing else outside the alphabet `A–Z a–z 0–9 + /` and the padding: a space, a tab, a non-ASCII character, `-` and `_` are errors. Both are lenient about the bits that the last character leaves unused, as Go's `StdEncoding` is: `aGVsbG9=` decodes as `hello`. Padded: the data characters come in groups of four; the last group may have two characters followed by `==`, or three followed by `=`; nothing but `\r` and `\n` may follow the padding. Unpadded: no `=` at all, and a last group of two or three characters; one character is an error. `aGVsbG8` and `aGVsbG8=` decode as `hello`; `aGVsbG8==`, `aGVsbG8=aGVsbG8=`, `a` and `aGVs bG8=` fail; the empty string gives empty bytes. The decoder is the code's own: `Convert.FromBase64String` skips spaces and tabs and `Base64.DecodeFromUtf8` has other rules, so neither is used. The length of the result is known after a pass that validates and counts, and it is charged before the array is created.

**JSON.** The rules of `json.encode` are those of cel-go: the value is converted to a JSON value as in protobuf and written by the compact encoder of Go with its escaping of HTML characters.

| Value | Text |
| :- | :- |
| `null`, `bool` | `null`, `true`, `false` |
| `int`, `uint` | decimal digits when the value is from −(2⁵³ − 1) to 2⁵³ − 1, the digits in double quotes outside it: `1`, `"9007199254740993"`, `"18446744073709551615"` |
| `double` | finite: the shortest digits that read back to the same value, as Go's `encoding/json` writes a `float64` — plain digits from 1e-6 up to but not including 1e21, otherwise `d.ddde±XX` with an exponent of two digits at least where a negative one of the form `e-07` becomes `e-7`: `1`, `1.5`, `0.000001`, `100000000000000000000`, `1e+21`, `1e-7`, `5e-324`, `-0`. NaN and the infinities — an error, `a double that is NaN or infinite cannot be encoded` |
| `string` | in double quotes; `"` and `\` escaped; `\n`, `\r`, `\t`, `\b`, `\f` as these two characters; every other code point below `U+0020` as `\u00xx`, so `\v` is `\u000b` and `U+0000` is `\u0000`; `<`, `>` and `&` as `\u003c`, `\u003e`, `\u0026`; `U+2028` and `U+2029` as `\u2028`, `\u2029`; a lone surrogate as `\ufffd`; `U+007F` and everything else as it is |
| `bytes` | the standard base64 text with padding, in double quotes |
| `duration`, `timestamp` | the text of `string()` in double quotes: `"1.5s"`, `"3600s"`, `"2020-01-01T00:00:00.123Z"` |
| `list` | `[` the elements separated by `,` `]`, with no spaces |
| `map` | `{` the entries as `"key":value` separated by `,` `}`, ordered by the key by code points. A key that is not a string is an error, `unsupported type conversion from 'int' to string` |
| `type`, abstract values, objects of the application | an error, `unsupported type conversion`: the kinds of a part that has its own JSON — messages, `optional`, `decimal` — are added by that part |

The writer appends to a `ResultBuilder` and charges as it grows ([extension-strings](extension-strings.md#conventions)); every node of the value gives at least one character, so the walk is inside the charge. It descends by recursion with `RuntimeHelpers.TryEnsureSufficientExecutionStack()` before each level, `false` being `InsufficientStack` (9000) at the call. A map or list over data of the application is read by its enumeration, and an error element is the error of the call. The pairs of a map are written, and charged, in the order of its enumeration and then written again in the order of the keys into room that is already charged, with no second charge; the text of a map is in memory twice while it is arranged.

Related pages: [extensions](extensions.md), [extension-macros](extension-macros.md), [library](library.md), [values](values.md), [evaluation](evaluation.md).
