# Regex syntax: reading a pattern

`Great.Parts.Cel.Library.Regex` turns the pattern of `matches` into a program that its own engine runs ([regex-engine](regex-engine.md)). This page is the first half: reading the text of a pattern into a tree, with the limits and the errors. The classes of Unicode that it reads come from [unicode-tables](unicode-tables.md); the functions `matches` and `matches_string`, their errors and costs are on [library](library.md).

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Library/Regex/PatternParser.cs` | the parser: the loop over the text, the stack of the parse, the errors |
| `src/Great.Parts.Cel/Library/Regex/PatternTree.cs` | the tree in arrays: kinds, children, counts, heights, sizes |
| `src/Great.Parts.Cel/Library/Regex/ClassBuilder.cs` | the sorted, merged list of ranges of a class: items, negation, folding |
| `src/Great.Parts.Cel/Library/Regex/PatternSize.cs` | the size m of the program from the tree, before any repeat is expanded |
| `src/Great.Parts.Cel/Library/Regex/RegexError.cs` | the error of a pattern: the offset and the reason |

The files are internal. Their names are the names the tests use; the split of the code among them is the developer's.

## The reference

The reference is `regexp/syntax` of Go 1.27.3, the version the oracle of the differential run pins (`eng/oracle/go.mod`), with the flags `syntax.Perl`: `ClassNL`, `OneLine`, `PerlX` and `UnicodeGroups`. Where this page is silent, Go decides, and the parser is a port of `parse.go` step by step: the stack of the parse, the merging of literals and of classes, the factoring of alternatives, the way a repetition takes its operand. That is not only a convenience: the height of the tree and the size of the program are counted on the tree that Go builds, so the limits give the same verdict at their edges. The sources of Go are in the image `golang:1.27.3` under `/usr/local/go/src/regexp/syntax` (`parse.go`, `simplify.go`, `compile.go`, `perl_groups.go`) and `/usr/local/go/src/unicode`.

This page lists what differs from Go and what the library fixes where the standard of the language is silent:

| Where | Go | Here |
| :- | :- | :- |
| input | UTF-8 bytes | a UTF-16 `string`, read by code points; offsets are in UTF-16 code units |
| a lone surrogate in the pattern | `invalid UTF-8` | an error, reason `invalid UTF-16`, offset of the surrogate |
| the reason of an error | the code and the quoted fragment | the code of Go as it is; for `unsupported Perl syntax` the fragment follows, as `unsupported Perl syntax: (?=`; see "Errors" |
| `\C` | `invalid escape sequence` | the same error: a code point is not a byte |
| the limits | `maxHeight` 1000, `maxSize` of about 3.3 million instructions, `maxRunes`, repeat count 1000 | height 1000, count 1000 and the product of counts 1000 as Go; the size is the limit of this library, 100 000 ([limits](../limits.md)); `maxRunes` has no counterpart: the limit of the size and the cost of the parse bound it |
| recursion | the parser has none; `calcHeight`, `calcSize`, `repeatIsValid`, `Simplify` and `Compile` do | none anywhere: height, product of counts and size are kept in the nodes as they are built, and the expansion is a loop over an explicit stack |
| captures | numbered and named groups, stored | a group is read, a name is checked, and nothing else is kept: the answer is only whether there is a match. A group is still a node of the tree, since it adds to the height |
| `String()`, `Prefix`, `MinInputLen` and the other methods of `Regexp` | present | not needed |

## Reading the text

The parser reads the pattern as a `ReadOnlySpan<char>` and decodes a code point at each step: a high surrogate followed by a low one is one code point; any other surrogate is an error. It never copies the pattern. The position of an error is the offset in UTF-16 code units of the fragment that Go names in `Error.Expr` — the text from its first character on; for an error about the whole pattern the offset is 0. The offsets in the table of "Errors" come from this rule and are the same for a constant pattern, where they are counted in the value of the literal after the escapes of CEL are read (`'\U0001F600a**'` has its fault at 3), and for a pattern from data.

## What is accepted

Everything that `syntax.Perl` accepts, which is the syntax page of RE2 without the constructs it marks NOT SUPPORTED:

- **Literals and escapes:** `\a \f \t \n \r \v`, `\x7F`, `\x{10FFFF}`, octal `\123` and `\0`, `\Q…\E`, and a backslash before any punctuation character. A literal of more than one character is one node with all its code points; adjacent literals merge.
- **Classes:** `[…]`, `[^…]`, ranges, the POSIX classes `[[:alpha:]]` and their negations `[[:^alpha:]]`, `\d \D \s \S \w \W`, `\pN`, `\p{Greek}`, `\PN`, `\p{^Greek}`, `\P{^Greek}`. A class may hold any of these as items. `[^a]` matches a line feed; `.` does not, unless `(?s)` is in effect.
- **Groups:** `(re)`, `(?:re)`, `(?P<name>re)`, `(?<name>re)`.
- **Flags:** `(?i)`, `(?m)`, `(?s)`, `(?U)`, a flag group `(?i:re)`, a negation `(?i-m)`. `(?U)` and the lazy forms `*?`, `+?`, `??`, `{n,m}?` are read and change nothing: the answer is whether any part of the string matches, and neither the priority of the alternatives nor greediness decides that.
- **Repetitions:** `*`, `+`, `?`, `{n}`, `{n,}`, `{n,m}`. A `{` that does not begin a complete count is an ordinary character, as `x{`, `x{,2}`, `x{}` and `x{-}` are in Go.
- **Anchors:** `^ $ \A \z \b \B`. Without `(?m)`, `^` is the beginning of the text and `$` its end, not the place before a final line feed; with `(?m)` they are the beginning and the end of a line. `\b` and `\B` are ASCII.
- **Alternatives** `a|b`, and the empty pattern, which matches every string.

The classes `\d`, `\s`, `\w`, the POSIX classes and the boundary `\b` are ASCII, as in Go and RE2: `\d` is `[0-9]`, `\s` is `[\t\n\f\r ]`, `\w` is `[0-9A-Za-z_]`; the tables of `perl_groups.go` are copied as constants of `Library.Regex`, and `CharUnicodeInfo`, `char.IsWhiteSpace` and `char.IsLetter` are never used. The classes `\p…` read `Library.Unicode`.

**Vim-style forms that Go reads as text are not errors.** The syntax page of RE2 lists `x{}`, `x{-}`, `x=`, `%(x)` and `\zs` among the constructs it does not support; Go does not refuse them and reads them as ordinary characters (`x{}` is the three characters `x`, `{` and `}`, `\zs` is `\z` and `s`), and cel-go gives the values that follow. The library does the same: refusing them would make a difference with cel-go for which nobody has decided. The rule of the library is: an error where Go gives an error.

**Constructs outside RE2 are errors** where Go refuses them, which is where the syntax page of RE2 marks them NOT SUPPORTED and Go has no reading of its own. By kind, each with the error that Go gives and the offset of the table below:

- *assertions and groups of Perl and PCRE:* `(?=`, `(?!` (look-ahead), `(?<=`, `(?<!` (look-behind), `(?>` (atomic), `(?|` (branch reset), `(?#` (comment), `(?'name'`, `(?{…})`, `(??{…})`, `(?C)`, conditionals `(?(1)a|b)`, and the calls and recursion `(?R)`, `(?1)`, `(?+1)`, `(?-1)`, `(?&name)`, `(?P>name)`, `(?P=name)`: `unsupported Perl syntax` or `invalid named capture`, at the `(`;
- *control verbs* `(*ACCEPT)`, `(*COMMIT)`, `(*FAIL)`, `(*SKIP)`, `(*ANYCRLF)`, `(*BSR_UNICODE)`: `missing argument to repetition operator` at the `*`;
- *repetitions on repetitions:* the possessive forms `x*+`, `x++`, `x?+`, `x{2,3}+`, and `a**`, `a+*`, `a?*`, `a*??`, `a{2}*`, `a*{2}`: `invalid nested repetition operator` at the first operator;
- *backreferences:* `\1` to `\9`, `\g1`, `\g{name}`, `\g<name>`, `\k<name>`, `\k'name'`: `invalid escape sequence`;
- *escapes that RE2 or Perl has and Go has not:* `\C`, `\Z`, `\G`, `\K`, `\X`, `\R`, `\h`, `\H`, `\V`, `\N{name}`, `\cX`, `\e`, `\l`, `\L`, `\u`, `\U` and the others of the letters that no escape of Go uses: `invalid escape sequence`; `\p{L&}` is `invalid character class range`.

A construct that is in none of the lists and that Go refuses is refused with the error Go gives; one that Go reads is read.

**Names of `\p`.** The name of a class is resolved as `unicodeTable` of Go 1.27.3 resolves it, whatever the form: `canonicalName` first — the first letter upper-case, the rest lower-case, the characters `_`, `-` and space dropped, ASCII letters only — then `Any`, `Assigned` (the complement of `Cn`), `Ascii`, `Lc`, the general categories (`L`, `Lu`, `Cn`, …, 38 names), the scripts by their long names with `canonicalName` applied (`Greek`, `greek`, `Old_Italic` and `OldItalic` are one; `Grek` is not a name), and the aliases of the categories (`Letter`, `Lowercase_Letter`, `digit`, `punct`, …). An unknown name is `invalid character class range` at the `\p`. The list of names is generated with the tables; the checks that compare them with Go are on [unicode-tables](unicode-tables.md).

## Errors

An error is a `RegexError`: the offset and the reason. The text of the error of the library is `invalid regular expression at offset {offset}: {reason}` ([library](library.md)). The reason is the code of the error of Go, letter for letter, except for two:

| Reason | When | Offset |
| :- | :- | :- |
| `missing closing )` | a group is not closed | 0 |
| `unexpected )` | a `)` with no `(` | 0 |
| `missing closing ]` | a class is not closed | the `[` of the class |
| `trailing backslash at end of expression` | the pattern ends with `\` | the backslash |
| `invalid escape sequence` | an escape that does not exist, such as `\C`, `\Z`, `\G`, `\K`, `\X`, `\1` (a backreference); a class escape as the end of a range, `[a-\d]` | the backslash of that escape |
| `invalid character class range` | a bad range `[z-a]`, an unknown POSIX class `[[:foo:]]`, an unknown `\p` name | the range, the POSIX class or the `\p` |
| `invalid named capture` | a group name that is not `[0-9A-Za-z_]+`, or a name that is not closed by `>`: `(?P<name`, `(?<name`; and the look-behind forms `(?<=` and `(?<!`, which Go reads as a name | the group |
| `unsupported Perl syntax: {fragment}` | the `(?` that no flag group or group makes: `(?=`, `(?!`, `(?>`, `(?#`, `(?P=name)`, `(?P>name)` and the other Perl and PCRE forms. `{fragment}` is the text from the `(` up to and including the character that Go stops at — `(?=` | the `(` |
| `invalid nested repetition operator` | `a**`, `a+*`, `a*?*`, `x{2}*` and the like: a repetition that follows a repetition | the first character of the first operator (`a**` is at 1) |
| `missing argument to repetition operator` | `*` with nothing before it, `(*)`, `|*` | the operator |
| `invalid repeat count` | a count over 1000, `min > max`, or the product of nested counts over 1000 | the `{` of the repetition; of the outer one for the product |
| `expression nests too deeply` | the height of the tree is over 1000 | 0 |
| `expression too large` | the size of the program is over 100 000 | 0 |
| `invalid UTF-16` | a lone surrogate | the surrogate |

In `x(abc` the fault is at 0, since Go names the whole pattern; in `x[a` it is at 1, since Go names the class from its `[`. The reasons that the standard of the language fixes, and that the examples check by their text, are `missing closing )`, `invalid repeat count`, `expression too large`, `expression nests too deeply` and `unsupported Perl syntax: {fragment}`; the other reasons keep the text of Go, with no other promise.

An error stops the parse; the first error in the order of the text is the one given. Two errors do not compete: the size of the program is known only when the text has been read to its end, so a syntax error later in the text wins over `expression too large` (`a{1000}` a hundred and one times, then `(`, is `missing closing )`). The height and the counts are checked while the tree is built, as Go does it, so an error earlier in the text wins over them.

The reason texts are constants, except the fragment of the Perl error, which is one string made when the error is made. A `RegexError` takes no other allocation, and the message with the offset is made when it is read ([library](library.md), "Errors").

## The tree

The nodes are those of Go: no match, empty match, literal (code points and a fold flag), class, any character (with or without the line feed), the six anchors (`^`, `$` in a line and in the text; `\b`, `\B`), group, star, plus, quest, repeat (`min`, `max`; `max` is −1 for no upper end), concat, alternate. The tree lives in arrays: a node is an index, and its kind, its children, its counts and the data below are fields of those arrays. The arrays are rented from `ArrayPool<int>.Shared` and returned when the parse is over, so a steady-state parse allocates nothing; what the parse keeps — the program — is built from them ([regex-engine](regex-engine.md), "Building the program") and is a new object. The code of the parser never creates one object for a node, a child list or a class range, and uses no closure, no LINQ and no recursion.

Three values are kept in each node and finished when the node is, so that no walk is needed afterwards. They are why the parse can stand on a stack alone:

- **the height** — a leaf is 1, any other node is 1 plus the greatest height of its children (Go, `calcHeight`). The group `(…)` is a node; `(?:…)` and a flag group are not. 999 groups around one character are accepted, 1000 are `expression nests too deeply`. The height is checked when a node is built, so the error comes at the first node over 1000;
- **the product of counts** — for a repeat node, the greatest product of the counts on a path of nested repeats from it down, where the count of `{n,m}` is `m` and of `{n,}` is `n`. The leaf is 1; a repeat with the count 0 is 1 — Go's `repeatIsValid` returns at once at the count 0 and does not look into it; any other repeat is its count times the greatest value among its children; the other nodes take the greatest value of their children. Values are kept at most 1001. Go calls `repeatIsValid` when a repeat with `min ≥ 2` or `max ≥ 2` is complete; so does the parser, with the value in the node: over 1000 is `invalid repeat count` at the `{` of that repeat, which is the outer one. `{1}`, `{0,1}` and `{0}` are not checked, and `(?:a{1000}){1}` is valid. The count of a single repeat is checked when its `{…}` is read: `min` or `max` over 1000, or `min > max`, is `invalid repeat count`;
- **the size** — the instructions that the node gives, with the rules of "The size of the program" below.

## The size of the program

The limit is on m, the number of instructions of the program plus the number of ranges that a class owns ([limits](../limits.md)). The program is the one that `Compile(Simplify(tree))` of Go gives, except as noted, so the size is computed on the simplified tree without building it, in the node, from the sizes of its children. Let a node have the kind K after simplification, the flag of laziness G and the number of instructions I; an empty match has no instruction, and nothing in the program is a no-operation or a jump: a path that Go would send through a no-operation goes straight to its successor.

| Node | I | K, G after simplification |
| :- | :- | :- |
| literal of k code points | k | literal |
| class, `.`, `\p` outside brackets, an anchor | 1 | the same |
| empty match; `x{0}`; the empty pattern | 0 | empty match |
| no match (a class that holds nothing, `\P{Any}`) | 1, a class with no ranges | class |
| group | I of the child | the kind group: `simplify1` does not look through it, and a repetition of a group is never collapsed |
| concat | the sum of the children | concat |
| alternate of k children | the sum plus k − 1 | alternate |
| `x*`, `x+`, `x?` | I(x) + 1, but I(x) if x is the empty match or has the kind of the same operator and the same G (`simplify1`), as `x` itself, never through a group | the operator, or the kind of x when it is collapsed |
| `x{n}` | n · I(x) | concat; for n = 1 the kind of x |
| `x{n,}` | n = 0: `x*`; n = 1: `x+`; n ≥ 2: (n − 1) · I(x) plus the `x+` row | star, plus, concat |
| `x{n,m}`, m > n | n · I(x) plus the suffix `(x(x(x)?)?)?` of m − n levels: the first level is `x?` — the row of `x?` — and each of the other m − n − 1 levels adds I(x) + 1; with n = 0 the suffix alone | quest when n = 0 — the kind of x when m = 1 and x collapses — else concat |

So `(x*)*` has I(x) + 2 instructions and `(?:x*)*` has I(x) + 1; `()*` has 1 and `(?:)*` has 0. Go compiles a group into a `cap`, the body and a `cap`; this program has neither `cap`, so the group adds no instruction, and it stays a node of the tree only for the height and for `simplify1`.

The star of an expression that can match the empty string is one fork here, not two: Go makes `(x+)?` of it to keep the priority of the alternatives, which a program that only answers yes or no does not need. So the size differs from the count of Go for such a pattern by one per star, and the difference does not matter for any limit that Go has.

The program ends with one `match` instruction, counted in m. A class written in brackets owns the ranges of its sorted, merged list; they are counted once for the node of the class in the text, however many times a repetition copies the node (the copies share the list). A class written as one escape outside brackets — `\d \D \s \S \w \W \pX \p{Name} \PX \p{^Name}` — refers to a static table: one instruction and 0 ranges. Under `(?i)` a class owns the ranges of the list after folding, written as a bracket class or not: `\d`, `\s`, `\w` and their complements always (`\w` has six), `\pX`, `\p{Name}`, `\PX` and `\p{^Name}` when the table has a fold table, the ranges being those of the table merged with its fold table, a complement taken after the merge; a table without a fold table stays a reference, as outside `(?i)`. Go takes the same two paths (`appendGroup`, `parseUnicodeClass`). The ranges of a class are counted for the node in the text even when a repeat of count 0 (`x{0}`) leaves it out of the program, so the program may hold fewer ranges than m counts: m is the bound that the limit and the charge use, and the build allocates the ranges it emits. Examples: `abc` is 4; `^.*$` is 5; `a{2,5}` is 9; `a{0,n}` is 2n + 1; `\pL` is 2; `[a-z]{3}` is 3 instructions, 1 range and the match, 5; `(a*)*` is 4 and `(?:a*)*` is 3.

The size is the sum over the root, plus the match, plus the ranges owned by all classes of the text; it is kept in a `long` that saturates at 2³¹, so `a{1000}` repeated in nesting cannot overflow. The comparison with 100 000 is made once, after the text has been read to its end and no syntax error has been found.

**Memory of the classes.** The ranges a bracket class owns are stored once in a growing array of the parse. A class that holds a large table — `[\pL_]` has about 650 ranges — is made there at once, which the cost of the parse does not charge byte for byte; this is why the arrays are rented and why the total is capped: when the ranges owned by the whole pattern pass 100 000, the parser stops storing ranges, goes on reading the text only to find a syntax error, and ends with `expression too large` when it finds none. What the program keeps is its instructions and its owned ranges, 16 bytes for an instruction and 8 for a range, within the 16 bytes per instruction that the charge of the build pays for ([cost](../cost.md), "matches").

## Simplification and expansion

`Simplify` is applied to the finished tree, after the size and the limits have been accepted, and only while the program is being built ([regex-engine](regex-engine.md)): the counted repeats become copies, with the rules of `simplify.go` that the table above gives sizes for. They are a loop over an explicit stack of nodes still to emit, with the count of copies left, so a nesting of a thousand repeats is a thousand entries of a stack and no frames. An expansion that the size has accepted is at most 100 000 instructions; no pattern is expanded before its size is known.

## Classes and case folding

A class is built in `ClassBuilder` as a list of ranges of code points, kept sorted and merged: items are added, then `[^…]` complements the list over 0 to U+10FFFF. Go's rules are the rules here: a class in brackets with `(?i)` folds each range; the negation applies after the fold, so `(?i)\W` does not hold `ſ` (U+017F) or `K` (U+212A) and `(?i)\w` does.

**Folding by orbits.** The fold is the simple case folding of Unicode, as `unicode.SimpleFold` of Go gives it: the code points that fold together form an orbit, and `SimpleFold(c)` is the next code point of the orbit in ascending order, the smallest after the last. The orbit of `k` is `K`, `k`, U+212A; of `i` is `I`, `i` (not U+0130 or U+0131: their orbits are their own); of `ß` is `ß`, U+1E9E; `SS` is not in the orbit of `ß`. The table of `SimpleFold` is generated ([unicode-tables](unicode-tables.md)), and the code reads it without allocation.

- A literal under `(?i)` whose orbit has more than one member stays a literal with the fold flag, with the smallest member of the orbit as its code point (Go's `minFoldRune`); a literal whose orbit is itself loses the flag.
- A range under `(?i)` becomes the range and every code point whose orbit meets it: the generated list of the code points that have an orbit of more than one member is searched for those inside the range, by a binary search to the first and then forward, and each one found adds the other members of its orbit; the work is the number of ranges times a logarithm plus the number found.
- `\p{X}` and `\P{X}` under `(?i)` take the table of `X` and the table of the code points that fold into it, which Go keeps in `FoldCategory` and `FoldScript` and the generator produces too; the list is then a class that owns its ranges, with the negation after the fold. `\p{Any}` is any character and needs no fold; `\p{Ascii}` under `(?i)` is `[\x00-\x7FſK]`.

The case folding of a Unicode version is a property of its tables, so a change of the version of Go that the oracle pins regenerates them ([unicode-tables](unicode-tables.md)); nothing in this page names a code point that the tables decide.

## Where the tests are

The tests of this page are in `tests/Great.Parts.Cel.Tests/Regex/`: one class for the acceptance of each construct and its refusal with the offset and the reason of the table above; one for the limits at their edges, with the patterns of [tests](tests.md); one for the classes, their names and the folding, against the tables of Go ([unicode-tables](unicode-tables.md)); one that parses a pattern of height 1000 and one of the greatest accepted count in nesting, which keeps the claim of no recursion honest on the stack of the test thread.

Related pages: [regex-engine](regex-engine.md), [unicode-tables](unicode-tables.md), [library](library.md), [evaluation](evaluation.md), [tests](tests.md), [core](core.md).
