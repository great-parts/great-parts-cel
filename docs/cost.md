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
| L | the length of a string key of a map, or of the text of a pattern of `matches` |
| m | the size of the program of a pattern of `matches`, see [`matches`](#matches) |
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
| values of different kinds; `null`, `bool`, `int`, `uint`, `double`, `timestamp`, `duration` and values of the type `type` | 1 |
| two strings | 1 + ⌈(La + Lb)/10⌉, with the lengths of the two strings |
| two `bytes` | 1 + ⌈(Ba + Bb)/10⌉, with the lengths of the two in bytes |
| two lists | 1 when the sizes differ; otherwise 1 + Σ eq(aᵢ, bᵢ) by index, in order, up to the first unequal pair |
| two maps | 1 when the sizes differ; otherwise 1 + Σ over the keys k of a in its order: the lookup of k in b (the classes Read and Read with a string key) + eq(a[k], b[k]), up to the first unequal pair. For a JSON object the lookup is the row "key lookup in a JSON object" of [Data of the application](#data-of-the-application), and its size and enumeration are the row "`size`, enumeration of a JSON object" |
| two objects of the application | 1 + Σ over the fields compared, in the order of the description: the reads of the field on both sides (1 each) + eq of the two values, up to the first unequal pair. Two objects of different descriptions are 1 |

Ordering (`<`, `<=`, `>`, `>=`) of numbers, `bool`, `timestamp` and `duration` is 1, of strings 1 + ⌈(La + Lb)/10⌉, of `bytes` 1 + ⌈(Ba + Bb)/10⌉.

## Standard library

The identifiers are those of the page [Standard library](standard-library.md).

| Overloads | Class | Units |
| :- | :- | :- |
| `conditional`, `logical_and`, `logical_or` | Logic | 0 beyond the operands |
| `not_strictly_false` | part of the macro step | 0 |
| `logical_not` | Constant | 1 |
| `equals`, `not_equals` | Equality | eq(a, b) |
| `add_int64`, `add_uint64`, `add_double`, `add_duration_duration`, `add_duration_timestamp`, `add_timestamp_duration`, `subtract_…`, `multiply_…`, `divide_…`, `modulo_int64`, `modulo_uint64`, `negate_int64`, `negate_double` | Constant | 1 |
| `add_string`, `add_bytes` | Text | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉, with Lᵣ = Lₐ |
| `add_list` | List | 1 + n, with n the size of the result |
| ordering of `bool`, numbers and time, among them the 24 overloads across numeric types | Constant | 1 |
| ordering of `string` and `bytes` | Order | 1 + ⌈Lₐ/10⌉ |
| `index_list` | Read | 1 |
| `index_map`, `in_map` | Read, Read with a string key | 1; for a string key of length L, 1 + ⌈L/10⌉ |
| `in_list` | Equality | 1 + Σ eq(x, eᵢ) over the elements in order, up to the first equal one |
| `size_bytes`, `bytes_size`, `size_list`, `list_size`, `size_map`, `map_size` | Constant | 1 |
| `size_string`, `string_size` | Text | 1 + ⌈Lₐ/10⌉: the size counts code points, which takes a pass over the string |
| `contains_string`, `ends_with_string`, `starts_with_string` | Text | 1 + ⌈Lₐ/10⌉ |
| `matches`, `matches_string` | `matches` | the steps of the section [`matches`](#matches) |
| `type`, `to_dyn` | Constant | 1 |
| `bool_to_bool`, `bytes_to_bytes`, `double_to_double`, `int64_to_int64`, `string_to_string`, `uint64_to_uint64`, `timestamp_to_timestamp`, `duration_to_duration` | Constant | 1: the identity, the value is not copied |
| `int64_to_double`, `uint64_to_double`, `double_to_int64`, `uint64_to_int64`, `double_to_uint64`, `int64_to_uint64` | Constant | 1 |
| `string_to_bool`, `string_to_double`, `string_to_int64`, `string_to_uint64`, `string_to_timestamp`, `string_to_duration` | Text | 1 + ⌈Lₐ/10⌉ |
| `string_to_bytes`, `bytes_to_string` | Text | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉ |
| `bool_to_string`, `int64_to_string`, `uint64_to_string`, `double_to_string`, `timestamp_to_string`, `duration_to_string` | Text | 1 + ⌈Lᵣ/10⌉; the length of the result is bounded and known before the string is created (for time at most 30) |
| `int64_to_timestamp`, `timestamp_to_int64`, `duration_to_int64` | Constant | 1 |
| the 14 receivers of time without a zone: `timestamp_to_year`, `timestamp_to_month`, `timestamp_to_day_of_year`, `timestamp_to_day_of_month`, `timestamp_to_day_of_month_1_based`, `timestamp_to_day_of_week`, `timestamp_to_hours`, `timestamp_to_minutes`, `timestamp_to_seconds`, `timestamp_to_milliseconds`, `duration_to_hours`, `duration_to_minutes`, `duration_to_seconds`, `duration_to_milliseconds` | Constant | 1 |
| the 10 receivers of a timestamp with a zone: `timestamp_to_year_with_tz`, `timestamp_to_month_with_tz`, `timestamp_to_day_of_year_with_tz`, `timestamp_to_day_of_month_with_tz`, `timestamp_to_day_of_month_1_based_with_tz`, `timestamp_to_day_of_week_with_tz`, `timestamp_to_hours_with_tz`, `timestamp_to_minutes_with_tz`, `timestamp_to_seconds_tz`, `timestamp_to_milliseconds_with_tz` | Text | 1 + ⌈Lₐ/10⌉, Lₐ the length of the zone string |

The search for a substring (`contains`) takes time linear in the lengths of the string and the substring in the worst case, so 1 + ⌈Lₐ/10⌉ holds for every input.

### Time

The overloads of time are on the page [Standard library](standard-library.md#time). Their cost is in the table above; what is particular to time:

- A receiver of a timestamp with a zone charges 1 + ⌈Lₐ/10⌉, with Lₐ the length of the zone string, whether the zone is a name of the database, `UTC`, an offset or a string that fails: the charge is made before the call, from the text of the argument. A zone that is a constant is charged in the same way.
- `string(timestamp)` and `string(duration)` produce at most 30 characters, so their charge is at most 1 + 3 = 4; it is made from the value, before the string is created.
- A call that the compiler folds, such as `duration('72h')` or `timestamp('2009-02-13T23:31:30Z')`, charges what the call charges at evaluation: folding changes the work of the program, not its cost.
- A receiver without a zone, the arithmetic, the comparison and the conversions between a time value and an `int` take constant time and no memory that grows with the data.

## `matches`

A call of `matches` has up to three steps. n is the length of the string being searched and L the length of the text of the pattern, both in UTF-16 code units; m is the size of the program of the pattern, the number of its instructions plus the ranges of its classes that are not shared tables, see the page [Limits](limits.md#pattern-of-matches).

| Step | When | Units |
| :- | :- | :- |
| the parse of a pattern of length L | at evaluation, when the pattern is not a constant | 1 + ⌈L/10⌉ |
| the build of the program of size m | at evaluation, right after the parse, when the parse succeeds and m is within the limit | ⌈m/4⌉ |
| the search in a string of length n | at every evaluation | 1 + ⌈(n + 1) · m/10⌉ |

- Each step is charged before the work it pays for, so m is known when the build is charged, and a limit that stops the evaluation stops it before the parse, the build or the search.
- A pattern that is a string literal is read when the program is compiled, so an evaluation charges only the search.
- A pattern that is not a literal pays the parse and the build at every evaluation, a hit in the cache of patterns included: the cache changes nothing that is charged. A pattern that is refused pays the parse alone, from the cache as well as the first time.
- The operands are charged by their own rows: a literal 0 and a variable 1.
- The divisors 10 and 4 are calibrated: one unit of the search is about ten steps of the engine, and ⌈m/4⌉ is about the 16 bytes of program for each instruction, see [Calibration](#calibration).

With the pattern `abc` (m = 4) and the string `xabcx` (n = 5): as a literal the call costs the search, 1 + ⌈6 · 4/10⌉ = 4, and 5 with the read of the string; from a variable it costs the parse 1 + 1 = 2, the build 1 and the search 4, which is 7, and 9 with the reads of the string and of the pattern.

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

## Extension sets

A call of a function of an extension set is charged like a call of the standard library: a fixed part and, where the cost grows with the data, a part computed from the arguments, both charged before the function runs; what the function builds while it runs is charged as the result grows, before the memory it needs is taken. An argument that is invalid without any work on the data gives its error after the constant unit alone. The rows of each set are given with the set, see [Extension sets](extensions.md).

- **Macros of sets** are charged by their expansion, as the standard macros above are: the expressions that the author wrote by their own rows, 1 for each executed step, and what the expansion adds by the rows of its set.
- **A set of the application** costs as the functions of the application do: 1 unit for a call and the cost function of the overload, see Functions of the application below.

### Set `strings`

A call passes over its strings: the part of the arguments is charged before the function runs, the part of the result before the result is created or, for `replace` and `split`, as the result is built. The lengths are in UTF-16 code units, although the indexes of these functions are in code points: finding the code point of an index takes a pass over the string, which the term ⌈Lₐ/10⌉ pays for. A result that equals the receiver, such as that of `replace` without a match, is charged as the copy it could have been. In the table, k is the number of parts that `split` makes.

| Overloads | Units |
| :- | :- |
| `string_char_at_int`, `string_substring_int`, `string_substring_int_int`, `string_trim`, `string_lower_ascii`, `string_upper_ascii`, `string_reverse`, `strings_quote` | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉; an index error comes after 1 + ⌈Lₐ/10⌉, since it has no result |
| `string_index_of_string`, `string_index_of_string_int`, `string_last_index_of_string`, `string_last_index_of_string_int` | 1 + ⌈Lₐ/10⌉ |
| `string_replace_string_string`, `string_replace_string_string_int` | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉, with Lₐ the lengths of the three strings; the result is charged as it grows |
| `string_split_string`, `string_split_string_int` | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉ + k, with Lᵣ the sum of the lengths of the parts; each part is charged, 1 and its share of Lᵣ, before it is created |
| `list_join`, `list_join_string` | 1 + n + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉, with Lₐ the lengths of the elements and of the separator |
| `string_format` | 1 + n + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉, with n the size of the list and Lₐ the length of the format string alone; the result, with the text of the nested lists and maps in it, is charged as it is built, so the units of a map are those of the length of its text; an error ends the call after the units charged so far |

For example, `'a,b,c'.split(',')` costs 1 + 1 + 1 + 3 = 6, `['a', 'b'].join('-')` costs 1 + 2 + 1 + 1 = 5 beyond the list literal, and `'%s-%d'.format(['a', 1])` costs 1 + 2 + 1 + 1 = 5 beyond the list literal.

### Set `sets`

Each element that is looked up in the other list is an `in` over it: 1 unit and eq of each pair compared, up to the first equal element, each charged before the comparison. The call itself is 1 unit.

| Overloads | Units |
| :- | :- |
| `list_sets_contains_list` | 1 + the sum, over the elements of the second list in order, of 1 + eq of the pairs compared with the first list; the first element that is missing ends the call |
| `list_sets_intersects_list` | 1 + the sum, over the elements of the first list in order, of 1 + eq of the pairs compared with the second list; the first element that is found ends the call |
| `list_sets_equivalent_list` | 1 + the sums of `contains(a, b)` and, when that is `true`, of `contains(b, a)`, each without its own 1 |

For example, `sets.contains([1, 2, 3], [1, 2])` costs 1 + (1 + 1) + (1 + 2) = 6 beyond the two list literals.

### Set `lists`

A call is 1 unit and the part of its arguments that the call node charges before the function runs; what the function builds is charged before it is built. In the table n is the size of the list the call works on, and k the size of the result of `slice`, known from the indexes. The units of the size of a result are charged with the call, so a limit stops the call before the memory is taken.

| Overloads | Units |
| :- | :- |
| `list_slice` | 1 + k; an index that is invalid gives its error after the 1 of the call, since it has no result |
| `lists_range` | 1 + n for the argument n from 0 to 1,000,000; an argument below 0 or above the limit gives its error after the 1 of the call, before the formula is charged |
| `list_reverse` | 1 + n |
| `list_flatten`, `list_flatten_int` | 1 + v, with v the sum of the sizes of the lists that `flatten` descends into: the receiver, and the nested lists not deeper than the depth, which is 1 for `flatten()`. The size of the receiver is charged with the call, the size of each nested list before its elements are viewed |
| `list_int_sort`, `list_uint_sort`, `list_double_sort`, `list_bool_sort`, `list_google.protobuf.Duration_sort`, `list_google.protobuf.Timestamp_sort` | 1 + n + n · ⌈log₂(n + 1)⌉, with ⌈log₂(n + 1)⌉ the bit length of n |
| `list_string_sort`, `list_bytes_sort` | 1 + n + c · n · ⌈log₂(n + 1)⌉, with c = 1 + ⌈2 · Lmax/10⌉ for the longest element, found in one pass before the call is charged |
| `list_<U>_sortByAssociatedKeys` | the same, with n and Lmax of the list of keys and c by the type `U` |
| `list_distinct` | 1 + n + the units of the equality of each pair compared, each charged before the comparison; each element is compared with the ones kept, in order, up to the first equal one |

The macro `sortBy` is charged by its expansion: the receiver; the empty list that is the range of the binding, 1, charged although no list is made; the `map` of the keys as the standard macro charges it — 1 for the empty list, 1 for each step and the expression of the key, 1 for each key added; and the call of `@sortByAssociatedKeys` by its row. The reads of the hidden names cost nothing.

For example, `[3, 1, 2].sort()` costs the list literal 4 and the call 1 + 3 + 3 · 2 = 10, 14 in all, and `[3, 1, 2].sortBy(e, e)` costs 4 + 1 + 10 + 10 = 25.

### Set `encoders`

A call is 1 unit. The two functions of base64 add ⌈Lₐ/10⌉ for the length of their argument, charged before the function runs, and ⌈Lᵣ/10⌉ for the length of the result, charged before the result is created. `json.encode` is charged as its text grows: every node of the value gives at least one character, so the walk of the value is inside the charge, and the text of a map, which is first written in the order of its entries, is charged once, by its length.

| Overloads | Units |
| :- | :- |
| `base64_encode_bytes` | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉, with Lₐ the number of bytes and Lᵣ = 4 · ⌈Lₐ/3⌉ |
| `base64_decode_string` | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉, with Lₐ the length of the text and Lᵣ the number of bytes, known after a pass that validates the text and counts them, which an invalid text ends without a charge for the result |
| `json_encode_dyn` | 1 + ⌈Lᵣ/10⌉, with Lᵣ the length of the text; a value that has no JSON ends the call after the units charged for the text written so far |

For example, `base64.encode(b'abc')` costs 1 + 1 + 1 = 3, and `json.encode([])` costs 1 + 1 = 2 beyond the list literal.

### Set `math`

A call is 1 unit; the list forms of `math.@max` and `math.@min` add 1 for each element of the list, charged before the function reads the first element. The macros `math.greatest` and `math.least` are charged by their expansion: the arguments by their own rows, then the call of `math.@max` or `math.@min`, and for three arguments or more the list literal that holds them, which is a list of n elements, 1 + n.

| Overloads | Units |
| :- | :- |
| `math_@max_double`, `math_@max_int`, `math_@max_uint`, the pairs and the mixed pairs of `math.@max`, and the same for `math.@min` | 1 |
| `math_@max_list_double`, `math_@max_list_int`, `math_@max_list_uint`, `math_@min_list_double`, `math_@min_list_int`, `math_@min_list_uint` | 1 + n for a list of n elements |
| `math_ceil_double`, `math_floor_double`, `math_round_double`, `math_trunc_double`, `math_isInf_double`, `math_isNaN_double`, `math_isFinite_double`, `math_abs_double`, `math_abs_int`, `math_abs_uint`, `math_sign_double`, `math_sign_int`, `math_sign_uint`, `math_sqrt_double`, `math_sqrt_int`, `math_sqrt_uint` | 1 |
| `math_bitAnd_int_int`, `math_bitAnd_uint_uint`, `math_bitOr_int_int`, `math_bitOr_uint_uint`, `math_bitXor_int_int`, `math_bitXor_uint_uint`, `math_bitNot_int_int`, `math_bitNot_uint_uint`, `math_bitShiftLeft_int_int`, `math_bitShiftLeft_uint_int`, `math_bitShiftRight_int_int`, `math_bitShiftRight_uint_int` | 1 |

An error of the function — a NaN, the empty list, the least `int` of `math.abs`, a negative offset — comes after the units of the call, which are charged before the function runs.

For example, `math.greatest(i, j, 3)` with the variables `i` and `j` costs the two reads 2, the list literal 1 + 3 and the call of `math_@max_list_int` 1 + 3, 10 in all; `math.greatest(1, 2)` costs 1, the call of `math_@max_int_int`.

### Set `bindings`

`cel.bind` is a comprehension over an empty list, charged by its expansion: 1 for the empty list that is the range, charged although the evaluation creates no list, no step, the expressions of the author by their own rows, and 1 for each read of the bound name in the result. `cel.@block`, the function that no text can write, costs nothing itself: a slot costs what its expression costs, once, at its first read; each read of a slot is 1, as the read of a variable is; a slot that nothing reads costs nothing.

| Overload or macro | Units |
| :- | :- |
| `cel.bind(name, value, result)` | 1 + the cost of `value` + the cost of `result`, each read of `name` in `result` counted as 1 |
| `cel_block_list` | 0 beyond the slots that are read and the result |

For example, `cel.bind(x, 1, x + 1)` costs 1 + 0 + (1 + 0 + 1) = 3.

### Set `two-var-comprehensions`

The macros of two variables are charged by their expansion, as the standard macros are: the range and the expressions that the author wrote — the predicate, the filter and the transformation — by their own rows, 1 for each step that is executed, the initial value of the accumulator, and the elements of a result that is built as they are added. The reads of the accumulator, and of the first variable that `transformMap` hands to `cel.@mapInsert`, are part of the step and cost 0; the reads of a name that the author wrote cost 1, as any read does.

| Macro | Units |
| :- | :- |
| `all`, `exists` | 1 for each executed step; the steps after a short circuit are not executed |
| `existsOne`, `exists_one` | 1 for each step, without a short circuit |
| `transformList` | 1 for the empty list, 1 for each executed step and 1 for each element added |
| `transformMap`, `transformMapEntry` | 1 for the empty map and 1 for each executed step, then the pairs by the rows of `cel.@mapInsert` below |

| Overload | Units |
| :- | :- |
| `@mapInsert_map_key_value` | 2 for the pair put in and ⌈L/10⌉ more for a string key of length L, charged before the pair is put in; no constant unit |
| `@mapInsert_map_map` | 2m for m pairs put in and ⌈L/10⌉ more for each string key of length L, pair by pair, after the enumeration of the second map that its row charges for a map view ordered by key or for a JSON object |

A step that adds nothing, because its filter is false or the map of `transformMapEntry` has no pairs, costs the step and the expressions it evaluated. For example, `[7, 8, 9].transformMap(i, v, v)` costs the list literal 1 + 3, the empty map 1, and for each of the three steps 1, the read of `v` 1 and the pair 2: 4 + 1 + 12 = 17. `[1, 2].all(i, v, v > i)` costs the list literal 3 and for each of the two steps 1 and the comparison with its two reads, 1 + 3: 3 + 8 = 11.

### Set `network`

A call is 1 unit. The seven overloads that parse a string add ⌈Lₐ/10⌉ for its length, and the two conversions to text add ⌈Lᵣ/10⌉ for the length of the text, which is known from the family and the digits of the address before the text is made. Both are charged before the function runs, so a parse that fails has been paid for. A value of `net.IP` or `net.CIDR` is one object of at most 48 bytes, so no row needs more than the unit it has; the equality of two such values is 1.

| Overloads | Units |
| :- | :- |
| `string_to_ip`, `string_to_cidr`, `is_ip`, `is_cidr`, `ip_is_canonical`, `cidr_contains_ip_string`, `cidr_contains_cidr_string` | 1 + ⌈Lₐ/10⌉, with Lₐ the length of the string |
| `ip_to_string`, `cidr_to_string` | 1 + ⌈Lᵣ/10⌉, with Lᵣ the length of the text that is made |
| `cidr_ip`, `ip_family`, `ip_is_loopback`, `ip_is_unspecified`, `ip_is_link_local_unicast`, `ip_is_link_local_multicast`, `ip_is_global_unicast`, `cidr_prefix_length`, `cidr_is_mask`, `cidr_masked`, `cidr_contains_ip_ip`, `cidr_contains_cidr` | 1 |

For example, `ip('192.168.0.1')` costs 1 + 2 = 3, and `string(ip('1.2.3.4'))` costs 1 + 1 = 2 beyond the call of `ip`.

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

`matches` at n = 0, a string of no characters, for patterns of the sizes m = 1, 10, 100, 1,000, 10,000 and 100,000 (`net10.0`, JIT, Release; the bytes of a call of the program after the bytes of a call of a program that reads the same variables and compares them, so that the frame and the result are not counted, and the units of the whole expression, among them the reads of the operands):

| Case | Bytes per unit |
| :- | :- |
| a pattern that is a literal | 0 |
| a pattern from data that is found in the cache of patterns | 0 |
| a pattern from data that is not in the cache: the parse, the build and the program that the cache keeps, 16 bytes for each instruction | 45.6 at most, 44.4 to 46.0 for the sizes from 10 |
| the first evaluation of a size in a process, which takes the arrays of the engine from the pool of arrays for the first time (once, not a rate) | 189 at most for a pattern from data, 318 for a literal |

The arrays of the engine and of the parser come from `ArrayPool<int>.Shared` and go back to it, so in the steady state a search allocates nothing and the rate is 0; what a miss allocates is the program that stays in the cache, which the units of the build pay for at 64 bytes each (⌈m/4⌉ units for 16 · m bytes). The divisor of the search, 10, is the time: about 10 ns for each unit at the size limit, as the page [Limits](limits.md#pattern-of-matches) shows. The measurement also checked each formula of the table of the `matches` section: with the cost limit equal to the units of the whole expression the call runs, and with one unit less it stops.
