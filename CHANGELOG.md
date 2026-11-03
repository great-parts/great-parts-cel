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
- The targets `net10.0` and `net11.0`, under JIT and Native AOT.

[Unreleased]: https://github.com/great-parts/great-parts-cel/commits/main
