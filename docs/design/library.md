# Library: the standard library

`Great.Parts.Cel.Library` holds the standard library of each version: the type identifiers, the declarations of its functions and overloads, their implementations and their costs. An environment names its version in `new CelEnvironmentBuilder(CelStandardLibrary.V1)`; the public `CelStandardLibrary.V1` is a handle to the table of version 1 in this namespace (see [core](core.md)).

This part has version 1 without `matches`: twelve type identifiers and 160 overloads — the 113 of the earlier parts and the 47 of time. The standard macros are expanded by the parser ([syntax](syntax.md)) and evaluated by the comprehension node ([evaluation](evaluation.md)). Identifiers, signatures and semantics are those of the [standard library](../standard-library.md) page, costs those of the [cost](../cost.md) page; this page says how they are built. The time functions are in `Library.Time`, `matches` goes to `Library.Regex` and `Library.Unicode`, extension sets to their own tables.

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

## Numbers and text

No culture takes part: the conversions use their own code or invariant formats.

- **`double` to text** is Go `%g` of cel-go: the shortest digits that read back to the same `double` (those of `double.ToString("R", CultureInfo.InvariantCulture)`) with the exponent e of the first digit; when e < −4 or e ≥ 6 the form is `d.ddde±XX`, without trailing zeros and with at least two digits of exponent, otherwise the plain decimal. `-0.0` is `-0`, the infinities `+Inf` and `-Inf`, NaN `NaN`. cel-go v0.32.0 gives `123456`, `1e+06`, `1e-05`, `0.1`, `1e+21` for `123456.0`, `1e6`, `1e-5`, `0.1`, `1e21`.
- **Text to `double`** checks the forms of the standard library page first — decimal, hexadecimal with a binary exponent, `_` between digits, `inf`, `infinity` and `nan` — then reads a decimal with `double.Parse` and `CultureInfo.InvariantCulture` after removing `_`, which rounds correctly; a finite text that reads as an infinity is `ConversionRange`. A hexadecimal form is read by own code, rounded to nearest, ties to even.
- **Integers to and from text** — own ASCII code: `[+-]?digits` for `int`, digits without a sign for `uint`, the whole string, no `_`, spaces or prefixes; `bool` takes the twelve strings of the page.

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

Related pages: [evaluation](evaluation.md), [checking](checking.md), [values](values.md), [time zones](time-zones.md), [declarations](declarations.md), [core](core.md).
