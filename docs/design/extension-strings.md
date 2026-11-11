# Extension set `strings`, version 5

The functions of `strings` except `format`, which has [its own page](extension-format.md). Identifiers, signatures and the versions in which each overload appeared are on the `docs/extensions.md` page; the costs are the rows of [library](library.md#cost-of-the-extension-sets); the set, its catalog and the build checks are on [extensions](extensions.md). This page says what each overload does at the edges, which errors it gives and how it uses the search of [library](library.md#substring-search).

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Library/Extensions/Strings/StringsExtension.cs` | the table of the set: 19 overloads in the order of cel-go, the factory |
| `src/Great.Parts.Cel/Library/Extensions/Strings/CodePoints.cs` | length and the offset of an index, over UTF-16 |
| `src/Great.Parts.Cel/Library/Extensions/Strings/StringFunctions.cs` | the implementations of the overloads of this page |
| `src/Great.Parts.Cel/Library/Extensions/Strings/ResultBuilder.cs` | the builder of a result that charges as it grows |
| `src/Great.Parts.Cel/Library/SubstringSearch.cs` | the search, extended as below |

## Conventions

- **Units are code points.** Every index, offset and length of this page counts code points: a valid surrogate pair is one, a lone surrogate — which only a string of the application can hold — is one too, as `size` counts it ([library](library.md)). `CodePoints` converts a code point index to an offset in UTF-16 units with one pass over the string; each call of a function pays that pass in the term ⌈Lₐ/10⌉ of its cost. An index is an `int` of CEL, a `long`: it is compared with the length as a `long` and never narrowed first, so `charAt(9223372036854775807)` is an index error, not an overflow.
- **Index errors** are `IndexOutOfRange` (3003) at the call, the text `index out of range: {index}` as the cel-spec cases of `string_ext` have it; the place is the call, the `(` after the name. An argument error is `InvalidArgument` (3008), `invalid argument to '{function}': {detail}`. The formula of the call is charged in full before an index error is raised: the bounds are in code points, whose check passes over the string, so no argument of these functions is invalid without work on the data (the rule for invalid arguments in [library](library.md#cost-of-the-extension-sets)).
- **Results are built as charged.** A result whose length is known before it is created — `charAt`, `substring`, `trim`, `lowerAscii`, `upperAscii`, `reverse`, `quote`, `join` — charges ⌈Lᵣ/10⌉ and then creates the string once with its exact length. `replace` and `split` charge as they build: `ResultBuilder` is an internal ref struct over an array rented from `ArrayPool<char>.Shared`; before it appends `l` units it charges ⌈(T + l)/10⌉ − ⌈T/10⌉ for the running length T, so the charges add up to ⌈Lᵣ/10⌉ and a limit that is exceeded stops the build before the memory is spent; `Charge(units)` charges for units that are not written yet; `WriteCharged(units)` then makes room for units already charged and returns them to be filled in, which lets `format` write a number or a text in place; `Remove(start, count)` deletes written units and keeps what was charged: `format` writes the texts of a map in the order of the map, writes them again in key order into units charged ahead, and removes the first texts, so the units charged are those of the result and what was charged is not returned; `split` also charges 1 for each part before it adds it. The pooled array is returned when the call ends, so the steady state allocates the result string or list alone, and the first rental, which allocates, is not charged. A result that equals the receiver — `replace` with no match, `lowerAscii` of a string without capitals — may be the receiver itself, but the charge is that of a copy: the cost follows the data, not the optimization.
- **No culture, no .NET search.** `string.IndexOf`, `Contains`, `Replace`, `Split` with a string pattern, `ToLower`, `ToUpper` and `Trim` are not used: the first four are not linear in the worst case, the others depend on the culture or on a table of their own. The functions below use `CodePoints`, `SubstringSearch` and their own tables.

## Functions

| Overload | What it does | Errors |
| :- | :- | :- |
| `string_char_at_int` | the code point at the index as a string; the index equal to the length gives `""` | index outside `[0, length]` — 3003 |
| `string_index_of_string` | the index of the first occurrence of the argument; the empty argument is found at 0 | — |
| `string_index_of_string_int` | the first occurrence at or after the offset, as the index in code points of the whole string. The empty argument is found at the offset. At the offset equal to the length a non-empty argument is not found (`-1`) | offset below 0 or above the length — 3003; this is the record of known differences `string_index_of_string_int`: cel-go gives `-1` above the length |
| `string_last_index_of_string` | the index of the last occurrence; the empty argument gives the length | — |
| `string_last_index_of_string_int` | the last occurrence that starts at or before the offset. The empty argument gives the offset. At the offset equal to the length a non-empty argument is not found (`-1`); `'abc'.lastIndexOf('c', 3)` is `-1` | offset below 0 or above the length — 3003; the record `string_last_index_of_string_int`, as above |
| `string_lower_ascii`, `string_upper_ascii` | the letters `A`–`Z` or `a`–`z` are changed; every other code point stays, so `'É'` and `'ı'` do not change | — |
| `string_replace_string_string` | every non-overlapping occurrence of the old string, scanned from the left, replaced; with an empty old string the new one is inserted before each code point and at the end: `'abc'.replace('', '-')` is `-a-b-c-` and `''.replace('', 'x')` is `x` | — |
| `string_replace_string_string_int` | as above with at most `n` replacements: below 0 all of them, 0 none, so the result is the receiver; with an empty old string the first `n` insertion points | — |
| `string_split_string` | the parts between the occurrences of the separator, from the left, without limit; `''.split(',')` is `['']` (one empty part), `',a,'.split(',')` is `['', 'a', '']`; with an empty separator one part for each code point, and no part for an empty string: `''.split('')` is `[]` | — |
| `string_split_string_int` | at most `n` parts, the last one holding the rest unsplit: `'a,b,c'.split(',', 2)` is `['a', 'b,c']`; below 0 all parts, 0 gives `[]`; with an empty separator `'héé'.split('', 2)` is `['h', 'éé']` | — |
| `string_substring_int` | from the index to the end; the index equal to the length gives `""` | index outside `[0, length]` — 3003 |
| `string_substring_int_int` | from the first index to the second, the end excluded. Checked in this order: start above end — 3008, `invalid substring range: start index {start} must be less than or equal to the end index`; start outside `[0, length]` — 3003; end outside `[0, length]` — 3003. So `'ab'.substring(2, 1)` is 3008 and `'ab'.substring(-1, 5)` is 3003 for the start | as listed |
| `string_trim` | the leading and trailing White_Space code points removed: `U+0009`–`U+000D`, `U+0020`, `U+0085`, `U+00A0`, `U+1680`, `U+2000`–`U+200A`, `U+2028`, `U+2029`, `U+202F`, `U+205F`, `U+3000`; the table is the code's own, as in Go's `unicode.IsSpace` | — |
| `list_join`, `list_join_string` | the elements joined by the separator, by none for `list_join`; the empty list gives `""` | an element that is not a string, which only a `dyn` list can hold, is `EvalNoMatchingOverload`: the argument is marked and checked at depth ([evaluation](evaluation.md#calls)) |
| `strings_quote` | the string in double quotes with `\a`, `\b`, `\f`, `\n`, `\r`, `\t`, `\v`, `\\` and `\"` escaped; every other code point stays as it is, `U+0000`, `U+007F` and `U+2028` included; a lone surrogate becomes `U+FFFD`. Called as `strings.quote(s)`, the qualified function name | — |
| `string_reverse` | the code points in reverse order; a surrogate pair stays a pair, a lone surrogate stays itself | — |

The examples above were checked by running cel-go v0.32.0.

## Search

The overloads that look for a string — the four of `indexOf` and `lastIndexOf`, `replace` and `split` — use `SubstringSearch`, the search of [library](library.md#substring-search), over UTF-16 units. A match counts only at a code point boundary: for a valid string the units give the same matches as the code points, and a pattern that starts with a lone low surrogate or ends with a lone high one, which no literal can hold, is rejected where it would cut a pair, and the search goes on one unit further with a new call, so the linear bound below holds for every input except those few.

`SubstringSearch` gets two additions, both without allocation beyond the array of the prefix function that the existing code already allows:

- a **prepared pattern**, the prefix function computed once for a call — at most 2m steps, on the stack up to 256 units, otherwise in an array of m `int`s — with `IndexOf(text, from)`, which continues from a given offset, so that `replace` and `split` find all occurrences with one preparation and the steps of the scans add up to at most 3n + 2m;
- `LastIndexOf(text, end)`, the last occurrence that ends at or before `end`: the same algorithm over the text and the pattern read from the end, with the prefix function of the reversed pattern, still at most 2m steps to prepare and 3n to scan.

Each comparison of two units is a step; the counter is the parameter `ref long steps` of the library functions that the property test calls ([tests](tests.md#substring-search-of-the-extension-sets)). The bound for one call of any of the six overloads is `steps ≤ 4 · (n + m)`, n the length of the receiver and m the length of the pattern in UTF-16 units. An empty pattern is not searched: `replace` and `split` step over the code points, `indexOf` and `lastIndexOf` answer from the lengths, and the count is 0.

## Differences from cel-go

- `indexOf` and `lastIndexOf` with an offset above the length are an error, as the cel-spec cases require and cel-go does not give; two records of the registry of known differences hold this. At the offset equal to the length both agree.
- A string of the application can hold a lone surrogate, which cel-go cannot (a Go string is UTF-8): the functions treat it as one code point and do not repair it, except in `quote`.

Related pages: [extensions](extensions.md), [extension-format](extension-format.md), [library](library.md), [evaluation](evaluation.md).
