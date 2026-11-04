# Limits

This page lists the limits of the library: the sizes past which the library reports an error instead of doing the work. The limits protect an application that compiles and evaluates expressions it does not trust: the work and the memory of a call stay bounded, and going over a limit is an error of the result, not an exception. The limits of parsing and of the ranges of values are fixed; the cost limit, the capacities of the compilation cache and the cancellation are set by the application.

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

## Compilation cache

`CelCompilationCache` keeps the results of compilations of one environment. The application creates it with two capacities, which are required parameters without defaults: the number of entries and the total length of the expression texts in UTF-16 code units.

| Limit | Value | How it is counted | Error |
| :- | :- | :- | :- |
| Capacity of the cache of compilations: entries and total length of the texts | set by the application; recommended 80,000 entries and 900,000 code units of total text length | An entry keeps the program of its text, so it takes about 400 bytes and about 33.6 bytes for each code unit of its text (measured by `GC.GetTotalMemory(true)` before and after a cache is filled with 1,000 programs of texts of 20, 200 and 2,000 code units, `net10.0`). The recommended values keep a full cache within 64 MiB: the number of entries is at most 32 MiB / 400 bytes and the total length of the texts at most 32 MiB / 33.6 bytes, each rounded down to one significant digit. Unsuccessful compilations are cached too; when an insertion takes the cache over a capacity, entries are evicted | not an error: entries are evicted |

A text longer than the capacity of the total length is compiled and returned but not kept. The result of an expression and its cost do not depend on the cache.

## Values

The ranges of the types of CEL. An operation that leaves a range is an error of evaluation, and a literal outside the range is a parse error.

| Limit | Value | How it is counted | Error |
| :- | :- | :- | :- |
| `int`, `uint` | 64 bits: `int` from −2⁶³ to 2⁶³ − 1, `uint` from 0 to 2⁶⁴ − 1 | The result of arithmetic outside the range is an error; so is a literal | `Overflow` (3006); a literal: `InvalidLiteral` (1001) |
| `double` to `int` and `uint` | `int`: the open interval (−2⁶³, 2⁶³); `uint`: from 0 up to, not including, 2⁶⁴ | A conversion of a NaN, an infinity or a value outside the interval is an error | `ConversionRange` (3007) |
