# Extension sets

An extension set is a named, versioned group of declarations that an environment adds as a whole: functions with their implementations and costs, macros, abstract types and, for the sets of the library, syntax. The library has a catalog of sets, `CelExtensions`; an application can also build sets of its own with `CelExtension.Create`. This page gives the rules that hold for every set, the catalog with the versions, the checks that `CelEnvironmentBuilder.Build` makes over the sets of an environment, and the codes of the errors. The functions of each set, with their identifiers, signatures and errors, are described set by set as the sets are added to the library; the costs are on the page [Cost](cost.md), the limits on the page [Limits](limits.md), and all error codes on the page [Error codes](error-codes.md).

## Adding a set

A set is added with `CelEnvironmentBuilder.AddExtension`, and its version is written out every time:

```text
new CelEnvironmentBuilder(CelStandardLibrary.V1)
    .AddExtension(CelExtensions.Strings(version: 5))
    .AddExtension(CelExtensions.Math(version: 3))
    .Build()
```

- **The call only collects.** `AddExtension` and the members of `CelExtensions` throw only for an argument that is `null`. A set that does not exist, a version that it does not have and every conflict are build errors that `Build` reports, together with all the other errors of the environment.
- **The same set added twice is one set.** Two members of the catalog with the same name and version, and the same `CelExtension` instance added twice, are one set: adding them twice adds nothing.
- **The order of the calls does not matter.** The environment, the errors and their texts are the same for every order of `AddExtension` and of the other calls of the builder. The environment keeps the sets ordered by name and version; the candidates of a call are the overloads of the standard library, then those of the sets in that order, then those of the application.
- **A set cannot be removed or replaced.** There is no call that takes a set out of an environment or exchanges one version for another; build a new environment from a builder with the sets you want.
- **`ToBuilder` keeps the sets.** The builder that `CelEnvironment.ToBuilder` returns has the sets of the environment.

## The catalog

| Set | Name in an environment description | Member of `CelExtensions` | Version in this release | Needs |
| :- | :- | :- | :- | :- |
| `strings` | `strings` | `Strings(version)` | 5 | none |
| `math` | `math` | `Math(version)` | 3 | none |
| `lists` | `lists` | `Lists(version)` | 4 | none |
| `sets` | `sets` | `Sets(version)` | 0 | none |
| `encoders` | `encoders` | `Encoders(version)` | 1 | none |
| `bindings` | `bindings` | `Bindings(version)` | 1 | none |
| two-variable comprehensions | `two-var-comprehensions` | `TwoVarComprehensions(version)` | 0 | none |
| `network` | `network` | `Network(version)` | 1 | none |
| `protos` | `protos` | `Protos(version)` | 0 | none |
| `optional` | `optional` | `Optional(version)` | 2 | none |
| `decimal`, only on `net11.0` | none: a description cannot add it | `Decimal(version)` | 1 | none |

- **Versions are those of cel-go.** The number of a version is the number of the same set in cel-go, the CEL implementation in Go, and a version has the declarations that cel-go gives it; `decimal`, which cel-go does not have, has a number of its own. This release has one version of each set. A version that the table does not have is not a number to try: it is the build error `UnknownExtensionVersion`.
- **The functions of a version never change.** A function that cel-go adds in a later version comes as that version, and an environment that names the versions it was built with means the same after the library is updated. A fix of the behavior of a function applies to the version that has it.
- **The sets do not depend on each other.** None of the sets above needs another, so the build error `MissingExtensionDependency` can only come from a set of the application.
- **All sets are in both targets.** `net10.0` and `net11.0` have the same sets with the same declarations; the `decimal` set of this library is only on `net11.0`.

## What the build checks

`Build` checks the sets before the declarations, and the sets that pass join the checks of the declarations. A set that fails one of the first three steps is left out, so it causes no second error: a set with an unknown version does not also overlap the functions of another set.

| Step | Code | When | Message |
| :- | :- | :- | :- |
| 1 | `UnknownExtensionVersion` (4004) | the set has no such version | `extension '{name}' has no version {version}; available versions: {versions}`, the versions ascending, separated by `, `; one error for each set |
| 2 | `ExtensionVersionConflict` (4003) | one name is added in two versions | `extension '{name}' is added in versions {version1} and {version2}`, the smaller number first; with three versions, one error for each pair |
| 3 | `DuplicateExtension` (4018) | two different sets have one name and one version | `extension '{name}' version {version} is added as two different sets: {source1} and {source2}` |
| 4 | `MissingExtensionDependency` (4005) | a set needs a set that is not in the environment | `{dependent} requires extension '{name}', which is not added to the environment`, with `{dependent}` as `extension {name} v{version}`; one error for each missing name |

- **The sources.** A set of the catalog is named `extension strings v5` and a set of the application `application`, as the declarations of the application are named everywhere. The two sources of a message are in the order of the text, so `application` stands before `extension strings v5`, and the message does not depend on the order of the calls.
- **Names of the catalog for sets of the application.** An application may build a set with the name of a set of the catalog. In an environment that also adds the catalog set with the same version, the two are different sets and the error is `DuplicateExtension`; with another version it is `ExtensionVersionConflict`.
- **Dependencies are never added implicitly.** A dependency is met by a set of its name in any version that the environment has. A set that was left out by one of the first three steps is not in the environment, so it does not meet a dependency.
- **The place.** These errors have the stage `Build` and no place in an expression. When a set comes from an [environment description in JSON](environment-json.md), the error has the place of the `{` of its element in the `extensions` array.
- **Overlaps.** The overloads of different sets, and of a set and the standard library or the application, are checked as the overloads of the application are: overloads that are the same or overlap after the type parameters are erased are `OverlappingOverloads` (4002) for two sets or a set and the application, and `StandardLibraryConflict` (4006) for one that overlaps the standard library; one overload identifier used twice in the environment is `DuplicateOverloadId` (4013). The message names both sources, such as `overload 'a' of application overlaps overload 'b' of extension strings v5 of function 'f'`.

## Sets of the application

`CelExtension.Create` builds a set from function declarations and their implementations, for example a group of functions that several environments share:

- The functions of the set are functions of the application. Their names follow the rules of `CelFunctionDecl`, they get the checks of the arguments and the result against the declared types, and the cost function of an overload is the cost of its call.
- The implementations belong to the set. An overload of the set with no implementation among those given to `Create` is `MissingImplementation` (4007), an implementation for which the set has no overload is `UnusedImplementation` (4010), and an implementation that the builder registers for an overload of a set is `DuplicateImplementation` (4014).
- A set can name sets that it needs. They have to be added to the environment; otherwise `Build` reports `MissingExtensionDependency`.
- `Create` checks only the arguments that its documentation lists and copies them: a later change of the array, the dictionary or a declaration cannot change the set. Name conflicts and every other build error are reported by `Build`.
- A set of the application brings no macros, abstract types or variables.

## Macros of sets

A set can bring macros, which the parser expands before type checking. A macro of a set is expanded only in an environment that has the set; without it the same call is an ordinary call, and type checking reports the function as undeclared. A function of the application with the name, the call style and the number of arguments of a macro of the environment — of the standard library or of a set — would never run, since the parser replaces the call; it is the build error `StandardLibraryConflict` (4006), which names the set: `function 'sortBy' of application conflicts with extension lists v4: it has the form of the macro 'sortBy'`.

A macro that cannot expand a call because of its arguments is the parse error `InvalidMacroCall` (1006): `invalid call of macro '{macro}': {detail}`, at the part of the call that is wrong. A call with another number of arguments than any macro of the set takes is not a macro call and stays a call.

## In an environment description

The key `extensions` of an [environment description in JSON](environment-json.md) adds a set by its name and version, as `AddExtension(CelExtensions.<Set>(version))`. The names are those of the catalog above, once the set is part of the library, except that `decimal` has no name in a description and is added in code; the full names of cel-go such as `cel.lib.ext.strings` are not names. A version is a string of decimal digits, and `Build` reports a version that the set does not have, with the place of its element.

## Optional syntax

The set `optional` turns on five forms of the syntax. Each form works with the optional values of the set: a form that selects or indexes gives an optional, and a form that builds a list, a map or a message adds a position only when its optional has a value. Without the set every form is the parse error `SyntaxNotEnabled`, at the `?` of the form: `'.?' is not enabled: add the extension set optional`, with `'[?'` and `'?'` for the others. The parser goes on, so each form of a text is one error, in the order of the text.

| Form | What it gives | Type |
| :- | :- | :- |
| `e.?f` | the value of the key or field `f` of `e` as an optional, and the empty optional when it is not there | `optional_type(V)`, `V` the type of the value |
| `e[?k]` | the same for an index or a key: an index outside a list or a key that a map does not have gives the empty optional, where `e[k]` is an error | `optional_type(V)` |
| `[?e]` | a list that holds the value of the optional `e`, or nothing when `e` is empty | the type of the list of the values |
| `{?k: v}` | a map that has the entry when the optional `v` has a value, and no entry otherwise | the type of the map of the values |
| `T{?f: v}` | a message whose field `f` is set to the value of the optional `v`, and left unset when `v` is empty | the type of the message |

```text
{'a': 1}.?a                              // optional.of(1)
{'a': 1}.?b                              // optional.none()
[1, 2][?1]                               // optional.of(2)
[1, 2][?5]                               // optional.none()
[1, ?optional.of(2), ?optional.none()]   // [1, 2]
{'a': 1, ?'b': optional.none()}          // {'a': 1}
Order{id: 7, ?note: optional.none()}     // an Order whose field note is not set
```

- **The value of a position with `?`** has to be an optional. In a list, a map or a message that the checker types, any other type is `TypeMismatch` at the value — at the `:` of the field in a message literal: `[?1]` is `expected type 'optional_type(int)' but found 'int'`. A value of the type `dyn` passes the check; if it is no optional at evaluation, the position is `EvalNoMatchingOverload` with the function `?` and the type of the value. The expressions of a position that is not stored are still evaluated, in the order of the text, and their errors are errors of the literal. The key of a map entry that is not stored is not looked at, so a key that cannot be a map key is an error only for a stored entry.
- **Optional chains.** An optional is selected and indexed through its value: `e.f` and `e[k]` over an operand of the type `optional_type(T)` select in a `T` and give an optional, and the empty optional stays empty. `e.?f` over an operand that is itself an optional unwraps it once, so `optional.of({'a': {'b': 1}}).?a.?b` is `optional.of(1)` and a chain of `.?` never nests optionals. `{'a': 1}.?a.b` is an error, because `.b` is read on the `int` 1. A value without fields under `dyn` is `FieldSelectionNotSupported`; a name that a message does not have is `NoSuchField` at the check and `EvalNoSuchField` at evaluation.
- **Maps and messages.** For a map the key must be present; for a message the field must be set — a field of proto3 without presence that holds its default counts as not set, as in `has()`.
- **`has()` over an optional.** `has(a.?b)` is no presence test: `a.?b` is a call, not a selection, and the macro refuses it with `InvalidMacroCall` (`argument must be a field selection`). `has(a.?b.c)` tests the presence of `c` in the value of `a.?b`: it is `false` for an empty optional and otherwise the presence of the key in a map or of a set field of a message; a held value that has no fields is `FieldSelectionNotSupported`. Where cel-go v0.32.0 fails with `no such key: bar` for `has({'foo': optional.none()}.foo.bar)`, this library gives `false`: an empty optional has no `bar`.
- **Indexes.** The index of an optional list is an `int`; the `uint` and `double` indexes that the plain index of a list also takes are not taken by `[?` and by an optional operand: `NoMatchingOverload` at the check, `EvalNoMatchingOverload` for an operand of the type `dyn`. The key of a map is looked up across the numeric kinds, so `dyn({1: 'a'})[?1u]` is `optional.of('a')`.
- **Cost.** A selection or an index of an optional costs what the same selection or index of the value costs; a literal with `?` costs what it stores. The units are on the page [Cost](cost.md).
- **Differences from cel-go.** `[?1]` and `{?'a': 1}` are one error each, where cel-go gives a second one when the literal is used further; a field of a message literal whose value is not an optional is reported at its `:` with the text `expected type 'optional_type(F)' but found '...'`.

## Set `strings`, version 5

Functions of strings with indexes and lengths in code points: a valid surrogate pair is one code point, and so is a lone surrogate, which only a string of the application can hold, as `size` counts them. The functions that look for a substring take time linear in the lengths of the string and the substring, and no function depends on the culture of the process. The units of each overload are on the page [Cost](cost.md#set-strings). The function `format` of this set has its own section, [`format`](#format).

| Overload | Call | What it does |
| :- | :- | :- |
| `string_char_at_int` | `s.charAt(i)` | the code point at the index as a string; the index equal to the length gives `""` |
| `string_index_of_string` | `s.indexOf(t)` | the index of the first occurrence of `t`, or `-1`; the empty `t` is found at 0 |
| `string_index_of_string_int` | `s.indexOf(t, i)` | the first occurrence at or after the offset `i`, as an index of the whole string; the empty `t` is found at `i`; at `i` equal to the length a non-empty `t` gives `-1` |
| `string_last_index_of_string` | `s.lastIndexOf(t)` | the index of the last occurrence, or `-1`; the empty `t` gives the length |
| `string_last_index_of_string_int` | `s.lastIndexOf(t, i)` | the last occurrence that starts at or before `i`; the empty `t` gives `i`; at `i` equal to the length a non-empty `t` gives `-1` |
| `string_lower_ascii`, `string_upper_ascii` | `s.lowerAscii()`, `s.upperAscii()` | only the letters `A`–`Z`, or `a`–`z`, change |
| `string_replace_string_string` | `s.replace(old, new)` | every non-overlapping occurrence of `old`, from the left, replaced; with an empty `old`, `new` goes before each code point and at the end |
| `string_replace_string_string_int` | `s.replace(old, new, n)` | at most `n` replacements: all of them for a negative `n`, none for 0 |
| `string_split_string` | `s.split(sep)` | the parts between the occurrences of `sep`; `''.split(',')` is `['']`; an empty `sep` gives one part for each code point, and `''.split('')` is `[]` |
| `string_split_string_int` | `s.split(sep, n)` | at most `n` parts, the last one holding the rest unsplit: `'a,b,c'.split(',', 2)` is `['a', 'b,c']`; all parts for a negative `n`, none for 0 |
| `string_substring_int` | `s.substring(i)` | from the index to the end |
| `string_substring_int_int` | `s.substring(i, j)` | from the index `i` to the index `j`, the end excluded |
| `string_trim` | `s.trim()` | the White_Space code points removed from both ends: U+0009 to U+000D, U+0020, U+0085, U+00A0, U+1680, U+2000 to U+200A, U+2028, U+2029, U+202F, U+205F and U+3000 |
| `list_join`, `list_join_string` | `list.join()`, `list.join(sep)` | the elements joined by `sep`, by nothing in the first form; the empty list gives `""` |
| `string_format` | `s.format(args)` | the clauses of `s` filled with the elements of the list `args`; see [`format`](#format) |
| `strings_quote` | `strings.quote(s)` | `s` in double quotes with `\a`, `\b`, `\f`, `\n`, `\r`, `\t`, `\v`, `\\` and `\"` escaped; every other code point stays as it is, and a lone surrogate becomes U+FFFD |
| `string_reverse` | `s.reverse()` | the code points in reverse order; a pair stays a pair |

- **Errors.** An index or an offset outside 0 to the number of code points is `IndexOutOfRange` (3003) with the text `index out of range: {index}`, at the `(` of the call: `'abc'.charAt(4)`, `'abc'.substring(-1)`, and also `'abc'.indexOf('a', 4)` and `'abc'.lastIndexOf('', 4)` with an offset above the length. `s.substring(i, j)` with `i` above `j` is `InvalidArgument` (3008), `invalid argument to 'substring': invalid substring range: start index {i} must be less than or equal to the end index`; it is checked first, then `i`, then `j`. An element of a list that is not a string, which only a list of the type `dyn` can hold, is `EvalNoMatchingOverload`.
- **Occurrences at code points.** A match of `indexOf`, `lastIndexOf`, `replace` and `split` starts and ends at a code point boundary: a lone low surrogate is not found inside a pair.
- **Results.** A result is created once for its exact length, or built as it is charged, so a cost limit stops `replace`, `split` and `join` before their memory is taken.

### `format`

`s.format(args)` takes the format string `s` and a list `args` and gives `s` with each clause replaced by the text of an element. The clauses take the elements in order: the first clause the first element, the second clause the second, and so on. The text outside the clauses is copied, and `%%` is a single `%`. A clause is `%`, an optional precision `.` and digits, and a letter; there are no flags, width or positions, so `%5d` is an error. The precision is read after any letter but only `%f` and `%e` use it; without one it is 6, and the largest is 100. A format string is read by its UTF-16 units: `%`, the digits and the letters are ASCII, so no other unit can be one of them.

| Clause | Takes | Writes |
| :- | :- | :- |
| `%s` | `string` | the string as it is |
| | `bytes` | the bytes read as UTF-8; an invalid sequence becomes U+FFFD |
| | `bool`, `int`, `uint` | `true` or `false`; the digits, with `-` for a negative `int` |
| | `double` | the shortest digits that read back to the same `double`, without an exponent: `1e21` is `1000000000000000000000`, `1e-7` is `0.0000001`, `-0.0` is `-0`; `NaN`, `Infinity` and `-Infinity` |
| | `null`, a type | `null`; the name of the type, such as `int` or `list` |
| | `timestamp`, `duration` | the text of the conversion `string()` of the value |
| | `list` | `[`, the elements written by this table with strings unquoted and separated by `, `, `]` |
| | `map` | `{`, the pairs `key: value` separated by `, `, `}`; keys and values by this table, the pairs ordered by the text of the key, by code points, and a repeated text keeps the order of the map |
| `%d` | `int`, `uint` | the digits |
| | `double` | as `%s` |
| `%f` | `int`, `uint`, `double` | exactly the precision's number of digits after the point, the value rounded correctly from its exact binary value and a tie to the even digit, so `'%.0f'.format([2.5])` is `2` and `'%.2f'.format([0.125])` is `0.12`; an integer is converted to a `double` first; `NaN`, `Infinity` and `-Infinity` whatever the precision |
| `%e` | `int`, `uint`, `double` | one digit, a point, the precision's number of digits, `e`, a sign and an exponent of two digits at least: `1.235e+04`, `0.000000e+00`, `1.00e+100`; rounded as `%f` |
| `%b` | `bool`, `int`, `uint` | `1` or `0` for a `bool`; base 2, a negative `int` as `-` and the digits of its magnitude |
| `%o` | `int`, `uint` | base 8, a negative `int` as `-` and its magnitude |
| `%x`, `%X` | `int`, `uint` | base 16 in lower or upper case, a negative `int` as `-` and its magnitude |
| | `string`, `bytes` | two digits for each byte of the UTF-8 text of the string, or of the bytes |

A value that the list holds under `dyn` is the value itself: its kind at run time decides. A letter that does not take the kind is an error whose text names the clause and the type that was given, such as `decimal clause can only be used on ints, uints, and doubles, was given string`. Elements of the list past the last clause are not used and are not an error at evaluation.

- **Errors at evaluation** are `InvalidArgument` (3008) at the `(` of the call, `invalid argument to 'format': {detail}`. The first error ends the call, and at a `%` that does not start `%%` the checks go in this order: no character after it (`unexpected end of string`); no element of the list for the clause (`index {i} out of range`, `{i}` the number of the clause from 0); the precision (`could not parse formatting clause: error while parsing precision: …`, with `precision 101 exceeds maximum allowed precision 100` above the limit); the letter (`could not parse formatting clause: unrecognized formatting clause "z"`); and the kind of the value (`error during formatting: …`).
- **Check at compilation.** A call whose format string is a string literal and whose argument is a list literal is checked when the expression is compiled, after its types are checked: `'%d: %s'.format([x, y])`. A mistake of the format string, an element whose type does not fit its clause, or more elements than clauses is `InvalidFormatString` (2010), `invalid format string: {detail}`, with the same details as above and `too many arguments supplied to string.format (expected {expected}, got {actual})`. The error is at the `(` of the call, and for an element that does not fit its clause at that element. A `dyn` element fits every clause, and `%s` also looks into the elements of a list or map literal that is an element, so `'%s'.format([[1, ip('1.2.3.4')]])` is an error although a list fits `%s`. A call whose format string or list is anything else, such as a variable, is not checked, and an error in it is found at evaluation.
- **Cost.** 1 for the call, 1 for each element of the list and ⌈Lₐ/10⌉ for the length of the format string, then ⌈Lᵣ/10⌉ for the length of the result, charged as the result is built, nested lists and maps included. The units are on the page [Cost](cost.md#set-strings).
- **Limit.** The precision is at most 100; a larger one is an error as above, at compilation in a checked call, otherwise at evaluation. See [Limits](limits.md#extension-sets).
- **Difference from cel-go.** The bytes of `%s` with an invalid UTF-8 sequence give U+FFFD: cel-go keeps the raw bytes in a Go string, which a .NET string cannot hold. An unknown letter in an error is named by its code point, not by its first byte, and the order of the first two checks differs; this changes only the text of an error.

## Set `sets`, version 0

Three functions over two lists whose elements have one type, called on the namespace: `sets.contains(a, b)`, `sets.equivalent(a, b)` and `sets.intersects(a, b)`. The elements compare by equality of CEL, so `1`, `1u` and `1.0` are equal, a NaN is equal to nothing, lists and maps compare by content, and values of different kinds are unequal without an error. With elements of mixed numbers, such as the lists of a variable of the type `dyn`, the numbers compare on one line. An element that cannot be read is the value of the call. The units are on the page [Cost](cost.md#set-sets).

| Overload | Call | Result |
| :- | :- | :- |
| `list_sets_contains_list` | `sets.contains(a, b)` | `true` when every element of `b` is in `a`; the empty `b` is contained |
| `list_sets_intersects_list` | `sets.intersects(a, b)` | `true` when some element of `a` is in `b`; with an empty list it is `false` |
| `list_sets_equivalent_list` | `sets.equivalent(a, b)` | `true` when `sets.contains(a, b)` and `sets.contains(b, a)` are both `true`, so each list may repeat elements |

## Set `lists`, version 4

Functions of lists: `slice`, `flatten`, `sort`, `reverse`, `distinct` and `lists.range`, and the macro `sortBy`. The macro is expanded by the parser: `r.sortBy(v, k)` sorts the list `r` by the key that the expression `k` gives for each element `v`, so `[3, 1, 2].sortBy(e, -e)` is `[3, 2, 1]`. A result is a new list, made once for its exact size. The units of each overload are on the page [Cost](cost.md#set-lists), the limit of `lists.range` on the page [Limits](limits.md#extension-sets).

| Overload | Call | What it does |
| :- | :- | :- |
| `list_slice` | `l.slice(i, j)` | the elements from the index `i` up to the index `j`, which is excluded |
| `list_flatten` | `l.flatten()` | each element that is a list replaced by its elements, one level; an element that is not a list stays: `[[1, [2]], [3]].flatten()` is `[1, [2], 3]` |
| `list_flatten_int` | `l.flatten(n)` | the same down to the depth `n`: 0 copies the list, 1 is `flatten()`, 2 also flattens the lists inside those, and so on |
| `list_int_sort`, `list_uint_sort`, `list_double_sort`, `list_bool_sort`, `list_google.protobuf.Duration_sort`, `list_google.protobuf.Timestamp_sort`, `list_string_sort`, `list_bytes_sort` | `l.sort()` | the elements in ascending order |
| `list_<U>_sortByAssociatedKeys`, the same eight types for `<U>` | `l.@sortByAssociatedKeys(keys)` | the list ordered by a list of keys of the same size; only `sortBy` calls it, since no expression can write a name that starts with `@` |
| `lists_range` | `lists.range(n)` | `[0, 1, …, n - 1]` |
| `list_reverse` | `l.reverse()` | the elements in reverse order |
| `list_distinct` | `l.distinct()` | the first of each group of equal elements, in the order of first appearance |

The set has 22 overloads.

- **Errors** are `InvalidArgument` (3008), `invalid argument to '{function}': {detail}`, at the call. `slice` checks its arguments in this order: a negative index, `cannot slice with a negative index {i}`, naming the start if it is negative and otherwise the end; a start above the end, `cannot slice: start index {start} must be less than or equal to the end index`; an end above the size, `cannot slice: end index {end} is above the list length {n}`. A negative depth of `flatten` is `level must be non-negative`. The errors of `lists.range` are on the page [Limits](limits.md#extension-sets).
- **`flatten`** walks the lists with a stack of its own, not by recursion, so a list nested very deep in the data of the application is flattened as any other. A list that is already flat is returned as it is, and an element that is not a list stays where it is, so a list of the type `dyn` such as `dyn([1, 2]).flatten()` is `[1, 2]`.
- **`sort` and `sortBy`** order the elements of the eight types by the order of CEL: numbers by value, strings by code points, bytes by unsigned byte values, `false` before `true`, time by the instant. The sort is stable: elements with equal keys keep their order. A list of elements of different kinds, such as `[1, 'a']`, or of kinds that have no order, such as lists, is `EvalNoMatchingOverload` when the elements are not all of one of the eight types. The keys of `sortBy` have to be of one type too: `[[1], ['a']].sortBy(e, e[0])` is `EvalNoMatchingOverload`. A NaN is unordered with every number, so a list with a NaN is not sorted but is left in a fixed order that the method gives: up to 12 elements an insertion sort, above 12 a merge sort. The same list gives the same result every time; cel-go may order a list of more than 12 elements with a NaN or with equal keys differently, since its sort is not stable there.
- **`distinct`** compares each element, in order, with the ones kept so far, up to the first equal one, by the equality of CEL: `1`, `1u` and `1.0` are one element, a NaN equals nothing, so every NaN stays. The time grows with the square of the number of different elements, which the units show.
- **`sortBy` errors** are `InvalidMacroCall` (1006): the receiver has to be a list literal, an identifier, a selection, a comprehension or a call, `sortBy can only be applied to a list, identifier, comprehension, call or select expression`, at the receiver; the first argument has to be an identifier, `argument is not an identifier`, at that argument; and not the name of the accumulator of the macros, `iteration variable overwrites accumulator variable`. The macro exists only in an environment that has the set.

## Set `encoders`, version 1

Three functions called on the namespaces `base64` and `json`: `base64.decode(s)`, `base64.encode(b)` and `json.encode(v)`. The units are on the page [Cost](cost.md#set-encoders).

| Overload | Call | What it does |
| :- | :- | :- |
| `base64_decode_string` | `base64.decode(s)` | the bytes of a base64 text, first with padding and, when that fails, without it |
| `base64_encode_bytes` | `base64.encode(b)` | the text of the bytes in the standard alphabet, with `=` padding and no line breaks; empty bytes give `''` |
| `json_encode_dyn` | `json.encode(v)` | the JSON text of a value, with no spaces |

- **Decoding.** `base64.decode('aGVsbG8=')` and `base64.decode('aGVsbG8')` are both `b'hello'`. The alphabet is `A`–`Z`, `a`–`z`, `0`–`9`, `+` and `/`; the characters `\r` and `\n` are skipped wherever they stand, and nothing else is accepted — a space, a tab, a character that is not ASCII, `-` and `_` are errors. The bits that the last character leaves unused are ignored, as in Go: `aGVsbG9=` is `b'hello'` too. The padded text has its characters in groups of four, the last group with two characters and `==` or with three characters and `=`, and nothing but line breaks after the padding; the unpadded text has no `=` and a last group of two or three characters. An invalid text is `InvalidArgument` (3008), `invalid argument to 'base64.decode': illegal base64 data at input byte {i}`, where `{i}` is the offset in UTF-16 code units at which the second attempt stopped.
- **JSON.** The value is converted as protobuf converts it to a JSON value and written as Go writes it, with the characters `<`, `>` and `&` escaped. The kinds that have a JSON text are in the table; any other kind, such as a type or a value of an abstract type, is `InvalidArgument`, `invalid argument to 'json.encode': unsupported type conversion`.

| Value | Text |
| :- | :- |
| `null`, `bool` | `null`, `true`, `false` |
| `int`, `uint` | the digits when the value is from -(2⁵³ - 1) to 2⁵³ - 1, and otherwise the digits in double quotes: `1`, `"9007199254740993"`, `"18446744073709551615"` |
| `double` | the shortest digits that read back to the same value: in plain digits from 1e-6 up to but not including 1e21, otherwise `d.ddde±XX` with the exponent `e-7` for a single digit: `1`, `1.5`, `0.000001`, `100000000000000000000`, `1e+21`, `1e-7`, `-0`. A NaN and an infinity are `InvalidArgument`, `a double that is NaN or infinite cannot be encoded` |
| `string` | in double quotes; `"` and `\` escaped; `\n`, `\r`, `\t`, `\b` and `\f` as these two characters; every other character below U+0020 as `\u00xx`; `<`, `>`, `&`, U+2028 and U+2029 as `\u003c`, `\u003e`, `\u0026`, `\u2028` and `\u2029`; a lone surrogate as `\ufffd`; the rest as it is |
| `bytes` | the standard base64 text with padding, in double quotes |
| `duration`, `timestamp` | the text of `string()` in double quotes: `"1.5s"`, `"2020-01-01T00:00:00.123Z"` |
| `list` | the elements between `[` and `]`, separated by `,` |
| `map` | the entries as `"key":value` between `{` and `}`, separated by `,`, ordered by the key by code points. A key that is not a string is `InvalidArgument`, `unsupported type conversion from 'int' to string` |

The writer descends into nested lists and maps by recursion and asks for the stack before each level: a value nested beyond what the stack allows is `InsufficientStack` (9000), not a crash. An element that cannot be read, such as an element of a list of the application that throws, is the error of the call.

## Set `math`, version 3

Functions of numbers, called on the namespace `math`: `math.ceil(x)`, `math.abs(x)` and the others of the table below, and the macros `math.greatest` and `math.least`. A function takes the kinds of number that its identifier names; a value of the type `dyn` goes to the overload of its kind at run time. The units of each overload are on the page [Cost](cost.md#set-math).

**`math.greatest` and `math.least`** take one number or more and return the largest or the smallest. The macro is expanded by the parser: one argument stays one argument, two arguments stay two, and three or more become one list literal of them, so `math.greatest(1, 2, 3)` calls the list form of `math.@max`. The names `math.@max` and `math.@min` start with `@` and cannot be written in an expression; only the macros call them. The macros exist only in an environment that has the set.

- **The arguments are checked by the kind of the node, not by the type.** A number is a constant `int`, `uint` or `double`, or any expression that is not a constant and not a list, map or message literal: a variable, a selection, a call such as `dyn(5.4)`, an operator. A constant of another kind (`'a'`, `true`, `null`, `b'x'`) and a list, map or message literal are not numbers. A single argument may also be a list literal that has at least one element, every element a number. So `math.greatest(x)` and `math.greatest([dyn(1), 2.5])` are expanded, and the types are left to the overloads, while `math.greatest([])`, `math.greatest('a')`, `math.greatest([1, 'a'])` and `math.greatest(1, [2])` are errors.
- **Errors of the macros** are `InvalidMacroCall` at the part of the call that is wrong: `math.greatest() requires at least one argument` at `math` for a call without arguments, `math.greatest() invalid single argument value` at the single argument, and `math.greatest() simple literal arguments must be numeric` at the first of several arguments that is not a number. The text is `invalid call of macro 'greatest': …`; for `math.least` the words are `math.least()` and `least`.
- **Mixed kinds.** Numbers of different kinds are compared by value as the operator `<` compares them: an `int` with a `double` by the `int` rounded to a `double`, so `math.greatest(9007199254740993, 9007199254740992.0)` is the `int`. The result for two numbers of different kinds has the type `dyn` and the kind of the winner. A tie gives the first argument with its own kind: `math.greatest(1, 1.0)` is the `int` `1`, and `math.greatest(-0.0, 0.0)` is `-0`.
- **NaN.** One number is returned as it is, so `math.greatest(nan)` is NaN. A NaN among two or more numbers is `InvalidArgument`, `invalid argument to 'math.@max': NaN values cannot be ordered` (`math.@min` for the smallest), at the first comparison that meets it.

| Overload | Call | What it does |
| :- | :- | :- |
| `math_@max_double`, `math_@max_int`, `math_@max_uint`, `math_@min_double`, `math_@min_int`, `math_@min_uint` | `math.greatest(x)` | the argument itself |
| `math_@max_double_double`, `math_@max_int_int`, `math_@max_uint_uint`, and the six mixed pairs `int_uint`, `int_double`, `double_int`, `double_uint`, `uint_int`, `uint_double`; the same for `math_@min_…` | `math.greatest(x, y)` | the second argument when the first is below it, otherwise the first; for the smallest, the second when the first is above it |
| `math_@max_list_double`, `math_@max_list_int`, `math_@max_list_uint`, `math_@min_list_…` | `math.greatest(a, b, c)`, `math.greatest([a, b])` | the fold of the elements in order by the rule of two numbers. A list of one element gives its element. The elements may be of different kinds, such as those of a list of the type `dyn`; the result has the kind of the winning element. An empty list is `InvalidArgument`, `math.@max(list) argument must not be empty`; an element that is not a number is `EvalNoMatchingOverload` |
| `math_ceil_double`, `math_floor_double`, `math_trunc_double` | `math.ceil(x)` | the rounding up, down or toward zero; the sign of a zero stays: `math.ceil(-0.5)` is `-0` |
| `math_round_double` | `math.round(x)` | the nearest integer, a half away from zero: `2.5` is `3`, `-2.5` is `-3`, `0.49999999999999994` is `0` |
| `math_isInf_double`, `math_isNaN_double`, `math_isFinite_double` | `math.isNaN(x)` | whether `x` is infinite, is a NaN, or is neither |
| `math_abs_double`, `math_abs_int`, `math_abs_uint` | `math.abs(x)` | the magnitude; the least `int`, `-9223372036854775808`, is `Overflow` (3006), `integer overflow` |
| `math_sign_double`, `math_sign_int`, `math_sign_uint` | `math.sign(x)` | `-1`, `0` or `1` of the kind of `x`, and NaN for a NaN; `-0.0` gives `0.0` |
| `math_sqrt_double`, `math_sqrt_int`, `math_sqrt_uint` | `math.sqrt(x)` | the square root as a `double`; a negative number gives NaN, not an error |
| `math_bitAnd_int_int`, `math_bitAnd_uint_uint`, `math_bitOr_…`, `math_bitXor_…` | `math.bitAnd(a, b)` | the bit operation on two numbers of one kind |
| `math_bitNot_int_int`, `math_bitNot_uint_uint` | `math.bitNot(a)` | the complement of the bits |
| `math_bitShiftLeft_int_int`, `math_bitShiftLeft_uint_int` | `math.bitShiftLeft(a, n)` | `a` shifted left by `n` bits, as its 64 bits, so `math.bitShiftLeft(1, 63)` is `-9223372036854775808`; `n` of 64 and above gives 0 |
| `math_bitShiftRight_int_int`, `math_bitShiftRight_uint_int` | `math.bitShiftRight(a, n)` | the shift to the right, which is logical for an `int` too: `math.bitShiftRight(-1, 1)` is `9223372036854775807`; `n` of 64 and above gives 0 |

A negative `n` of a shift is `InvalidArgument` (3008), `math.bitShiftLeft() negative offset: -1`, at the call. The set has 58 overloads, 30 of them of `math.@max` and `math.@min`.

## Set `bindings`, version 1

The macro `cel.bind(name, value, result)` gives the value of the expression `value` the name `name` in the expression `result`. The value is evaluated once, before the result, and every read of the name takes it, so a value that is costly or that fails is paid for, or fails, once. A value that the result does not read does not make the result fail: `cel.bind(v, 1 / 0, 7)` is `7`. The name hides every variable and every outer name of the same spelling in the result, and an inner `cel.bind` hides an outer one.

```text
cel.bind(area, width * height, area / 2 + area)
```

- **The first argument has to be a simple identifier.** Otherwise the call is `InvalidMacroCall`, `invalid call of macro 'bind': cel.bind() variable names must be simple identifiers`, at that argument: `cel.bind(1, 2, 3)` at the `1`, `cel.bind(a.b, 2, 3)` at the dot of the selection.
- **A call with another number of arguments, or on another target, is not the macro.** `cel.bind(x, 1)` is a call of a function that no set declares, and type checking reports it. The macro exists only in an environment that has the set.
- **`cel.@block`.** The set also has the function `cel.@block(slots, result)` with the overload `cel_block_list`, `cel.@block(list(dyn), T) -> T`, which takes a list literal of slot expressions and the result. The name starts with `@`, so no expression can write it; it is for trees that the library builds itself: the library has no public way to build a syntax tree. In the slots and the result, `@index0`, `@index1` and so on are the values of the slots. A slot is evaluated the first time it is read and not again; a slot that nothing reads is not evaluated, and its error does not happen. A slot that reads itself, directly or through other slots, goes on to the slot of the same number of the next enclosing block that has it, as cel-go does, and the read is `Unbound` only when no block is left. The names `@index0`… are not declared variables, so type checking rejects a tree that reads them as an undeclared reference. A first argument that is not a list literal is refused: by type checking as `NoMatchingOverload`, and in an evaluation without type checking as `EvalNoMatchingOverload`.

The units are on the page [Cost](cost.md#set-bindings).

## Set `two-var-comprehensions`, version 0

Macros whose body sees two names for each step of the range: the index and the element of a list, the key and the value of a map. The macros `all`, `exists`, `existsOne`, `exists_one`, `transformList`, `transformMap` and `transformMapEntry` take the names of the two variables first, so `all(i, v, p)` has three arguments and the transformations three or four. The standard macros `all`, `exists`, `exists_one`, `map` and `filter` take two or three arguments and are not changed by the set: the number of arguments is part of what tells a macro from another, so `xs.all(v, p)` stays the standard macro in an environment that has the set. The macros exist only in an environment that has the set; without it `xs.all(i, v, p)` is a call of a function that no set declares.

```text
{'a': 1, 'b': 2}.transformMap(k, v, v * 10)
```

| Macro | Arguments | Result |
| :- | :- | :- |
| `r.all(i, v, p)` | the predicate `p` | `true` when `p` is true for every step, as `&&` over the steps: the loop stops at the first `false`, and an error value of one step is absorbed when another step is `false` |
| `r.exists(i, v, p)` | the predicate `p` | `true` when `p` is true for some step, as `\|\|` over the steps: the loop stops at the first `true` |
| `r.existsOne(i, v, p)`, `r.exists_one(i, v, p)` | the predicate `p` | `true` when `p` is true for exactly one step; no short circuit, every step runs, and an error value of a predicate is the result |
| `r.transformList(i, v, t)`, `r.transformList(i, v, p, t)` | the transformation `t`, and a filter `p` | the list of the values of `t`, one for each step, in the order of the range; with a filter, only for the steps where `p` is true |
| `r.transformMap(i, v, t)`, `r.transformMap(i, v, p, t)` | the same | the map from the first variable to the value of `t`: for a list the index, for a map the key |
| `r.transformMapEntry(i, v, t)`, `r.transformMapEntry(i, v, p, t)` | the same, `t` is a map | the map that holds the pairs of the maps that `t` gives for the steps, in the order of the steps and of each map |

- **The range.** A list or a map, or a value of type `dyn` that is one at run time. The first variable is the index of a list, an `int` from 0, or the key of a map; the second is the element or the value. A map is visited in the order in which it lists its entries — for a map literal the order of the text, for a JSON object the order of its properties — and the value of an entry is read with its key. A range that is neither is `InvalidComprehensionRange` (2005) at the range in type checking when its type is known, and `EvalInvalidComprehensionRange` (3026) at run time.
- **The types.** Type checking gives the first variable the type `int` and the second the element type for a list range, and the key type and the value type for a map range; a range of type `dyn` gives `dyn` to both. The names hide the variables and the names of the environment in the body, and an inner macro hides an outer one. `ints.transformList(i, v, v * 2)` has the type `list(int)`, `counts.transformMap(k, v, v)` the type `map(string, int)`.
- **Errors in the names.** The first two arguments have to be simple identifiers, different from each other, and not the name of the accumulator. Otherwise the call is `InvalidMacroCall` (1006), once for the call: `argument must be a simple name` at the argument that is not a name, `duplicate variable name: i` at the second name, and `iteration variable overwrites accumulator variable` at the name.
- **An error value stays.** An error value of the predicate or of the transformation is the value of the step, and the later steps do not undo it: the result is that error, at the place where it happened. A transformation that is a map with a key that an earlier step put in is `DuplicateMapKey` (3010) at the call of the macro, `duplicate map key: 1`; keys are equal by CEL equality, so `1u` after `1` is a repeat.
- **`cel.@mapInsert`.** The set has the function `cel.@mapInsert` with two overloads, `@mapInsert_map_key_value`, `cel.@mapInsert(map(K, V), K, V) -> map(K, V)`, and `@mapInsert_map_map`, `cel.@mapInsert(map(K, V), map(K, V)) -> map(K, V)`, which `transformMap` and `transformMapEntry` expand to. The name starts with `@`, so no expression can write it; it is for trees that the library builds itself: the library has no public way to build a syntax tree. Each overload gives a new map with the pair or the pairs added; a key that is no `int`, `uint`, `bool` or `string` is `UnsupportedMapKeyType` (3027), and a key that is already in is `DuplicateMapKey`. In a loop of a macro the library puts the pairs into the map that it is building, without copying it at each step.

The units are on the page [Cost](cost.md#set-two-var-comprehensions).

## Set `network`, version 1

Two abstract types, `net.IP` and `net.CIDR`, and 21 functions over them, as in the `network` extension of cel-go. A value of an abstract type is made and read only by the functions of its set: the library has no `CelValue.From…` or `TryGet…` method for it, `CelValue.Kind` is `CelValueKind.Abstract`, and an application receives the value as a `CelValue` that it can pass back as a binding. The names `net.IP` and `net.CIDR` are identifiers of type values, so `type(ip('1.2.3.4')) == net.IP` is `true`, and a variable or a function of the application declares them by name: `CelType.ObjectNamed("net.IP")`, or `"type": "net.IP"` in a description in JSON. Without the set the name is `TypeNotDeclared`, and the message names the set to add. The units are on the page [Cost](cost.md#set-network).

```text
cidr('10.0.0.0/8').containsIP(ip('10.20.30.40'))
```

| Overload | Call | Result |
| :- | :- | :- |
| `string_to_ip` | `ip(s)` | the address that `s` is; `InvalidArgument` for a text that is no address |
| `string_to_cidr` | `cidr(s)` | the prefix that `s` is, with the address as written and its host bits; `InvalidArgument` for a text that is no prefix |
| `cidr_ip` | `c.ip()` | the address of the prefix as written: `cidr('192.168.1.5/24').ip()` is `192.168.1.5` |
| `ip_to_string`, `cidr_to_string` | `string(a)` | the text form of the address or the prefix |
| `is_ip`, `is_cidr` | `isIP(s)`, `isCIDR(s)` | `true` when the text parses by the rules below, never an error; `isCIDR('10.0.0.1/8')` is `true` |
| `ip_is_canonical` | `ip.isCanonical(s)` | `true` when `s` is equal to `string(ip(s))`: `ip.isCanonical('2001:db8::1')` is `true`, `'2001:DB8::1'`, `'1:0:0:0:0:0:0:1'` and `'::0'` are `false`; `InvalidArgument` for a text that is no address |
| `ip_family` | `a.family()` | 4 or 6 |
| `ip_is_loopback` | `a.isLoopback()` | IPv4 with the first octet 127; IPv6 `::1` |
| `ip_is_unspecified` | `a.isUnspecified()` | `0.0.0.0` or `::` |
| `ip_is_link_local_unicast` | `a.isLinkLocalUnicast()` | IPv4 `169.254.0.0/16`; IPv6 `fe80::/10` |
| `ip_is_link_local_multicast` | `a.isLinkLocalMulticast()` | IPv4 `224.0.0.0/24`; IPv6 `ff02::`, `ff12::`, `ff32::` and the other addresses whose first 16 bits are `ff02` once the scope digit is ignored; `ff01::` and `ff0e::` are not |
| `ip_is_global_unicast` | `a.isGlobalUnicast()` | `false` for `0.0.0.0`, `255.255.255.255`, the unspecified and loopback addresses, multicast addresses (IPv4 `224` to `239`, IPv6 starting `ff`) and link-local unicast addresses; `true` otherwise, so `10.1.1.1`, `fc00::1` and `2001:db8::1` are `true` |
| `cidr_prefix_length` | `c.prefixLength()` | the length of the prefix |
| `cidr_is_mask` | `c.isMask()` | `true` when the address has no host bits |
| `cidr_masked` | `c.masked()` | the prefix with the host bits of its address cleared, the same length |
| `cidr_contains_ip_ip`, `cidr_contains_ip_string` | `c.containsIP(a)`, `c.containsIP(s)` | `true` when the address is of the family of the prefix and its first bits, as many as the length, equal those of the prefix; an address of the other family is `false`, not an error; a string that is no address is `InvalidArgument` |
| `cidr_contains_cidr`, `cidr_contains_cidr_string` | `c.containsCIDR(d)`, `c.containsCIDR(s)` | `true` when the other prefix is of the same family, its length is not smaller and its first bits, as many as the length of `c`, equal those of `c`; the host bits of both are ignored; a string that is no prefix is `InvalidArgument` |

**Parsing is strict.** The text is read by the rules of `net/netip` of Go; a zone and an IPv4-mapped address are errors, which Go accepts:

- An IPv4 address is four decimal fields separated by dots, each of 1 to 3 digits, at most 255, with no leading zero unless the field is `0`: `1.2.3`, `01.2.3.4`, `1.2.3.256`, `1.2.3.4.`, `0x1.2.3.4` and ` 1.2.3.4` are not addresses.
- An IPv6 address is groups of 1 to 4 hexadecimal digits of either case, with at most one `::` that stands for at least one group of zeros, and the last 32 bits may be written as a dotted IPv4. `::` is the unspecified address, `1:2:3:4:5:6:7::` is valid and `1:2:3:4:5:6:7:8::` is not; `1::2::3`, `12345::1`, `:1`, `1:` and `[::1]` are not addresses. The IPv4-compatible `::1.2.3.4` is valid and is an IPv6 address.
- An IPv4-mapped IPv6 address — the first 80 bits zero and the next 16 ones, in any written form: `::ffff:1.2.3.4`, `::ffff:c0a8:1` — is refused. So is a zone, anything after a `%`, whether the zone is empty or not.
- A prefix is split at the last `/`: the part before it is an address by the rules above, the part after is one or more digits with no sign and no leading zero unless it is `0`, at most 32 for IPv4 and 128 for IPv6. A prefix keeps the address as written, with its host bits: `10.0.0.1/8` is valid. `10.0.0.0/33`, `10.0.0.0/08`, `10.0.0.0/+8`, `10.0.0.0/`, `10.0.0.0` and `::ffff:1.2.3.4/96` are not prefixes.
- A character above U+007F is an error wherever it stands.

An error of a parse is `InvalidArgument` (3008) at the call, `invalid argument to 'ip': IP Address "1.2.3" parse error during conversion from string: IPv4 address too short`; the reason is that of Go, and the refusals read `IPv4-mapped IPv6 address "…" is not allowed` and `IP Address "…" with zone value is not allowed` (`CIDR` for a prefix). A call whose argument is a constant, such as `ip('1.2.3')`, fails at evaluation, not at compilation: this library does not check the constant arguments of `ip` and `cidr` when the expression is compiled, as cel-go does.

**Text forms.** `string` writes an address as `netip` does: IPv4 as four decimal fields; IPv6 in lower case with no leading zeros in a group, the first of the longest runs of two or more zero groups written as `::`, and a single zero group left alone: `2001:0db8:0:0:0:0:0:1` is `2001:db8::1`, `1:0:0:2:0:0:0:3` is `1:0:0:2::3`, `1:2:3:4:5:6:7:0` is itself and `::1.2.3.4` is `::102:304`. A prefix is written as its address as it was parsed, `/` and the length: `string(cidr('10.0.0.1/8'))` is `10.0.0.1/8`.

**Equality.** Two `net.IP` values are equal when they are of one family and have the same bits, so `ip('::1.2.3.4')` and `ip('1.2.3.4')` are different. Two `net.CIDR` values are equal when they have the same family, the same address as written and the same length: `cidr('10.0.0.1/8') == cidr('10.0.0.0/8')` is `false` and `cidr('10.0.0.1/8').masked() == cidr('10.0.0.0/8')` is `true`. A value of an abstract type equals no value of another kind, and `null` too, without an error. The types have no order, so `<` on them is refused by the type check; they are no map keys, `UnsupportedMapKeyType`; and `json.encode` and `%s` of `format` do not take them.

## Set `protos`, version 0

Two macros, `proto.getExt` and `proto.hasExt`, that read and test the extension fields of a protobuf message of proto2. The set has no function, overload or abstract type and needs no other set.

| Call | Is the same as |
| :- | :- |
| `proto.getExt(m, pkg.name)` | the selection of the field `pkg.name` of `m` |
| `proto.hasExt(m, pkg.name)` | `has(m.pkg.name)`, with the name of the extension as the name of the field |

```text
proto.getExt(m, cel.expr.conformance.proto2.int32_ext) + 1
proto.hasExt(m, cel.expr.conformance.proto2.nested_ext)
```

- **The second argument** is the full name of the extension written as a chain of identifiers with at least one dot: `cel.expr.conformance.proto2.int32_ext`. A single identifier, a constant, a call, an operator, an index, a list or a `has()` is the parse error `InvalidMacroCall` (1006), `invalid call of macro 'getExt': invalid extension field`, at the argument; the call is not expanded and parsing goes on. A leading dot stays in the name, which then names no field.
- **The field comes from a type source.** The macros only name the field; the extension is a field of the description of its message with the full name of the extension for its name, which the package `Great.Parts.Cel.Protobuf` makes for the extensions that the application registers. A message type without that field is the type checking error `NoSuchField` at the call, and a value of the type `dyn` without it is `EvalNoSuchField`.
- **Without the set** the call is an ordinary call: type checking reports `proto` and `getExt` as undeclared. So it is for a call with a number of arguments other than two and for a target that is not the identifier `proto`.
- **The version** is 0. `CelExtensions.Protos` does not check it: another version is the build error `UnknownExtensionVersion`, `extension 'protos' has no version 1; available versions: 0`. In an environment description in JSON the set is `{"name": "protos", "version": "0"}`.

A macro costs what its expansion costs, see [Cost](cost.md#set-protos).

## Set `optional`, version 2

The type `optional_type(T)`, the functions that make, test and unwrap its values, and the macros `optMap` and `optFlatMap`. The set also turns on the optional syntax, `x.?f`, `x[?k]`, `[?e]`, `{?k: v}` and `T{?f: v}`; without the set that syntax is the parse error `SyntaxNotEnabled` (1007). In .NET a value of the type is `CelOptional<T>`, see [Type mapping](type-mapping.md#optional-values). The units are on the page [Cost](cost.md#set-optional).

An optional either holds a value, which can be `null`, or is empty. `optional.none()` and `optional.of(null)` are different values: the first has no value and the second has the value `null`. Two optionals are equal when both are empty, or both hold values that are equal by the equality of CEL, so `optional.of(1) == optional.of(1.0)` is `true`; an optional equals no value of another kind, `null` included, and `optional.of(null) == null` is `false`. The name of the type is `optional_type`, and `type(optional.of(1)) == optional_type` is `true`: the parameter of the type is not part of the value of the function `type`. An optional is no key of a map, `UnsupportedMapKeyType` (3027), and no operand of `string()`, `json.encode`, an ordering or a clause of `format`. The set declares the type: a declaration of an environment that uses `optional_type` without the set is the build error `TypeNotDeclared` (4008), `…; add the extension set 'optional'`.

| Overload | Call | What it does |
| :- | :- | :- |
| `optional_of` | `optional.of(v)` | an optional that holds `v`, `null` included |
| `optional_ofNonZeroValue` | `optional.ofNonZeroValue(v)` | the empty optional when `v` is a zero value, otherwise `optional.of(v)` |
| `optional_none` | `optional.none()` | the empty optional |
| `optional_value` | `o.value()` | the held value; `OptionalHasNoValue` (3016), `optional value is empty`, at the call for an empty optional |
| `optional_hasValue` | `o.hasValue()` | whether `o` holds a value |
| `optional_or_optional` | `o.or(p)` | `o` when it holds a value, otherwise `p` |
| `optional_orValue_value` | `o.orValue(v)` | the held value when `o` holds one, otherwise `v` |
| `list_first` | `l.first()` | the optional of the first element, or the empty optional for an empty list |
| `list_last` | `l.last()` | the optional of the last element, or the empty optional for an empty list |
| `optional_unwrap` | `optional.unwrap(l)` | the held values of the optionals of the list `l` that hold one, in order, in a new list |
| `optional_unwrapOpt` | `l.unwrapOpt()` | the same as `optional.unwrap(l)` |

```text
optional.of(5).value()                        // 5
optional.none().orValue(7)                    // 7
optional.none().or(optional.of(2)).value()    // 2
[1, 2, 3].last().orValue(0)                   // 3
optional.unwrap([optional.of(1), optional.none(), optional.of(3)])    // [1, 3]
```

- **`or` and `orValue` evaluate their argument only when the receiver is empty.** `optional.of(1).orValue(1 / 0)` is `1`, and `optional.none().orValue(1 / 0)` is `DivisionByZero`. An error value of a part is the value of the call: the receiver always, the argument when it is evaluated. A receiver that is not an optional, which only a value of type `dyn` can be, is `EvalNoMatchingOverload`, `no such overload: or(int)`, and the argument is not evaluated.
- **`or` takes only an optional.** When the receiver is empty and the argument is not an optional, which only a value of type `dyn` or `null` can be, the call is `EvalNoMatchingOverload`, `no such overload: or(optional_type, int)`; cel-go gives the argument whatever it is, so `optional.none().or(dyn(1))` is `1` there. `orValue` takes an argument of any kind, as in cel-go.
- **Zero values.** `optional.ofNonZeroValue(v)` is the empty optional exactly when `v` is `null`; `false`; the `int` 0 and the `uint` 0; the `double` 0.0 and -0.0, but not a NaN; the empty string; the empty `bytes`; the empty list; the empty map; the `duration` of 0; the `timestamp` of 0001-01-01T00:00:00Z, which is not the epoch; or an object, such as a protobuf message, none of whose fields is set. A value of any other kind is not zero, the optionals and the type values among them. The call reads the fields of an object in the order of the description and stops at the first field that is set.
- **`first` and `last`** keep what the element is: `[null].first()` holds `null`, and an element that is an error value, such as an element of a list of the application that cannot be read, is the value of the call.
- **`optional.unwrap` and `unwrapOpt`** keep a held `null` and give a list of the held values, never of optionals. An element that is not an optional, which only a list of type `dyn` can have, is `EvalNoMatchingOverload`.
- **Macros.** `o.optMap(v, e)` is `optional.of(e)` with `v` bound to the held value of `o`, and the empty optional when `o` is empty: `optional.of(1).optMap(y, y + 1)` is `optional.of(2)`. `o.optFlatMap(v, e)` is the same for an `e` that is an optional itself, and gives `e` without wrapping it again. The body `e` is not evaluated for an empty optional, and `o` is evaluated once. The first argument has to be an identifier; otherwise the parse error is `InvalidMacroCall` (1006), `invalid call of macro 'optMap': optMap() variable name must be a simple identifier`, at that argument, and for `optFlatMap` `optFlatMap() variable name must be a simple identifier`. A call with a number of arguments other than two is not a macro and stays a call, which type checking reports. The macros exist only in an environment that has the set.
- **The version.** The set has the one version 2, as in cel-go with `OptionalTypesVersion(2)`: `CelExtensions.Optional(1)` is the build error `UnknownExtensionVersion`, `extension 'optional' has no version 1; available versions: 2`. In an environment description in JSON the set is `{"name": "optional", "version": "2"}`, and the type is written `optional_type<int>`.

## Set `decimal`, version 1

The type `decimal`, a decimal128 number of 34 significant digits, with `+`, `-`, `*`, `/`, the negation, the four orderings and nine conversions: 18 overloads. The set exists only on `net11.0`, whose `System.Numerics.Decimal128` it uses; on `net10.0` there is no `decimal`, no set and no member of the public API for it. The whole contract, the literal `12.34m` and its option, the grammar and the text, equality and order are on the page [Decimal](decimal.md); the units are on the page [Cost](cost.md#set-decimal).

- **Not in a description.** The set is added in code, `CelExtensions.Decimal(1)`. It has no name in the catalog of an [environment description in JSON](environment-json.md): the name `decimal` there is `InvalidConfiguration` (4009), `'decimal' is not the name of an extension set of the library`.
- **The type.** The set declares the type identifier `decimal`; without the set `decimal('1')` and `type(x) == decimal` are `UndeclaredReference` (2000).
- **The version.** The set has the one version 1: `CelExtensions.Decimal(2)` is the build error `UnknownExtensionVersion`, `extension 'decimal' has no version 2; available versions: 1`. The same instance added twice, or `Decimal(1)` twice, is one set. The set needs no other set and brings no macro, no variable and no syntax; the literal is enabled by `CelEnvironmentBuilder.EnableDecimalLiterals()`, which needs the set (`MissingExtensionDependency`, 4005).

| Overload | Call | Errors |
| :- | :- | :- |
| `add_decimal`, `subtract_decimal`, `multiply_decimal` | `a + b`, `a - b`, `a * b` | `Overflow` (3006) |
| `divide_decimal` | `a / b` | `DivisionByZero` (3004), `Overflow` |
| `negate_decimal` | `-a` | none |
| `less_decimal`, `less_equals_decimal`, `greater_decimal`, `greater_equals_decimal` | `a < b`, `a <= b`, `a > b`, `a >= b` | none |
| `int64_to_decimal`, `uint64_to_decimal` | `decimal(i)` | none |
| `double_to_decimal` | `decimal(d)` | `ConversionRange` (3007) for a NaN and an infinity |
| `string_to_decimal` | `decimal(s)` | `InvalidArgument` (3008), `ConversionRange` |
| `decimal_to_decimal` | `decimal(x)` | none |
| `decimal_to_int64`, `decimal_to_uint64`, `decimal_to_double` | `int(x)`, `uint(x)`, `double(x)` | `ConversionRange` |
| `decimal_to_string` | `string(x)` | none |

```text
decimal('1.50') + 1.5m                // 3.00
decimal(1) / decimal(3)               // 0.3333333333333333333333333333333333
int(decimal('9.99'))                  // 9
```
