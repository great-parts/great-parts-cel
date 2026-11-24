# Error parts: the fragment, the subject and the types of an error

Version 2.0 gives every `CelError` the fragment of the expression it refers to (`Span`) and the name and the CEL types it is about (`Subject`, `ExpectedType`, `ActualType`), and two texts for people: `ToString()` and `ToDisplayString(expression)`. The codes, their stages and their messages do not change. Which parts each code has is the section "Parts of an error" of `docs/error-codes.md`; this page says where the values come from.

| File | Content | Core |
| :- | :- | :- |
| `src/Great.Parts.Cel/Diagnostics/CelError.cs` | the four new members, `ToString`, `ToDisplayString`; one internal constructor that takes all parts | no |
| `src/Great.Parts.Cel/Diagnostics/ErrorTemplate.cs` | one template per kind of evaluation error: it makes the text and the parts when they are read | no |
| `src/Great.Parts.Cel/Diagnostics/LineTable.cs` | the end of a node by its place, found when an error asks for its fragment | no |
| `src/Great.Parts.Cel/Diagnostics/CelSourceSpan.cs` | the public struct | no |
| `src/Great.Parts.Cel/Diagnostics/CelSourceLocation.cs` | `ToString` | no |
| `src/Great.Parts.Cel/Syntax/**` | the end offset of each node | no |
| `src/Great.Parts.Cel/Checking/**`, `Evaluation/**`, `Library/**`, `Values/**`, `Declarations/**`, `Configuration/**` | the places that create errors pass the parts | yes, where the file is in the core |

## The end of a node

The parser already records, for each node id, the offset of the token that is the place of the node (the identifier, the operator, `(` of a call, `.` of a selection, the first character of a literal). It now records the offset just after the last token of the node as well (`NodeFactory`, `ParsedExpression.GetEnd`): for a call the `)`, for an operator the end of its last operand, for a selection the end of the field name, for a macro call the `)` of the call before expansion. The expanded nodes of a macro take the start and the end of the call they come from, as their start is taken now. The end is recorded when the parser finishes the node, so it costs one `int` per node and no second walk; `LineTable.GetEnd(offset)` finds it lazily, only for an error that asks for its fragment.

The fragment of an error is `[place, end)` of its node: it never starts before `Location`, so `Span.Start == Location` holds for every error. A parse error at a token has the token as its fragment; at the end of the text, length 0. The root of an expression has no rule of its own: `ResultConversion` (3015) and `ExpectedTypeMismatch` (2003) keep the place they have, the place of the root node, so their fragment follows the kind of that node — the whole text for an identifier, a literal or a selection chain, `+ b` for `a + b`, `(…)` for a call. The place is not moved to the first character: that would change the place of those codes, and the rule `[place, end)` would need an exception. The length is `end − start` in UTF-16 code units of the text that was parsed; with `CelCompileOptions.SourceLocation` the start is moved as now and the length is the same.

An error of evaluation takes the node of the plan that gave it: each plan node keeps the node id it has now, and the error looks up start and end through it. A build error has no node: `Span` is `default`, length 0. An error in a JSON description is a build error too: `Span` is `default`, and its position in the JSON text — line and column, as now — is part of the message and of the source location the error already carries, without a length; the fragment is a part of the expression, and a JSON description is not one. `Subject` of `InvalidConfiguration` (4009) is the key when the error is about a key.

## Subject, expected type, actual type

The parts are values that the place of the error already has when it formats the message, so no part is computed only for the error:

- `Subject` is the string that the message puts in quotes, unquoted: the name of the variable, field, function, overload id, type, set, key, zone or macro. A .NET type is its full name, as the message writes it (3012, 3015, 4008). An operator is its function name, such as `_+_`. For 4019 it is the first of the two types by ordinal comparison of their full names, and the text names both in that order.
- `ExpectedType` and `ActualType` are `CelType` values. A check error takes the static types the checker compared; an evaluation error the type of the value (`CelValue.Kind` and, for an object, its description) and, for 3011, 3015 and 3028, the type that `{actualType}` of the message names, so the part and the text never disagree; where the message names a type without parameters at the top level (`list`, `map`), the part is `list(dyn)` or `map(dyn, dyn)`, the type that `type()` gives for that value, and deeper the type that the text writes (`list(string)`).
- A part is the very value that the message substitutes; a place of the error in the library passes it as a separate argument. For 3008, 3014 and 3020 that is the value of the text: the function or the overload for a call, the field for a member, the type for equality and creation. The exception is an error that a type description (generated code, the protobuf package) gives through `CelObjectType.FieldError(number, code, detail)` as text only, since no public member is added for the parts: for 3020 the type URL is taken from the text, which the protobuf package builds by the template of the code; for 3007 from `FieldError`, `ExpectedType` and `ActualType` are the declared CEL type of the field in the description (`int` for `int32`, `uint` for `uint32`), since the value that did not fit has that CEL type.
- A code without a part has `null` in it. A part that the page of the codes names "if …" is `null` when the condition does not hold (2002: `Subject` only for a field of a message literal).

The error helpers (`CheckErrors`, `CallSite`, the binders of the facade and the checks of the declarations) get one more argument each for the parts they own; no new allocation is made on a path without an error. The error value of the evaluation (`CelValue` of the kind `Error`) keeps the parts by reference to the objects that already exist, with one shared `ErrorTemplate` per kind of error, until the message or a part is read, so a function that returns an error value does not format text it may never need, and the error object does not grow.

## Errors of other packages

The internal constructor of `CelError` stays internal, and the core grants no `InternalsVisibleTo` to the packages that extend it, so `CelError.Create` is the public way to make an error with a code of the table: `Create(code, stage, message, location, length, subject, expectedType, actualType)`, all after the message optional. It checks the code and the stage with the generic `Enum.IsDefined` (no reflection, safe for Native AOT), the message (not empty) and the length (not negative), and makes the error with no exception. `location`, when given, names the document, and the error then has `Location`, `Span.Start` and `SourceLocation` equal: an error of a document knows no expression, so its place is the place in the document. `CelException(IEnumerable<CelError>)` copies the errors into an `ImmutableArray`, rejects an empty list and a `null` element, and keeps as `InnerException` the exception of the first error that has one, as `CelException.FromErrors` does for the errors of the library. The package `Great.Parts.Cel.Policy` makes the errors of its documents so ([policy](policy.md)).

## Texts for people

`CelSourceLocation.ToString()`: `{line}:{column}`, and `{sourceName}:{line}:{column}` when there is a source name; `default` gives `""`. Line and column are 1-based, as now.

`CelError.ToString()`: `{Code} at {location}: {Message}` when the error has a place (`UndeclaredReference at 1:7: undeclared reference to 'totl'`), `{Code}: {Message}` for a build error. The code is the name of the member of `CelErrorCode`.

`ToDisplayString(expression)` returns three lines joined by `"\n"`: the message, the line of `expression` where the fragment starts, and a mark under the fragment. The mark is spaces up to the column of the start, then `^`, then `~` for each further code unit of the fragment on that line; a fragment of length 0 is one `^`; a fragment that goes on to the next line is marked to the end of its first line. The line is taken from `expression` by the offset of the start, counting `\n`, `\r\n` and `\r` as line ends, as the parser counts lines. A build error, which has no fragment, gives the message alone. `ArgumentException` (`ParamName` `expression`) when `Span.Start.Offset + Span.Length` is greater than `expression.Length`; the method does not check that the text is the one compiled, since it cannot, and a different text of sufficient length gives a mark at the same offsets. A tab is written as it is: the mark counts code units, not display columns.

The texts are not a contract: the examples fix them for this version.

## Tests

The examples (`tests/Great.Parts.Cel.Tests/Examples/Diagnostics/ErrorSpanTests.cs`, `ErrorPartsTests.cs`) fix the fragment of each stage, the parts of each code that an expression or a builder reaches, and the three texts. The developer tests add the rest: the end of the parser on every node kind (for every node of a parsed expression `start ≤ end ≤ length`, and the end of a node is not before the end of any of its children), the parts of the codes that need an application, a JSON description or protobuf, one test per code, and the argument checks of `ToDisplayString`.

Related pages: [syntax](syntax.md), [checking](checking.md), [evaluation](evaluation.md), [facade](facade.md), [facade-2.0](facade-2.0.md).
