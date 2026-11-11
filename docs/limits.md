# Limits

This page lists the limits of the library: the sizes past which the library reports an error instead of doing the work. The limits protect an application that compiles and evaluates expressions it does not trust: the work and the memory of a call stay bounded, and going over a limit is an error of the result, not an exception. The limits of parsing, of the patterns of `matches` and of the ranges of values are fixed; the cost limit, the capacities of the compilation cache and the cancellation are set by the application.

Each error has its code of `Great.Parts.Cel.Diagnostics.CelErrorCode`, its stage and its place in the expression, see [error codes](error-codes.md).

## Parsing

| Limit | Value | How it is counted | Error |
| :- | :- | :- | :- |
| Length of the expression text | 65,536 UTF-8 bytes | The text is the .NET string as given. A text of more than 65,536 UTF-16 code units is over the limit, since each code unit is at least one byte. Otherwise its bytes are counted in one pass; a lone surrogate counts as the 3 bytes of the U+FFFD that replaces it. A text of exactly 65,536 bytes is parsed. A text over the limit is not parsed at all | `ExpressionTooLong` (1002), at the first character with which the text passes the limit |
| Nesting | 250 | As the CEL implementation in Go counts it: at a point of the text, 1 plus the number of enclosing brackets of any kind — parentheses, the arguments of a call, lists, indexes, maps and messages — plus the largest of three counts of enclosing operands: third operands of `?:`, right operands of comparisons and `in`, right operands of `+`, `-`, `*`, `/` and `%`. The condition and the second operand of `?:`, the operands of `&&` and `\|\|`, the unary `!` and `-` and left operands add nothing, so flat chains such as `a + b + c` do not grow it. So 249 nested parentheses are parsed, 250 are not | `NestingTooDeep` (1003), "nesting depth exceeds the limit of 250", at the bracket, the operator or the `?` that opens the operand; parsing stops there |
| Depth of the syntax tree | 250 | The largest number of nodes on a path from the root down, counting only binary operators, `?:`, field selections, receiver-style calls and indexes. Chains of `&&` and `\|\|` are balanced trees and add no depth; macros count as the call they are written as. So 250 chained additions, selections or indexes are parsed, 251 are not | `NestingTooDeep` (1003), "syntax tree depth exceeds the limit of 250", at the first node of depth 251; reported only when parsing reaches the end of the text |
| Number of errors | 100 | Every error of the text counts. After a syntax error parsing goes on at the next `,` or closing bracket of the construct the error is in, so one text can report many errors; they are listed by their place in the text | `TooManyErrors` (1004) in place of the 101st error, at its place, after the 100 errors before it; parsing stops there |
| Execution stack | no number | Before each recursive step the parser asks the runtime whether enough stack is left (`RuntimeHelpers.TryEnsureSufficientExecutionStack`). Within the limits above this fails only on a thread with a small stack | `InsufficientStack` (9000), at the token where parsing could not go deeper; parsing stops there |

The error that stops parsing comes after all errors found before it. A text with errors gives no program.

## Type checking and evaluation

| Limit | Value | How it is counted | Error |
| :- | :- | :- | :- |
| Execution stack | no number | Before each recursive step of macro expansion, type checking, planning, evaluation and the comparison of nested values, the library asks the runtime whether enough stack is left. Within the limits of parsing this fails only on a thread with a small stack | `InsufficientStack` (9000), at the node at which the step could not be made; the stage that was running ends |
| Cost limit | set by the application in `CelEvalOptions.CostLimit`; without it nothing is counted | The cost of an operation is charged before it runs; an operation whose charge takes the total above the limit does not run. A total equal to the limit is allowed. The units are on the page [Cost](cost.md); the time and the memory of an application function are not measured, it declares its cost with a `CelCostFunction`. An application that evaluates expressions it does not trust sets the limit | `CostLimitExceeded` (3013), at the node of the operation |
| Memory for one unit of cost | at most 64 bytes beyond the frame | The goal of the table of units: an operation with a growing size has a formula of the size, so the units bound the memory of an evaluation. A property test compares the bytes allocated with the units charged | not an error: a property of the table of units; going over the cost limit is `CostLimitExceeded` |
| Events after a signal of cancellation | at most 64 | If the token can be canceled, the evaluation counts the steps of macros and the calls of application functions and checks the token before the first event and then every 64. A token that cannot be canceled, `CancellationToken.None`, counts nothing. A one-step call checks the token before the compilation and between the compilation and the evaluation | not a `CelErrorCode`: `OperationCanceledException` |

## Extension sets

A function of an extension set has a limit where its result could otherwise grow without bound. Each limit is fixed, belongs to the version of its set, and is not configurable; going over it is an error of the result, not an exception. The limits are listed here with the function that has them, and the sets themselves are on the page [Extension sets](extensions.md). The rules of the sets that hold for every set — the order of the calls, the checks of the build — have no limit of their own beyond those of the other declarations.

| Limit | Value | How it is counted | Error |
| :- | :- | :- | :- |
| Precision of a clause of `format` | 100, in the set `strings` from version 5 | The precision is read for every letter (`%.3s`, `%.2f`, …) and acts for `%f` and `%e`. A precision above 100 is an error of the format: when the call is checked at compilation — the format string is a string constant and the argument a list literal — it is `InvalidFormatString` (2010); otherwise it is `InvalidArgument` (3008) at evaluation. The text is `could not parse formatting clause: error while parsing precision: precision {n} exceeds maximum allowed precision 100`; a number that does not fit a 64-bit integer is `…: error while converting precision to integer: value out of range` | `InvalidFormatString` (2010) at compilation; `InvalidArgument` (3008) at evaluation |
| Size of the list of `lists.range` | from 0 to 1,000,000, in the set `lists` from version 4 | The argument of `lists.range(n)` is the size of the list. An argument below 0 or above 1,000,000 is an error that comes with the 1 unit of the call, before the units of the formula, 1 + n, are charged and before the list is made. The text is `invalid argument to 'lists.range': size must be non-negative, got {n}` or `invalid argument to 'lists.range': size {n} exceeds maximum allowed (1000000)` | `InvalidArgument` (3008) at evaluation |

## Pattern of `matches`

A pattern that is a string constant is checked when the program is compiled: its error is `InvalidRegex` (2009), at the literal. Any other pattern is checked when the call is evaluated: its error is `EvalInvalidRegex` (3021), at the call. The limits are checked before the repetitions are expanded. The parser works without recursion, on a stack of its own, so a pattern needs no reserve of stack. The syntax is on the page [Standard library](standard-library.md#matches).

| Limit | Value | How it is counted | Error |
| :- | :- | :- | :- |
| Count of a repetition `{n,m}` | n ≤ 1,000 and m ≤ 1,000 | As `repeatIsValid(re, 1000)` of `regexp/syntax` in Go 1.27.3. `a{1000}` and `a{0,1000}` are accepted, `a{1001}`, `a{0,1001}` and `a{2,1}` are not | `invalid repeat count`, at the `{` of the repetition |
| Product of nested counts | 1,000 | As in Go: the counts of repetitions nested in each other are multiplied; `*`, `+` and `?` have no count and do not multiply. `(?:a{10}){100}` is accepted and `(?:a{10}){101}` is not | `invalid repeat count`, at the `{` of the outer repetition that makes the product too large |
| Height of the tree of the pattern | 1,000 | As `maxHeight` in Go: the number of nodes on the longest path from the root to a leaf. A group `(…)` is a node, a group `(?:…)` is not, a literal or a class is one node, and a repetition is a node. 999 nested groups `(` are accepted and 1,000 are not. It is counted while the tree is built | `expression nests too deeply`, at offset 0 |
| Size of the program m | 100,000 | The number of instructions of the program plus the number of ranges in its classes that are not shared tables; the one final instruction, the match, is counted. A literal is one instruction for each character; `.`, `^`, `$`, `\A`, `\z`, `\b` and `\B` are one each; `x*`, `x+` and `x?` are a copy of x and one fork, but x alone when x is an empty match or the same repetition with the same greediness (`(?:a*)*` counts as `a*`; a group is not looked through, so `(a*)*` has one more instruction); `x{n}` is n copies; `x{n,}` is n − 1 copies and `x+`; `x{n,m}` is n copies and m − n copies with m − n forks (`x{0,n}` is 2n); `x{0}` and the empty pattern are 0. A class written as one escape outside brackets — `\d \D \w \W \s \S \pX \p{Name} \PX \p{^Name}` — is one instruction and no ranges, since it uses a shared table. A class in brackets, among them `[[:alpha:]]` and `[\d_]`, owns the ranges of its normalized list, and a class under `(?i)` owns the ranges of the list after folding case; the ranges are counted once for each class in the text of the pattern, however many times a repetition copies it (`[a-z]{3}` is 5: three instructions, one range and the match). The size is computed from the tree by a formula before any repetition is expanded, and it is checked when the whole text has been read: a syntax error later in the text wins over `expression too large` | `expression too large`, at offset 0 |

The edge of the size is `a{1000}` ninety-nine times followed by `a{999}`, and `[a-z]{1000}` ninety-nine times followed by `a{900}`: both are 100,000 and accepted, and one more instruction is refused. The first error in the order of the text is the one reported; the height and the counts are checked while the tree is built, so an error earlier in the text wins over them.

The CEL implementation in Go limits the size to about 3.3 million instructions; a pattern between the two limits has a value there and is an error here.

**Calibration of the limit of the size.** A pattern at the limit, as a variable in an expression of a new environment (`net10.0`, JIT, a container with two processors that was not the only load of the machine; the median of 11 runs after 3):

| What | Result |
| :- | :- |
| the first evaluation, which parses the pattern and builds the program | 4 to 10 ms for the two patterns above, and 1.6 MB allocated: the program that the cache then keeps, 16 bytes for each instruction |
| the same evaluation again, a hit in the cache | nothing allocated beyond the call itself, less than 0.01 ms for a string of up to 10 characters |
| a pattern that is a string literal, built when the expression is compiled | 4 to 7 ms |
| the search in a string of 1,000 characters | 11 to 15 ms |
| the search in a string of 10,000 characters | about 1 s |

The search takes time proportional to the length of the string times the size of the pattern, and a unit of its cost takes about 10 ns; a string of 10,000 characters against a pattern of the size limit costs about 100 million units, so a cost limit stops such a call. The limit of 100,000 keeps the build and the program small: a few milliseconds and 1.6 MB.

## Caches and accelerations

A cache that is over its capacity is not an error: it evicts entries when one is inserted, with no threads and no timers. The result of an expression and its cost do not depend on a cache.

### Compilation cache

`CelCompilationCache` keeps the results of compilations of one environment. The application creates it with two capacities, which are required parameters without defaults: the number of entries and the total length of the expression texts in UTF-16 code units.

| Limit | Value | How it is counted | Error |
| :- | :- | :- | :- |
| Capacity of the cache of compilations: entries and total length of the texts | set by the application; recommended 80,000 entries and 900,000 code units of total text length | An entry keeps the program of its text, so it takes about 400 bytes and about 33.6 bytes for each code unit of its text (measured by `GC.GetTotalMemory(true)` before and after a cache is filled with 1,000 programs of texts of 20, 200 and 2,000 code units, `net10.0`). The recommended values keep a full cache within 64 MiB: the number of entries is at most 32 MiB / 400 bytes and the total length of the texts at most 32 MiB / 33.6 bytes, each rounded down to one significant digit. Unsuccessful compilations are cached too; when an insertion takes the cache over a capacity, entries are evicted | not an error: entries are evicted |

A text longer than the capacity of the total length is compiled and returned but not kept. The result of an expression and its cost do not depend on the cache.

### Cache of patterns of `matches`

The programs of the patterns of `matches` that are not string literals are kept in a cache of the environment. It is shared by all the programs of the environment, starts empty, and is empty again in an environment made by `CelEnvironment.ToBuilder()`; there is no cache that is global to the process. A pattern that is refused is kept too, as an error. A read of the cache takes no lock; an insertion takes one, and when the entry does not fit one of the two capacities, entries are evicted, the oldest first, an entry that was read since it was last passed over getting a second chance. A pattern longer than 4,096 UTF-16 code units, or one whose size is more than the whole capacity, is used and not kept. The two capacities are internal constants of the library; the application has no parameter for them.

| Limit | Value | How it is counted | Error |
| :- | :- | :- | :- |
| Number of patterns in the cache of patterns | 128 | Each entry, a program or an error, counts one | not an error: entries are evicted |
| Sum of the sizes of the programs in the cache of patterns | 100,000 | The sizes m of the programs; an entry that is an error counts one | not an error: entries are evicted |

**Calibration of the capacities.** The memory of an entry was measured by `GC.GetTotalMemory(true)` before and after environments were filled with distinct patterns of the sizes 2, 10, 100, 1,000 and 10,000, each size in a process of its own, `net10.0`, JIT. An entry takes about 194 bytes, plus the key, the text of the pattern, which is a string of 22 + 2 · L bytes for L code units, plus 16 bytes for each unit of its size m: 250 bytes for a pattern of the size 2 and a text of 1 character, 1,826 for the size 100 and a text of 5, 16,234 for the size 1,000 and a text of 6, and 160,332 for the size 10,000 and a text of 69. The 16 bytes for each unit is the same number that the build of the program is charged for (⌈m/4⌉ units of at most 64 bytes). The capacities keep a cache full to both of them within 4 MiB: the sum of the sizes is at most 3 MiB / 16 bytes = 196,608, which rounds down to one digit as 100,000, and the number of patterns is at most 1 MiB / (194 + 22 + 2 · 4,096) bytes, the entry with the longest key that is kept, which is 124 and has the same first digit as 128. So a cache full to both capacities takes about 1.6 MiB with the short keys of practice (100,000 · 16 bytes and 128 · 194 bytes, and the keys), and at most 2.6 MiB when every key is 4,096 code units long.

Bytes for a unit of cost at n = 0, in the steady state of the process: a pattern that is a literal and a pattern from data found in the cache allocate nothing beyond the call, 0 bytes for a unit; a pattern from data that is not in the cache, so that its program is built and kept, allocates 45.6 bytes for a unit at most, for the sizes 10 to 100,000. The first evaluation of a size in a process, when the arrays of the engine are taken from the pool of arrays for the first time, allocates them once: up to 318 bytes for a unit for a literal and 189 for a pattern from data, which is a one-time allocation of that size and not a rate. The two numbers of the steady state are below the 64 bytes of the page [Cost](cost.md#calibration).

## Values

The ranges of the types of CEL. An operation that leaves a range is an error of evaluation, and a literal outside the range is a parse error.

| Limit | Value | How it is counted | Error |
| :- | :- | :- | :- |
| `int`, `uint` | 64 bits: `int` from −2⁶³ to 2⁶³ − 1, `uint` from 0 to 2⁶⁴ − 1 | The result of arithmetic outside the range is an error; so is a literal | `Overflow` (3006); a literal: `InvalidLiteral` (1001) |
| `double` to `int` and `uint` | `int`: the open interval (−2⁶³, 2⁶³); `uint`: from 0 up to, not including, 2⁶⁴ | A conversion of a NaN, an infinity or a value outside the interval is an error | `ConversionRange` (3007) |
| `duration` | one `int` of nanoseconds, about ±292 years: from −2⁶³ to 2⁶³ − 1 nanoseconds | A sum or difference outside the range is an error; so is a conversion from a string, and a `TimeSpan` of the application whose nanoseconds do not fit | `Overflow` (3006) for arithmetic; `ConversionRange` (3007) for a conversion |
| `timestamp` | from `0001-01-01T00:00:00Z` to `9999-12-31T23:59:59.999999999Z`, in nanoseconds | The same: a moment moved outside the range by `+` or `-` is an error; so is `timestamp(int)` outside −62135596800 … 253402300799 seconds, and a string of a moment outside the range | `Overflow` for arithmetic; `ConversionRange` for a conversion |
