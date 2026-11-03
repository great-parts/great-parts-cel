# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Until 1.0.0 the public API may change in any version.

A change of the result or of the error of an expression that was valid in an earlier version is listed under `Behavior changes` of its version.

## [Unreleased]

### Added

- Compilation and evaluation of an expression: `CelEnvironmentBuilder`, `CelEnvironment.Compile<T>` and `CelProgram<T>.Evaluate`, with the result converted to the requested .NET type.
- Literals of the types `int`, `uint`, `double`, `string`, `bytes`, `bool` and `null`, list literals and map literals; a map is enumerated in the order of its literal.
- `int` addition, division and negation, with the errors `Overflow` and `DivisionByZero`, and logical or, `||`.
- Variables declared in code or in an environment description in JSON (`CelEnvironmentJson`).
- Parse, type and evaluation errors as results, not exceptions, each with its code of `CelErrorCode`, its stage and its place in the expression.
- Parsing of the complete CEL syntax: the conditional operator `?:`, `&&`, comparisons and `in`, subtraction, multiplication and remainder, logical not, field selection, also of escaped field names in backticks, indexing, receiver-style calls, message literals and names with a leading dot. Type checking and evaluation of these constructs are not available yet.
- The parse errors `ReservedIdentifier`, for a reserved word such as `if` used as a name, and `SyntaxNotEnabled`, for the optional syntax `.?`, `[?` and `?` without its extension set and for `decimal` literals such as `1.5m`.
- Parsing of the standard macros `has`, `all`, `exists`, `exists_one`, `map` with two or three arguments and `filter`, with the parse error `InvalidMacroCall` for a call that a macro cannot expand, such as `xs.all(1, true)` or `has(x)`. Type checking and evaluation of macros are not available yet.
- The limits of parsing for untrusted text, on the new page [Limits](docs/limits.md): 65,536 UTF-8 bytes of text (`ExpressionTooLong`), a nesting and a syntax tree depth of 250 (`NestingTooDeep`), 100 errors (`TooManyErrors`), and `InsufficientStack` instead of a stack overflow on a thread with a small stack.
- Parsing reports every error of a text, up to the limit, in the order of the text: after a syntax error it goes on at the next comma or closing bracket. An unexpected exception inside the parser becomes the error `InternalError` with the original exception.
- `CelCompileOptions.SourceLocation`: the position at which an expression starts in its source document. Every compilation and evaluation error of the expression then has its position in the document in `CelError.SourceLocation`.
- The targets `net10.0` and `net11.0`, under JIT and Native AOT.

[Unreleased]: https://github.com/great-parts/great-parts-cel/commits/main
