# Extension set `strings`: `format`

`string.format(list(dyn))` fills the clauses of a format string with the elements of a list. The rules are those of `string.format` in cel-go v0.32.0 (`ext/formatting_v2.go`), version 2 of the format, which the set `strings` version 5 has, with the limit of 100 on the precision. This page gives the grammar, the table of clauses and kinds, the texts of the errors, the check at compilation and how the call is charged. The rest of the set is on [extension-strings](extension-strings.md).

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Library/Extensions/Strings/FormatEngine.cs` | the parser of the format string (`FormatParser`), the table of specifier by kind of value (`FormatRules`), and the failure and clause records, shared by the evaluation and the check |
| `src/Great.Parts.Cel/Library/Extensions/Strings/FormatValues.cs` | the writing of one value for a clause: integers in bases 2, 8, 10 and 16, strings, bytes, lists and maps in `%s`, time through the texts of `Library.Time` |
| `src/Great.Parts.Cel/Library/Extensions/Strings/FloatText.cs` | the text forms of a `double`: shortest, fixed, scientific |
| `src/Great.Parts.Cel/Library/Extensions/Strings/FormatCheck.cs` | the check of a call at compilation, over a neutral description of its arguments |
| `src/Great.Parts.Cel/Library/Extensions/Strings/StringFunctions.Format.cs` | the overload `string_format`: the argument cost, the evaluation and the error values |
| `src/Great.Parts.Cel/Declarations/CallValidator.cs` | the delegate and the description types through which the checker calls a validator ([checking](checking.md#calls-that-the-checker-validates)) |

## Format string

```text
format   = { text | "%%" | clause }
clause   = "%" [ "." digits ] letter
digits   = { "0" … "9" }                   (ASCII)
letter   = "s" | "d" | "f" | "e" | "b" | "x" | "X" | "o"
```

Everything but `%` is copied; `%%` is one `%`. A clause takes the next element of the list: the first clause the first element, and so on. There are no flags, width, `*` or positions: `%5d` is the error `unrecognized formatting clause "5"`. A precision is read after any clause, but only `%f` and `%e` use it; without one it is 6. A format string is read by its UTF-16 units: `%`, the digits and the letters are ASCII, so no other unit can be one of them.

The parser takes the clauses from left to right, and the first error ends the call. At a `%` that does not start `%%`, in this order:

1. no unit after it — `unexpected end of string`;
2. no element of the list for this clause — `index {i} out of range`, `{i}` the number of the clause from 0;
3. the precision, if a `.` follows: the digits up to the first non-digit; the end of the string before one — `could not parse formatting clause: error while parsing precision: could not find end of precision specifier`; no digit — `…: error while converting precision to integer: invalid syntax`; a number that does not fit a `long` — `…: error while converting precision to integer: value out of range`; a number above 100 — `…: error while parsing precision: precision {n} exceeds maximum allowed precision 100`. The limit is the constant `ExtensionLimits.MaxFormatPrecision`, 100 (in `Library/ExtensionLimits.cs`); versions of the set below 5 have none, and this release has only version 5;
4. the letter: none of the eight — `could not parse formatting clause: unrecognized formatting clause "{c}"`, `{c}` the code point;
5. the value: a kind the clause does not take — `error during formatting: {text}`, the texts in the table below.

The detail of the errors is these texts. At compilation they are the detail of `InvalidFormatString` (2010), `invalid format string: {detail}`; at evaluation the detail of `InvalidArgument` (3008), `invalid argument to 'format': {detail}`; the error value takes its parts by reference, so the text is formed when it is read. At evaluation the place is the call; at compilation it is the call too, except the error of a kind, which is at the element of the list literal that does not fit its clause (a nested element for a nested literal), as in cel-go. cel-go orders steps 1 and 2 the other way at evaluation; the library uses the order above everywhere, which changes only the text of an error, never whether there is one.

## Clauses and kinds

`{type}` in a text is the runtime type name at evaluation — `int`, `null_type`, `list`, `net.IP` — and the name of the static type at compilation, so a timestamp is `timestamp` at evaluation and `google.protobuf.Timestamp` at compilation, a duration `duration` and `google.protobuf.Duration`; the name of a wrapper type is that of its primitive. Kinds not listed are errors.

| Clause | Kinds | Output | Text of the error |
| :- | :- | :- | :- |
| `%s` | string | as it is | `string clause can only be used on strings, bools, bytes, ints, doubles, maps, lists, types, durations, and timestamps, was given {type}` |
| | bytes | the text of the bytes as UTF-8; an invalid sequence becomes `U+FFFD` | |
| | bool, int, uint | `true`, `false`; decimal digits with a `-` for a negative `int` | |
| | double | the shortest digits that read back to the same `double`, laid out without an exponent: `1e21` is `1000000000000000000000`, `1e-7` is `0.0000001`, `0.1` is `0.1`, `-0.0` is `-0`; NaN is `NaN`, the infinities `Infinity` and `-Infinity` | |
| | null, type | `null`; the name of the type, `int`, `list`, `net.IP` | |
| | duration, timestamp | the text of `string()` of the value, from `Library.Time`: `90s`, `1.5s`; `1970-01-01T00:00:00Z`; for the text of a duration see [Differences from cel-go](#differences-from-cel-go) | |
| | list | `[` the elements by this table, strings unquoted, separated by `, `, `]` | |
| | map | `{` `key: value` pairs separated by `, ` `}`; keys and values by this table; the pairs ordered by the text of the key, by code points, stably | |
| `%d` | int, uint | decimal digits | `decimal clause can only be used on ints, uints, and doubles, was given {type}` |
| | double | the form of `%s` | |
| `%f` | int, uint, double | exactly `p` digits after the point, `p` the precision; the number is correctly rounded from its exact binary value, an exact tie to the even digit; an `int` or `uint` is converted to `double` first; the sign of a negative zero and of a small negative number that rounds to zero stays: `-0.00`. NaN is `NaN`, the infinities `Infinity` and `-Infinity`, whatever the precision | `fixed-point clause can only be used on ints, uints, and doubles, was given {type}` |
| `%e` | int, uint, double | one digit, a point, `p` digits, `e`, the sign and the exponent with at least two digits: `1.235e+04`, `0.000000e+00`, `1.00e+100`; rounding as `%f`; NaN and infinities as `%f` | `scientific clause can only be used on ints, uints, and doubles, was given {type}` |
| `%b` | bool, int, uint | `1` or `0`; base 2, a negative `int` as `-` and the digits of its magnitude: `-101` | `only ints, uints, and bools can be formatted as binary, was given {type}` |
| `%o` | int, uint | base 8, a negative `int` as `-` and its magnitude | `octal clause can only be used on ints and uints, was given {type}` |
| `%x`, `%X` | int, uint | base 16 in lower or upper case, a negative `int` as `-` and its magnitude: `-ff` | `only ints, uints, bytes, and strings can be formatted as hex, was given {type}` |
| | string, bytes | two digits for each byte of the UTF-8 text of the string, or of the bytes | |

A value that the clause takes but the list holds under a wrapper or `dyn` is the value itself: the kind of the runtime value decides. Elements of the list beyond the clauses are not used and are not an error at evaluation; only the check at compilation reports them.

## Text of a `double`

The three forms are in `FloatText`, over `Span<char>` buffers and the invariant culture; none uses the culture of the process.

- **Shortest.** The digits and the decimal exponent of `DoubleTextRules` ([core](core.md)), the same that `string(double)` uses, laid out without an exponent: the digits with zeros added before or after, and a point where the exponent puts it.
- **Fixed and scientific.** `double.TryFormat` with the formats `F{p}` and `e{p}` and `CultureInfo.InvariantCulture`: on `net10.0` and `net11.0` it is exact for any precision and rounds an exact tie to the even digit, as Go does — `'%.0f'.format([2.5])` is `2` and `'%.2f'.format([0.125])` is `0.12`; this was checked on both targets and a test pins it, with a reference over `BigInteger` on ties and on 767-digit values. The exponent of `e{p}` has three digits at least (`e+003`) and `FloatText` cuts it to two where the third is a zero. A buffer over 256 units — `%.100f` of `1e300` has 402 — is rented from `ArrayPool<char>.Shared` and returned; the steady state allocates only the result.
- **Conversion of an integer.** `(double)long` and `(double)ulong` round to nearest, ties to even, as `float64(x)` in Go; `'%.2f'.format([9007199254740993])` is `9007199254740992.00`.

## Check at compilation

A call is checked when the function is `format` called on a receiver, the receiver is a string literal, and the only argument is a list literal without optional elements. The binding of `string_format` carries a validator, `FormatCheck`, which [checking](checking.md#calls-that-the-checker-validates) runs after the traversal of the tree, when it has found no error, with the final static types of the receiver and of the elements, and reports its answer as `InvalidFormatString` (2010), at the call or at the element that the answer names. The check runs only for a call that has the overload `string_format` among its candidates. A receiver or an argument that is a variable, a call, a `dyn` or any other expression is not checked, and nothing but evaluation can find its errors.

`FormatCheck` runs the parser of the format string with the clauses of the table and, for each clause, looks at the shape of the element in place of a value (`CallShape` and `ValueShape` are defined on the page of the checker):

- a kind check by the static type: `dyn` passes every clause; a wrapper counts as the wrapped primitive; a type parameter was already replaced by `dyn`; the kinds of the table pass, any other — optional, an object, `google.protobuf.Any`, an abstract type — is the error of the clause with the name of the type;
- for `%s` and an element that is a list or a map literal, the elements, keys and values are checked in turn by the same rule, so `'%s'.format([[1, ip('1.2.3.4')]])` is an error although a list passes;
- the first error ends the check; its detail is the text of "Format string", `error during formatting: …` for a kind, and its place is the element that failed, `CallValidation.Offset`; the other errors are at the call;
- when the format has no error, more elements than clauses give `too many arguments supplied to string.format (expected {expected}, got {actual})`; fewer give `index {i} out of range` from step 2.

Nothing of a checked call changes: the plan is that of an unchecked one, and the checker keeps no record of the result.

## Evaluation and cost

`string_format` has the implementation `StringFormat(receiver, list, site)`: the same parser, with the value of each element in place of its shape. The list argument is typed `list(dyn)`, so it is not marked and no check of the depth is charged. A list element that is an error value cannot occur: a list holds none.

The cost is 1 + n + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉: `Lₐ` the length of the format string alone, n the number of elements of the list, `Lᵣ` the length of the result including the text of nested values. 1 + n + ⌈Lₐ/10⌉ is the argument cost, charged before the call; the rest is charged through `ResultBuilder` as the result grows, before each piece is appended. A map in `%s` formats each key and each value into a text first, which is charged when it is made, from the same running total, and the sorted output is copied without a second charge, so the charges add up to ⌈Lᵣ/10⌉ and the temporary texts stay within B bytes per unit. The sort of the pairs — a stable insertion sort up to 12 pairs, a merge sort above, ordered by code points like the ordering of strings — does O(n log n) comparisons that the formula does not charge apart. An error ends the call after the units it has charged; a failed clause allocates only the error value.

## Differences from cel-go

- The text of an error may differ in steps 1 and 2 and in the code point of an unrecognized letter, which cel-go reads as a byte; whether the call fails does not.
- `%s` of bytes with an invalid UTF-8 sequence gives `U+FFFD`; cel-go keeps the raw bytes in a Go string, which a UTF-16 string cannot hold. This is a deliberate difference, recorded in the registry of known differences as one record, `string_format`.
- The text of a duration is that of `string()`; for a duration of more than 17 significant digits cel-go loses digits through a 64-bit float, the cel-go defect that the registry records for `duration_to_string`; the text here is exact.

Related pages: [extension-strings](extension-strings.md), [extensions](extensions.md), [checking](checking.md), [library](library.md).
