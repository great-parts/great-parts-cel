# Limits

This page lists the limits of the library: the sizes past which the library reports an error instead of doing the work. The limits protect an application that compiles and evaluates expressions it does not trust: the work and the memory of a call stay bounded, and going over a limit is an error of the result, not an exception. The application cannot change these limits.

Each error has its code of `Great.Parts.Cel.Diagnostics.CelErrorCode`, the stage `Parse` and its place in the expression, see [error codes](error-codes.md).

## Parsing

| Limit | Value | How it is counted | Error |
| :- | :- | :- | :- |
| Length of the expression text | 65,536 UTF-8 bytes | The text is the .NET string as given. A text of more than 65,536 UTF-16 code units is over the limit, since each code unit is at least one byte. Otherwise its bytes are counted in one pass; a lone surrogate counts as the 3 bytes of the U+FFFD that replaces it. A text of exactly 65,536 bytes is parsed. A text over the limit is not parsed at all | `ExpressionTooLong` (1002), at the first character with which the text passes the limit |
| Nesting | 250 | As the CEL implementation in Go counts it: at a point of the text, 1 plus the number of enclosing brackets of any kind — parentheses, the arguments of a call, lists, indexes, maps and messages — plus the largest of three counts of enclosing operands: third operands of `?:`, right operands of comparisons and `in`, right operands of `+`, `-`, `*`, `/` and `%`. The condition and the second operand of `?:`, the operands of `&&` and `\|\|`, the unary `!` and `-` and left operands add nothing, so flat chains such as `a + b + c` do not grow it. So 249 nested parentheses are parsed, 250 are not | `NestingTooDeep` (1003), "nesting depth exceeds the limit of 250", at the bracket, the operator or the `?` that opens the operand; parsing stops there |
| Depth of the syntax tree | 250 | The largest number of nodes on a path from the root down, counting only binary operators, `?:`, field selections, receiver-style calls and indexes. Chains of `&&` and `\|\|` are balanced trees and add no depth; macros count as the call they are written as. So 250 chained additions, selections or indexes are parsed, 251 are not | `NestingTooDeep` (1003), "syntax tree depth exceeds the limit of 250", at the first node of depth 251; reported only when parsing reaches the end of the text |
| Number of errors | 100 | Every error of the text counts. After a syntax error parsing goes on at the next `,` or closing bracket of the construct the error is in, so one text can report many errors; they are listed by their place in the text | `TooManyErrors` (1004) in place of the 101st error, at its place, after the 100 errors before it; parsing stops there |
| Execution stack | no number | Before each recursive step the parser asks the runtime whether enough stack is left (`RuntimeHelpers.TryEnsureSufficientExecutionStack`). Within the limits above this fails only on a thread with a small stack | `InsufficientStack` (9000), at the token where parsing could not go deeper; parsing stops there |

The error that stops parsing comes after all errors found before it. A text with errors gives no program.
