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

## The limit of an environment and the reported cost

`CelEnvironmentBuilder.SetDefaultCostLimit(ulong)` sets the limit of every evaluation of the environment that passes no limit of its own, and `CelEnvironment.DefaultCostLimit` tells it; calling the method again replaces the value, and `ToBuilder()` carries it to the new builder. The limit of a call, `CelEvalOptions.CostLimit`, replaces the limit of the environment, a larger one included. With neither of them the cost is not counted.

`CelEvalResult<T>.Cost` is the sum of the units that the evaluation charged, when a limit applied to it, and `null` otherwise:

- for an evaluation that succeeds, the cost of the whole evaluation, so a limit of exactly that value lets it run;
- for the error `CostLimitExceeded`, the sum before the operation that was refused, so `Cost` is not above the limit;
- for another evaluation error, the sum up to and including the operation that gave the error;
- for a one-step call, the cost of the evaluation only; a call that failed to compile has no cost, `null`.

The cost depends on the expression and the data, not on the limit: the same evaluation gives the same `Cost` under any limit that it does not reach. `Cost` of `default(CelEvalResult<T>)` throws `InvalidOperationException`. The counter does not overflow: a total that does not fit `ulong` is over every limit.

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
| Object literal | creating a value of a .NET type that the generator describes, or of a type described at run time, with a literal of n entries (`AllowObjectLiterals`) | 1 + n, and for a .NET type 1 more for each element of a list and each pair of a map among the values, which the generated code copies into members; the constructor, `set` and `init` of the application are not charged, as a getter is not |
| Message | creating a protobuf message with a literal of n entries, a field named twice counted each time | 1 + n, and ⌈L/10⌉ for the sum L of the lengths of the values written directly in the fields that are strings, in UTF-16 code units, and bytes; for `google.protobuf.Any{…}` the lengths of the `type_url` and of the `value` count |
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
| values of different kinds; `null`, `bool`, `int`, `uint`, `double`, `decimal`, `timestamp`, `duration` and values of the type `type` | 1 |
| two strings | 1 + ⌈(La + Lb)/10⌉, with the lengths of the two strings |
| two `bytes` | 1 + ⌈(Ba + Bb)/10⌉, with the lengths of the two in bytes |
| two lists | 1 when the sizes differ; otherwise 1 + Σ eq(aᵢ, bᵢ) by index, in order, up to the first unequal pair |
| two maps | 1 when the sizes differ; otherwise 1 + Σ over the keys k of a in its order: the lookup of k in b (the classes Read and Read with a string key) + eq(a[k], b[k]), up to the first unequal pair. For a JSON object the lookup is the row "key lookup in a JSON object" of [Data of the application](#data-of-the-application), and its size and enumeration are the row "`size`, enumeration of a JSON object" |
| two objects of the application | 1 + Σ over the fields compared, in the order of the description: the reads of the field on both sides (1 each) + eq of the two values, up to the first unequal pair. Two objects of different descriptions are 1 |
| two protobuf messages ([Protobuf messages](protobuf.md)) | 1 + ⌈(Sₗ + Sᵣ)/10⌉, S the size of a message in bytes of the protobuf wire format; charged once before the comparison and not by pairs; the size bounds the work of the comparison that grows with the data, and the check of each field of the description, a constant of the description, is not charged |

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

#### Time zones of the operating system

In an environment whose time zone database is the one of `CelTimeZoneDatabase.FromSystem()`, a receiver of a timestamp with a zone charges 1 + ⌈Lₐ/10⌉ + Z for a name that is not `UTC` and not a fixed offset, where Z = 2,000 units is one constant for every such name. It is the same whether the zone is found, kept from an earlier call, not found, or refused because the name has not the form of an IANA identifier ([Limits](limits.md#time-zones-of-the-operating-system)), so the cost does not depend on what the database has kept or on the calls before. The charge is made before the zone is looked up, and a zone that is a constant is looked up at each evaluation, not at compilation: compiling reads nothing from the operating system, so it is charged in the same way. Z covers a request to the platform that finds nothing. A database from `CelTimeZoneDatabase.Create` or no database charges only 1 + ⌈Lₐ/10⌉.

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

The elements are looked up by hash, as the page "Hashing in `distinct` and `sets`" below says; the values that are not hashed (an object of the application, an error, a number of 2^53 or more) are compared with the values of their group in order, each pair charged. The call itself is 1 unit. In the rows, h is the units of hashing a value and "the comparison" is the one comparison of the element looked up with the equal element it found. The list that is looked in is indexed whole, so an element of it that is an error ends the call wherever it stands.

| Overloads | Units |
| :- | :- |
| `list_sets_contains_list` | 1; when the second list is not empty, also the sum of h over the whole first list, and over the elements of the second list in order, h + 1 + the comparison; the first element that is missing ends the call |
| `list_sets_intersects_list` | 1; when the first list is not empty, also the sum of h over the whole second list, and over the elements of the first list in order, h + 1 + the comparison; the first element that is found ends the call |
| `list_sets_equivalent_list` | 1 + the sums of `contains(a, b)` and, when that is `true`, of `contains(b, a)`, each without its own 1 |

For example, `sets.contains([1, 2, 3], [1, 2])` costs 1 + 3 + (1 + 1 + 1) + (1 + 1 + 1) = 10 beyond the two list literals, and `sets.contains([1], [])` costs 1.

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
| `list_distinct` | 1 + n + the sum of h over all the elements + the comparison of each element that repeats a kept one; the elements that hold an object of the application, an error, or a number of 2^53 or more are compared as the page "Hashing in `distinct` and `sets`" says |

The macro `sortBy` is charged by its expansion: the receiver; the empty list that is the range of the binding, 1, charged although no list is made; the `map` of the keys as the standard macro charges it — 1 for the empty list, 1 for each step and the expression of the key, 1 for each key added; and the call of `@sortByAssociatedKeys` by its row. The reads of the hidden names cost nothing.

For example, `[3, 1, 2].sort()` costs the list literal 4 and the call 1 + 3 + 3 · 2 = 10, 14 in all, and `[3, 1, 2].sortBy(e, e)` costs 4 + 1 + 10 + 10 = 25.

### Hashing in `distinct` and `sets`

`distinct` and the functions of the set `sets` look an element up by a hash that agrees with CEL equality, so a call does not compare every pair. The cost is the hashing of the values and, for each element that is equal to another, the one comparison that found it; the comparisons of values that only share a hash are not charged, since how many there are depends on the random seed of the hash and the cost depends only on the data. The cost does not depend on the threshold of the pairwise pass, the length of the list at which the library changes from scanning the hashes to buckets, and the result is the one that comparing the pairs in order gives: the elements for which no equal one was kept before stay, in order.

Each element is in one of three groups, taken in this order, the first that fits:

- **Pairwise**: a value with, at any depth, an object of the application, a message of protobuf, a value that is an error (an element of a list of the application that cannot be read, an object of JSON with a repeated name), or a number of 2^53 or more in absolute value: an `int` or a `uint`, or a `double`, an infinity included. The equality of these values calls the code of the application, gives an error, or is not transitive, so they are compared with each other in the order of the pairs, as in version 1.0, each pair charged as the description of the type says (see Data of the application), and the code of the application is called for the same pairs in the same order. An exception that the code throws ends the call with the error `ApplicationException` as before; an error of the equality ends the call when the comparison in order reaches it. Such a value is never equal to a value of another group.
- **Single**: a value with a NaN inside, and none of the above. It is equal to nothing, not even to itself, so `distinct` keeps every one of them and no function of `sets` finds one; it costs its hashing only.
- **With a hash**: every other value. Numbers of the three kinds with the same value below 2^53 in absolute value (`1`, `1u`, `1.0`) are equal, so are both zeros, strings and bytes of the same content, lists of equal elements in order, and maps of equal entries in any order.

The equality of an `int` or a `uint` with a `double` compares the integer rounded to the nearest `double`, and an `int` with a `uint` exactly, so from 2^53 on equality is not transitive: `9007199254740993 == 9007199254740992.0` and `9007199254740992 == 9007199254740992.0`, but `9007199254740993 != 9007199254740992`. A number below 2^53 is the same as its `double` and equals no number of 2^53 or more, so those numbers are a group of their own, compared in order, and the result of `distinct` is what the passes over the pairs give: an element stays when no element kept before it is equal to it. A call on a long list of numbers of 2^53 or more (identifiers of 64 bits, for example) is therefore as slow as in version 1.0 and is charged for every pair.

The units h of hashing a value are charged before each of its nodes, in the order they are walked: 1 for `null`, a `bool`, a number, a time, a duration, a type, an `optional` without a value, an object of the application and a message of protobuf (they are not walked into); 1 + ⌈L/10⌉ for a string of L UTF-16 code units; 1 + ⌈B/10⌉ for `bytes` of B bytes; 1 + the sum of h of the elements for a list; 1 + the sum of h of the keys and values for a map; 1 + h of the value for an `optional` with a value. A value is hashed once, whatever its group. The reading of the data of the application and of JSON while a value is walked (the enumeration and the `size` of a JSON object, the index of a JSON array, the enumeration of a map with its order of keys, the check for repeated keys) is charged beyond h by the rows of [Data of the application](#data-of-the-application), as it is in equality; a value that the walk and a comparison both read pays the reading twice.

The functions of `sets` index the whole list that is looked in before they look for the first element: an element of that list that is itself an error ends the call with that error, wherever it stands, when the index is built (when the other list is not empty), where a search in order of version 1.0 could have stopped before it. An error inside an element makes it a value of the group Pairwise, and is reached by the comparison in order within the group; a comparison of such a value with a value of another group, which in version 1.0 could give that error, is not made in 2.0.

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

### Set `protos`

The set has two macros and no function, so it has no row of its own. A macro costs what its expansion costs: `proto.getExt(m, a.b.c)` is the selection of the field `a.b.c` of `m`, and `proto.hasExt(m, a.b.c)` its presence test, so each costs 1 beyond the operand `m`, the unit of any field selection. The identifier `proto` and the name of the extension are not read and cost nothing. For example, `proto.getExt(m, pkg.count_ext)` with a variable `m` costs the read of `m`, 1, and the selection, 1: 2 in all.

### Set `optional`

A call is 1 unit, and the nodes of the set charge what the table says before the function runs or, for `or` and `orValue`, between the receiver and the argument. In the table n is the size of the list that `optional.unwrap` and `unwrapOpt` work on.

| Overloads | Units |
| :- | :- |
| `optional_of`, `optional_none`, `optional_value`, `optional_hasValue`, `list_first`, `list_last` | 1 |
| `optional_ofNonZeroValue` | 1; for an object of the application or a message, 1 + f, with f the number of the fields of its description, which is known without reading a field and is charged before the first field is read |
| `optional_or_optional`, `optional_orValue_value` | 1, charged after the receiver is evaluated and before the argument; the argument is charged only when it is evaluated, which is when the receiver is empty |
| `select_optional_field` | what the selection it stands for costs: 1 for a field of a message or of an object of the application and for a map with a key that is not a string, 1 + ⌈L/10⌉ for a map with a string key of length L, whether the key is present or not; for a JSON object, the row of the key lookup in [Data of the application](#data-of-the-application) |
| `list_optindex_optional_int`, `optional_list_optindex_optional_int`, `optional_list_index_int` | 1, as the index of a list |
| `map_optindex_optional_value`, `optional_map_optindex_optional_value`, `optional_map_index_value` | 1 for a key that is not a string, 1 + ⌈L/10⌉ for a string key of length L, whether the key is present or not |
| `optional_unwrap`, `optional_unwrapOpt` | 1 + n |
| a literal with `?` (`[?e]`, `{?k: v}`, `T{?f: v}`) | the cost of the literal without `?`, counted over the positions that are stored: a list 1 + the number of stored elements; a map 1 + 2 for each stored entry and ⌈L/10⌉ for each stored string key of length L; a message 1 + the number of stored fields and ⌈L/10⌉ for the lengths of the strings and `bytes` stored, as in [Nodes of an expression](#nodes-of-an-expression). The expressions of the positions are charged by their own rows, whether the position is stored or dropped |

The equality of two optionals costs 1 when either is empty, and 1 plus the equality of the held values when both hold one.

The macros `optMap` and `optFlatMap` are charged by their expansion, which is a conditional on `hasValue()` over a comprehension of an empty list. The expressions that the author wrote — the target and the body — are charged by their own rows. Everything that the expansion adds costs 0 to read: the copy of the target in `x.value()` and the hidden name that binds a target that is not an identifier. Each call that it adds, `hasValue`, `value`, `optional.of` and `optional.none`, is 1, and the empty list that is the range of the comprehension is 1 although no list is made. No step runs. With c(t) the cost of the target and c(e) the cost of the body:

| Macro | Target | Empty optional | Optional with a value |
| :- | :- | :- | :- |
| `optMap` | an identifier | c(t) + 2 | c(t) + 4 + c(e) |
| `optMap` | any other expression | c(t) + 3 | c(t) + 5 + c(e) |
| `optFlatMap` | an identifier | c(t) + 2 | c(t) + 3 + c(e) |
| `optFlatMap` | any other expression | c(t) + 3 | c(t) + 4 + c(e) |

For example, `x.optMap(y, y + 1)` with `x` bound to `optional.of(1)` costs the read of `x` 1, `hasValue` 1, `optional.of` 1, the empty list 1, `value` 1 and the body 2, which is 7; with `x` bound to `optional.none()` it costs the read of `x`, `hasValue` and `optional.none`, which is 3. `optional.of(42).optMap(y, y + 1)` has a target that costs its call, 1, so it costs 8.

### Set `decimal`

A call is 1 unit. Only two overloads grow with a text, and one has a unit of its own for the work of a `double`. A value of the type `decimal` takes no memory beyond the 16 bytes of a value, so no row needs more than the unit it has, and the equality of two decimals, or of a decimal and a value of another kind, is 1. Only on .NET 11.

| Overloads | Units |
| :- | :- |
| `add_decimal`, `subtract_decimal`, `multiply_decimal`, `divide_decimal`, `negate_decimal`, `less_decimal`, `less_equals_decimal`, `greater_decimal`, `greater_equals_decimal`, `int64_to_decimal`, `uint64_to_decimal`, `decimal_to_decimal`, `decimal_to_int64`, `decimal_to_uint64`, `decimal_to_double` | 1 |
| `double_to_decimal` | 4: the shortest text of a `double` is found, laid out and read |
| `string_to_decimal` | 1 + ⌈Lₐ/10⌉, with Lₐ the length of the string, charged before the string is read, so a string that does not parse has been paid for |
| `decimal_to_string` | 1 + ⌈Lᵣ/10⌉, with Lᵣ the length of the text, at most 42, charged before the string is created |

Version 2 adds `decimal_round_int64`, `decimal_round_int64_string`, `decimal_quantize_decimal` and `decimal_quantize_decimal_string`, 1 unit each: the value is kept without allocation, the mode is a string of at most nine characters, and a longer string is refused before it is compared, so the cost does not grow with it. `(1.5m).round(0)` costs 1.

A decimal literal costs 0, as every literal does. For example, `string(1.5m + decimal('0.25'))` costs the sum 1, the conversion of the string `'0.25'` 1 + 1, and the text 1 + 1: 5 in all. An error is charged as its call: `1m / 0m` costs 1.

### Set `calendar`

A call is 1 unit. A date and a time of day take no memory beyond the 32 bytes of a value, so the equality of two dates, of a date and a time of day, and of a date and a value of another kind, is 1, and the ordering, the receivers, the shifts, the differences and the identities are 1 each. The overloads that read or write a text add `⌈L/10⌉`, as the functions of time do.

| Overloads | Units |
| :- | :- |
| `int64_int64_int64_to_date`, `timestamp_to_date`, `timestamp_to_time_of_day`, `date_to_date`, `time_of_day_to_time_of_day`, `date_time_of_day_to_timestamp`, `date_to_year`, `date_to_month`, `date_to_day_of_month_1_based`, `date_to_day_of_month`, `date_to_day_of_week`, `date_to_day_of_year`, `time_of_day_to_hours`, `time_of_day_to_minutes`, `time_of_day_to_seconds`, `time_of_day_to_milliseconds`, `date_add_days`, `date_add_months`, `date_add_years`, `subtract_date_date`, `subtract_time_of_day_time_of_day`, `less_date`, `less_equals_date`, `greater_date`, `greater_equals_date`, `less_time_of_day`, `less_equals_time_of_day`, `greater_time_of_day`, `greater_equals_time_of_day` | 1 |
| `string_to_date`, `string_to_time_of_day` | 1 + ⌈Lₐ/10⌉, with Lₐ the length of the string, charged before the string is read |
| `timestamp_to_date_with_tz`, `timestamp_to_time_of_day_with_tz`, `date_time_of_day_to_timestamp_with_tz` | 1 + ⌈L/10⌉, with L the length of the name of the time zone, and the charge of the time zone database of the operating system when the environment uses it, as the receivers of a timestamp with a time zone |
| `date_to_string` | 1 + 1: the text has 10 characters, charged before the string is created |
| `time_of_day_to_string` | 1 + ⌈Lᵣ/10⌉, with Lᵣ the length of the text, 8 to 16, charged before the string is created |

For example, `date('2026-11-19')` costs 1 + ⌈10/10⌉ = 2; `date(t, 'Europe/Moscow')` costs the read of `t` 1, the call 1 and ⌈13/10⌉ = 2, which is 4; `date('2026-11-19').addDays(1)` costs 2 + 1 = 3, and `string(date('2026-11-19'))` 2 + 1 + 1 = 4. An error is charged as its call.

### Set `formats`

A call is 1 unit and adds ⌈Lₐ/10⌉ for the length of the receiver, charged before the function runs; the `bool` and the `int` that some overloads take add nothing. A function of the set reads the string once and builds nothing, so no row has a part for a result, and a string of another format, for which the answer is `false`, costs the same as one of the format.

| Overloads | Units |
| :- | :- |
| `string_is_email`, `string_is_hostname`, `string_is_host_and_port_bool`, `string_is_ip_prefix`, `string_is_ip_prefix_int`, `string_is_ip_prefix_bool`, `string_is_ip_prefix_int_bool`, `string_is_uri`, `string_is_uri_ref` | 1 + ⌈Lₐ/10⌉, with Lₐ the length of the receiver in UTF-16 code units |

For example, `'foo@example.com'.isEmail()` costs 1 + ⌈15/10⌉ = 3, and `'10.0.0.0/8'.isIpPrefix(4, true)` costs 1 + ⌈10/10⌉ = 2.

## Data of the application

The rows below are charged by the node or the implementation that does the work, before it, as the rows of the sections above. The rows for a JSON object replace the cost of the same operations on a map, they are not added to it: the key lookup (an index, a selection, `has()`) instead of the class Read with a string key and the index of a map, `in` instead of `in` on a map, and "`size`, enumeration" instead of the `size` of a map. The row for the index of a JSON array replaces the unit that the index of a list costs.

| Operation | Units |
| :- | :- |
| `e.f` and `has(e.f)` on an object of the application: the read of a field | 1; the read of a field that holds a structure, a set or a collection without an index adds the rows below, and `has(e.f)` is always 1 |
| the enumeration of a map view of an application dictionary that is ordered by ascending key ([Objects and collections](type-mapping.md#objects-and-collections)), n entries | 1 + c · n · ⌈log₂(n + 1)⌉ before the first entry, at each enumeration, also when the view keeps its sorted keys; c is 1 for integer, `bool` and enumeration keys, and 1 + ⌈2 · Lmax/10⌉ for `string` and `Guid` keys (a `Guid` key is a string of 36 characters), Lmax the length of the longest key. The conversion of a result to a .NET type enumerates without this charge, as it is not charged |
| the key lookup of a key of length L in a JSON object of n properties: an index, a selection, `has()` | 1 + n · (1 + ⌈L/10⌉). The lookup passes over all n properties and counts the matches, so that a repeated name is found |
| `size` and the enumeration of a JSON object of n properties | 1 + Σ (1 + ⌈Lᵢ/10⌉) over the properties, the 1 being the unit of the call for `size` and charged by the enumeration itself for a comprehension and for an equality, Lᵢ the length of the name of the property as it is written in the document, in UTF-8 bytes; the term of a property is charged before the property is processed. The operation checks the names for a repeat, and an escaped name is decoded for it |
| `in` on a JSON object, a key of length L | the row above for `size` + ⌈L/10⌉ |
| the equality of two JSON objects | the row of two maps in [Equality and order](#equality-and-order): the lookup in the second object is the row of the key lookup, after the check of both objects for repeated names |
| the index of a JSON array of n elements | 1 + n, instead of the unit of the index of a list: the array finds an element in constant time only when it has no nested values, otherwise it walks to the index |
| a string of JSON, at each read | 1 + ⌈L/10⌉, charged before the string is created. L is the length in UTF-8 bytes of the string as it is written in the document, without its quotes and with its escape sequences; for a string without escape sequences and without characters outside ASCII it is the length of the string. The node that reads the string charges it: a selection or an index that finds it, the index of an array, the element of a range or of a comparison, and the identifier of a variable bound to a JSON string. The names of an enumeration are inside the row above and are not charged again; `has()` and `in` read no value |
| the equality of two objects of the application | the row of two objects in [Equality and order](#equality-and-order) |
| the read of a field of type `google.protobuf.Any` of a protobuf message, U the length of the type URL in UTF-16 units and B the bytes of the content, with the `Any` nested in it | 1 + ⌈(U + B)/10⌉ beyond the unit of the read of the field; for a `repeated` or `map` field, the sum over its elements; charged before the content is parsed, also when it does not unpack. A field that is not set reads as `null` and costs nothing beyond the read |
| the equality of two protobuf messages | the row of two protobuf messages in [Equality and order](#equality-and-order) |
| the read of a member of a type of a type context that is a structure that the read boxes: an object type that is a structure or a `record struct`, a `Nullable<X>` of it, a structure that is a collection other than `ImmutableArray<X>`, a `JsonElement`; S the size of the structure in bytes | 1 + ⌈S/64⌉ beyond the unit of the read of the field. S is the sum of the sizes of all instance fields of the structure without padding, which the source generator computes: an estimate of the box, not a measurement. A `Nullable<X>` that has no value reads as `null` and costs nothing beyond the read |
| the read of a member of a type of a type context that is a set (`IReadOnlySet<X>`, `ISet<X>`, `HashSet<X>`, `SortedSet<X>`, `ImmutableHashSet<X>`), n elements | 1 + c · n · ⌈log₂(n + 1)⌉ beyond the unit of the read of the field, for the sorted copy that each read makes; c is 1 for numbers, `bool`, enumerations, times and `decimal`, and 1 + ⌈2 · Lmax/10⌉ for `string`, `bytes` and `Guid` elements (Lmax the longest element; 36 for a `Guid`). A `null` member costs nothing beyond the read |
| the read of a member of a type of a type context that is a collection without an index (`Queue<X>`, `Stack<X>`, `LinkedList<X>`), n elements | 1 + n beyond the unit of the read of the field, for the copy that each read makes. A `null` member costs nothing beyond the read |
| a set that an application function returns, read as a list, n elements | 1 + c · n · ⌈log₂(n + 1)⌉, charged by the call after the implementation and before the sort; c is as above for `string` and `bytes` elements (Lmax the longest) and 1 for the others |
| a collection without an index that an application function returns, n elements | 1 + n, charged by the call after the implementation and before the copy |
| the check of a value against a declared type | the section [Values checked against their declared type](#values-checked-against-their-declared-type); a typed view that already fits costs 0 |

A set and a collection without an index that are bound to a variable are converted by `CelBindings.Add` before the evaluation and cost nothing at the evaluation.

A member of a type that a type context describes ([Types of a type context](type-mapping.md#types-of-a-type-context)) is the first row of this section, 1 unit, and the three rows above for a structure, a set and a collection without an index add to it. The generator writes the charge into the description of the type, and it is made only when the evaluation has a cost limit, before the member is read, so a limit stops a read before the copy or the sort that it would pay for; the getter of a member that is a set or a collection without an index is called once more for that. A view of a list or of a map and the pair of an element with its description are made in time and memory that do not grow with the data, and cost no more; the string of a `Guid` member is not charged beyond the unit of the read.

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
| the divisor of the build of the program of a pattern of `matches` | 4 |
| the divisor of the search of `matches` | 10 |
| the divisor of the box of a structure | 64 |
| the units of `decimal(double)` | 4 |

These numbers are final: B is 64, and the units of this page are the units of 1.0. After 1.0 a number changes only in a major version.

The units are calibrated by a property test that compares `GC.GetAllocatedBytesForCurrentThread` with the charged units. With the cost limit `ulong.MaxValue` the bytes that an evaluation allocates beyond its frame are at most the units it charges times B. The bytes are counted from after the frame is made to the end of the evaluation, as the smallest of three evaluations after the first, so that the one-time initialization is not counted, and neither are the few kilobytes that the runtime allocates on the thread of the evaluation when it moves methods between tiers. Each row of the tables below was measured on data of the sizes 0, 1, 10, 100 and 1,000 (a row that needs an element, or whose units grow with the square of the size, has its own sizes), through `Compile` and through the planner without type checking, on `net11.0`, JIT, Release. The number of the row is the largest number of bytes per unit over the sizes and the two ways; the test fails when a row is above B. B does not rise to fit a row: a row that did not fit changed its formula or its allocation.

Every row fits B. The largest rows are 64.0: the two error values of one unit, since the creation of the error value of an operation that fails, such as `1 / 0` or `lists.range(-1)`, allocates one object of 64 bytes (see below), and the sorted copy of a `SortedSet<string>`, whose enumerator allocates a stack of its own. All the other rows are at most 62.7.

### Strings set

| Row | Bytes per unit |
| :- | -: |
| strings, charAt | 6.0 |
| strings, format, %f with a precision of 100 | 19.4 |
| strings, format, %s of a list | 20.0 |
| strings, format, %s of a long string | 19.8 |
| strings, format, %s of a map | 21.7 |
| strings, format, %x of a long string | 19.9 |
| strings, indexOf, pattern over 256 | 9.2 |
| strings, join | 5.3 |
| strings, lowerAscii | 12.0 |
| strings, quote | 13.3 |
| strings, replace, many matches | 13.3 |
| strings, replace, no match | 0.0 |
| strings, reverse | 12.0 |
| strings, split, many parts | 29.1 |
| strings, substring | 0.0 |
| strings, trim | 10.0 |
| strings, upperAscii | 12.0 |

### Lists set

| Row | Bytes per unit |
| :- | -: |
| lists, distinct | 18.7 |
| lists, flatten of a deep list | 26.7 |
| lists, flatten of lists | 24.0 |
| lists, range | 40.0 |
| lists, reverse | 32.0 |
| lists, slice | 32.0 |
| lists, sort of numbers | 16.0 |
| lists, sort of strings | 6.7 |
| lists, sortBy | 20.0 |

### Sets set

| Row | Bytes per unit |
| :- | -: |
| sets, contains | 0.0 |
| sets, equivalent | 0.0 |
| sets, intersects | 0.0 |

### Math set

| Row | Bytes per unit |
| :- | -: |
| math, greatest of a list | 0.0 |
| math, least of a list | 0.0 |

### Encoders set

| Row | Bytes per unit |
| :- | -: |
| encoders, base64.decode | 8.0 |
| encoders, base64.encode | 11.4 |
| encoders, json.encode of a nested value | 20.0 |

### Network set

| Row | Bytes per unit |
| :- | -: |
| network, ip() of a prefix | 14.7 |
| network, isCIDR of a text of the size | 0.0 |
| network, isIP of a text of the size | 0.0 |
| network, masked | 16.0 |
| network, string(cidr) | 13.0 |
| network, string(ip) | 12.6 |

### Macros of the sets

| Row | Bytes per unit |
| :- | -: |
| macros, cel.bind | 32.0 |
| macros, math.greatest of three arguments | 18.0 |
| macros, transformList | 28.0 |
| macros, transformList with a filter | 28.0 |
| macros, transformMap | 26.7 |
| macros, transformMap with a filter | 20.0 |
| macros, transformMapEntry | 31.2 |
| macros, transformMapEntry with a filter | 26.0 |

### Macros of two variables

| Row | Bytes per unit |
| :- | -: |
| two variables, all | 0.0 |
| two variables, all over a map | 8.0 |
| two variables, exists | 0.0 |
| two variables, existsOne | 0.0 |

### Optional set

| Row | Bytes per unit |
| :- | -: |
| optional, first | 24.0 |
| optional, hasValue | 16.0 |
| optional, last | 24.0 |
| optional, list literal with ? | 42.7 |
| optional, list literal with ? of empty optionals | 24.0 |
| optional, map literal with ? | 33.6 |
| optional, none | 0.0 |
| optional, of | 24.0 |
| optional, ofNonZeroValue of a list | 24.0 |
| optional, optFlatMap with a value | 18.0 |
| optional, optional index of a list | 24.0 |
| optional, optional index of a map | 16.0 |
| optional, optional selection of a map | 0.0 |
| optional, optMap of an empty optional | 6.0 |
| optional, optMap with a value | 18.0 |
| optional, or of an empty optional | 12.0 |
| optional, orValue of an empty optional | 0.0 |
| optional, unwrap | 25.1 |
| optional, unwrapOpt | 25.1 |
| optional, value | 16.0 |

### Decimal set

| Row | Bytes per unit |
| :- | -: |
| decimal, comparison | 0.0 |
| decimal, decimal(double), a large number | 0.0 |
| decimal, decimal(double), a third | 0.0 |
| decimal, decimal(int) | 0.0 |
| decimal, decimal(string) | 0.0 |
| decimal, decimal(string) of a text that does not parse | 32.0 |
| decimal, difference | 0.0 |
| decimal, double(decimal) | 0.0 |
| decimal, equality | 0.0 |
| decimal, int(decimal) | 0.0 |
| decimal, negation | 0.0 |
| decimal, product | 0.0 |
| decimal, quotient | 0.0 |
| decimal, string(decimal) | 8.7 |
| decimal, sum | 0.0 |

### Protobuf messages

| Row | Bytes per unit |
| :- | -: |
| protobuf, Any with a string | 19.9 |
| protobuf, Any with bytes | 11.6 |
| protobuf, equality of two messages | 51.8 |
| protobuf, field, bytes | 0.0 |
| protobuf, field, Duration | 0.0 |
| protobuf, field, enum | 12.0 |
| protobuf, field, int64 | 12.0 |
| protobuf, field, message | 0.0 |
| protobuf, field, string | 0.0 |
| protobuf, field, Timestamp | 0.0 |
| protobuf, field, wrapper | 12.0 |
| protobuf, ListValue | 24.0 |
| protobuf, literal of a small message | 44.0 |
| protobuf, literal with a bytes value | 36.0 |
| protobuf, map field | 40.0 |
| protobuf, map field, in | 26.0 |
| protobuf, map field, traversal | 62.7 |
| protobuf, repeated Any | 8.0 |
| protobuf, repeated field | 36.0 |
| protobuf, repeated field, traversal | 19.2 |
| protobuf, repeated message field, traversal | 16.0 |
| protobuf, Struct | 28.0 |
| protobuf, Struct, traversal | 41.3 |
| protobuf, Value with a string | 0.0 |

### Data of the application (generated types)

| Row | Bytes per unit |
| :- | -: |
| generated, Dictionary<Guid, long>, traversal | 39.1 |
| generated, Dictionary<long, string>, traversal | 49.6 |
| generated, Dictionary<string, long>, read | 28.0 |
| generated, Dictionary<string, long>, traversal | 41.3 |
| generated, HashSet<long> | 30.0 |
| generated, ImmutableArray<long>, read | 24.0 |
| generated, ImmutableArray<long>, traversal | 24.0 |
| generated, ImmutableHashSet<long> | 56.0 |
| generated, IReadOnlySet<long> | 30.0 |
| generated, LinkedList<long> | 32.0 |
| generated, List<long>, read | 24.0 |
| generated, List<long>, traversal | 24.0 |
| generated, long[], read | 24.0 |
| generated, nested lists, traversal | 24.0 |
| generated, objects in a list, traversal | 24.0 |
| generated, Queue<long> | 32.0 |
| generated, SortedDictionary<string, long>, traversal | 52.0 |
| generated, SortedSet<string> | 64.0 |
| generated, Stack<string> | 32.0 |
| generated, structure that is a list | 22.0 |

### JSON documents

| Row | Bytes per unit |
| :- | -: |
| in, JSON object | 52.5 |
| json, a string of the size | 19.3 |
| json, equality of two objects | 27.2 |
| json, has() on an object | 0.0 |
| json, in on an array | 0.0 |
| json, index of an array | 0.0 |
| json, index of an object by a name | 0.0 |
| json, key lookup in an object | 0.0 |
| json, size of an array | 0.0 |
| json, traversal of an array | 0.0 |
| json, traversal of an object | 24.8 |
| size, JSON object | 52.5 |

### Copies of arguments of application functions

| Row | Bytes per unit |
| :- | -: |
| copy, bytes | 10.0 |
| copy, list(int) | 16.0 |
| copy, map(string, int) | 19.4 |

### Time

| Row | Bytes per unit |
| :- | -: |
| time, duration(string) | 0.0 |
| time, getDayOfWeek without a zone | 0.0 |
| time, getHours with a zone name | 0.0 |
| time, getHours with an offset | 0.0 |
| time, string(duration) | 14.4 |
| time, string(timestamp) | 17.6 |
| time, timestamp plus duration | 0.0 |
| time, timestamp(string) | 0.0 |

### Error values

| Row | Bytes per unit |
| :- | -: |
| error, 1 / 0 | 64.0 |
| error, a duplicate key in transformMapEntry | 25.5 |
| error, base64.decode(x) | 21.3 |
| error, charAt past the end | 32.0 |
| error, cidr(x) | 32.0 |
| error, format with a precision of 101 | 24.0 |
| error, format with an unknown letter | 24.0 |
| error, getHours with the zone +1:5 | 16.0 |
| error, int(x) | 32.0 |
| error, ip(x) | 32.0 |
| error, lists.range(-1) | 64.0 |
| error, timestamp(i) outside the range | 32.0 |

### Standard library and literals

| Row | Bytes per unit |
| :- | -: |
| add_bytes | 6.9 |
| add_list | 32.0 |
| add_string | 10.0 |
| bool_to_string | 0.0 |
| bytes_to_string | 12.0 |
| contains, a short pattern | 0.0 |
| contains, pattern over 256 | 9.2 |
| double_to_string | 16.0 |
| endsWith | 0.0 |
| equality, two bytes | 0.0 |
| equality, two lists | 0.0 |
| equality, two maps with string keys | 5.3 |
| equality, two strings | 0.0 |
| filter | 28.0 |
| in, a list | 0.0 |
| in, a map with string keys | 0.0 |
| index, a list | 0.0 |
| index, a map with string keys | 0.0 |
| int64_to_string | 14.0 |
| list literal | 40.0 |
| map | 28.0 |
| map literal | 40.4 |
| matches, a literal pattern | 0.0 |
| matches, a pattern from a variable | 0.0 |
| ordering, two bytes | 0.0 |
| ordering, two strings | 0.0 |
| size, a list | 0.0 |
| size, a string | 0.0 |
| startsWith | 0.0 |
| string_to_bytes | 9.6 |
| string_to_double | 0.0 |
| string_to_int64 | 0.0 |
| string_to_uint64 | 0.0 |
| uint64_to_string | 16.0 |

The evaluation that a cost limit stops while a loop builds its result, `map` and `filter` over 1,000 elements with the limits 2, 3, 10 and 100, allocates at most B bytes for each unit charged at the stop and the bytes of the stop itself, the exception that leaves the evaluation and the error value: 2,048 bytes at most, measured 469.3 bytes for each unit with the stop included, so the array of the result is not made for the whole range at the first units.

A map literal did not fit 1 + n: a pair is two values of 32 bytes, so the index that finds a key in constant time did not fit 64 bytes for each unit; the unit count of a map is therefore 1 + 2n. The creation of an error value allocates at most 64 bytes, one object without strings or arrays, whose text and position are built when the error is read, so a refusal of an operation of 1 unit, such as `1 / 0`, fits. An error value itself is not charged.

Two constants of the descriptions of protobuf messages are outside B, and the rows of messages above measure what grows with the data: the instance of a class that protoc generates allocates each of its `repeated` and `map` fields when it is made, 27,904 bytes for `TestAllTypes`, which is the code of the application, as a function of the application is, and not limited by the library; and the reflection of Google.Protobuf boxes the value of each field of the description that a comparison of two messages tests, about 1.7 KB for two empty `TestAllTypes`, a constant of the description that does not grow with the data. The rows of messages use a small message of the tests for these two. The set that a read of an application collection makes is the shared empty list when the collection has no element, so the read of an empty set allocates nothing.

`matches` at n = 0, a string of no characters, for patterns of the sizes m = 1, 10, 100, 1,000, 10,000 and 100,000 (`net10.0`, JIT, Release; the bytes of a call of the program after the bytes of a call of a program that reads the same variables and compares them, so that the frame and the result are not counted, and the units of the whole expression, among them the reads of the operands):

| Case | Bytes per unit |
| :- | :- |
| a pattern that is a literal | 0 |
| a pattern from data that is found in the cache of patterns | 0 |
| a pattern from data that is not in the cache: the parse, the build and the program that the cache keeps, 16 bytes for each instruction | 45.6 at most, 44.4 to 46.0 for the sizes from 10 |
| the first evaluation of a size in a process, which takes the arrays of the engine from the pool of arrays for the first time (once, not a rate) | 189 at most for a pattern from data, 318 for a literal |

The arrays of the engine and of the parser come from `ArrayPool<int>.Shared` and go back to it, so in the steady state a search allocates nothing and the rate is 0; what a miss allocates is the program that stays in the cache, which the units of the build pay for at 64 bytes each (⌈m/4⌉ units for 16 · m bytes). The divisor of the search, 10, is the time: about 10 ns for each unit at the size limit, as the page [Limits](limits.md#pattern-of-matches) shows. The measurement also checked each formula of the table of the `matches` section: with the cost limit equal to the units of the whole expression the call runs, and with one unit less it stops.
