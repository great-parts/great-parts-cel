# Great.Parts.Cel.Filtering: instructions for AI coding agents

This file is for an agent that helps a developer use the package `Great.Parts.Cel.Filtering` in an application. It is not for the contributors of the library; they have `AGENTS.md` in the repository. Read the `AGENTS.md` of `Great.Parts.Cel` first: a filter becomes an ordinary program of a CEL environment. Versions 1.x of the library are not supported; this package exists from 2.0.

## What it is

- `new CelFilterCompiler(environment, options)` reads the description of a CEL environment once. The fields of a filter are the variables of the environment and the fields of their types; with `UseInput<T>()` they are the members of the type of the item.
- `compiler.Compile(filter)` translates an AIP-160 filter (`google.aip.dev/160`) into a CEL expression, type-checks it and returns a `CelProgram<bool>` or all errors; `compiler.Parse(filter)` returns the CEL expression without type checking, with the positions of the nodes in the text of the filter, for an application that translates further, for example into SQL.
- Every error has its place in the text of the filter in `CelError.Span`, in UTF-16 code units. The filter comes from a client: errors are returned, never thrown, and a filter that is too long or too deeply nested is an error too.

## Rules for the agent

- The environment decides the type of each field. Declare the fields with their CEL types; a field that the environment does not declare is `UndeclaredReference` at its place in the filter.
- An integer field that holds an enumeration is named in `CelFilterOptions.EnumTypes` (the field, and the full name of the enumeration type of the environment); only then does `state = ACTIVE` read a name. Without it the field takes integers.
- A value without a field, such as `Dune`, is a search: name the string fields in `CelFilterOptions.SearchFields`, or it is `InvalidFilterRestriction`.
- A time is a string in quotes: `published > "2026-01-01T00:00:00Z"`. A time without quotes is a syntax error.
- A name on the right of a comparison is a value, not a field: `title = author.name` compares with the text `author.name`.
- A star at the start or the end of a string in quotes is a pattern, with `=` and `!=` on a string field only.
- A repeated field on the path is walked only with `:`: `editions.year:1965`; `editions.year = 1965` is an error.
- A field of a message type that is not set reads as an empty message in a protobuf item: the guard `x != null` of a top-level field cannot tell not set from set and empty (`author:*` is true, `author.name != "X"` matches an item without `author`); a nested field is checked with `has()` and is exact, and an item of a .NET type with a nullable member is exact.
- `f(a):v` is not defined: a function with `:` is `InvalidFilterRestriction`, and a function on the right of a comparison is refused too.
- A star in a value that is not `=` or `!=` of a string (`<`, `>`, a function result, a key of a map) is `InvalidFilterValue`; a star outside the quotes is a character.
- The value of a `decimal` field is checked as a number only: more than 34 digits is an error of evaluation of the item, not of the translation.
- Do not parse the text of a filter yourself and do not build CEL text from it by hand: use `Parse` and the tree, or `Compile`.
- The compiler is immutable and thread-safe: keep one per environment and options. It keeps no cache of programs; an application that compiles the same filter often keeps the program by the text of the filter.
