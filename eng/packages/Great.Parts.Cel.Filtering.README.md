# Great.Parts.Cel.Filtering

![Great.Parts.Cel logo](https://raw.githubusercontent.com/great-parts/great-parts-cel/main/eng/packages/icon.png)

[![NuGet version](https://img.shields.io/nuget/v/Great.Parts.Cel.Filtering)](https://www.nuget.org/packages/Great.Parts.Cel.Filtering)
[![NuGet downloads](https://img.shields.io/nuget/dt/Great.Parts.Cel.Filtering)](https://www.nuget.org/packages/Great.Parts.Cel.Filtering)
[![CI status](https://img.shields.io/github/actions/workflow/status/great-parts/great-parts-cel/ci.yml?branch=main&label=ci)](https://github.com/great-parts/great-parts-cel/actions/workflows/ci.yml)
[![OpenSSF Best Practices](https://img.shields.io/cii/level/16897)](https://www.bestpractices.dev/projects/16897)
[![OpenSSF Scorecard](https://img.shields.io/ossf-scorecard/github.com/great-parts/great-parts-cel?label=openssf%20scorecard&style=flat)](https://scorecard.dev/viewer/?uri=github.com/great-parts/great-parts-cel)
[![License: Apache-2.0](https://img.shields.io/badge/license-Apache--2.0-blue)](https://github.com/great-parts/great-parts-cel/blob/main/LICENSE)

AIP-160 filters for [Great.Parts.Cel](https://www.nuget.org/packages/Great.Parts.Cel), the CEL (Common Expression Language) library for .NET / C#.

A list method of an API often takes a `filter`: the text of [AIP-160](https://google.aip.dev/160), such as `author.name = "Frank Herbert" AND published > "2026-01-01T00:00:00Z"`. The package translates the text into an ordinary CEL expression of your environment, type-checks it there and compiles it into a program that tells for each item whether it matches. Every error has its place in the text of the filter, and a client that sends the filter gets the errors back, not an exception. The package depends only on `Great.Parts.Cel` and uses no reflection, so it works under trimming and Native AOT and in the browser.

Documentation: start with [Getting Started](https://github.com/great-parts/great-parts-cel/blob/main/docs/getting-started.md), seven short scenarios from the first expression to a web service; the pages are listed in the Documentation section below.

## Install

The package targets `net10.0` and `net11.0` and depends only on `Great.Parts.Cel`:

```shell
dotnet add package Great.Parts.Cel.Filtering
```

## Use

The fields of a filter are the variables of the environment, and the fields of the types of the variables. With `UseInput<T>()` the members of the type of the item are the fields:

```csharp
using Great.Parts.Cel;
using Great.Parts.Cel.Filtering;

CelEnvironment env = CelEnvironment.Create(b => b
    .AddVariable<string>("title")
    .AddVariable<long>("pages"));

CelFilterCompiler compiler = new(env);
CelProgram<bool> program = compiler.Compile("title = \"Dune\" AND pages > 100").GetProgramOrThrow();

bool matches = program.Evaluate(new CelBindings().Add("title", "Dune").Add("pages", 412L)).GetValueOrThrow();
```

`Compile` gives the program or all errors. `Parse` gives the CEL expression without type checking, with the position of each node in the text of the filter: translate it further with your own code, for example into SQL, or print it with `CelSyntax.Format`.

## What a filter becomes

The value of a restriction is read by the type of its field, so `rating >= 4` is `rating >= 4.0` for a `double` field. The comparison is guarded where a field may be missing: an item without the object does not match, also for `!=`.

| Filter | CEL expression |
| :- | :- |
| `title = "Dune"` | `title == "Dune"` |
| `published > "2026-01-01T00:00:00Z"` | `published > timestamp("2026-01-01T00:00:00Z")` |
| `read_time < 20s` | `read_time < duration("20s")` |
| `author.name != "X"` | `author != null && author.name != "X"` |
| `author.address.city = Paris` | `author != null && has(author.address) && author.address.city == "Paris"` |
| `title = "Du*"`, `"*une"`, `"*un*"` | `title.startsWith("Du")`, `title.endsWith("une")`, `title.contains("un")` |
| `tags:sf` | `"sf" in tags` |
| `labels.genre = sf` | `"genre" in labels && labels["genre"] == "sf"` |
| `editions.year:1965` | `editions.exists(e, e.year == 1965)` |
| `author:*` | `author != null` |
| `lower(title) = "dune"` | `lower(title) == "dune"` |
| `Dune` | `title.contains("Dune") \|\| author != null && author.name.contains("Dune")` |

A value without a field is searched in the fields that `CelFilterOptions.SearchFields` names, as a substring with the case kept; without search fields it is an error. `CelFilterOptions.EnumTypes` names the integer fields that hold an enumeration, so that `state = ACTIVE` reads the name of a constant.

## Errors

| Error | When |
| :- | :- |
| `FilterSyntaxError` | the text is not a filter: an unclosed string or parenthesis, a keyword out of place, text after the end of the filter |
| `InvalidFilterValue` | the value does not fit the type of the field: a fraction for an `int`, a minus for a `uint`, a text that is not a time, a name that is not a constant of the enumeration |
| `InvalidFilterRestriction` | the comparison is not allowed for the field: an order of a `bool` or of an enumeration, a repeated field outside `:`, a value without a field when there are no search fields |
| `ExpressionTooLong`, `NestingTooDeep`, `TooManyErrors` | the limits of a filter from a client: 65,536 bytes, 250 nested parentheses, 100 errors |
| the errors of type checking | a field that the environment does not declare, a function that does not take the arguments |

## Differences from AIP-160

AIP-160 asks a service to document what it restricts and what it reads differently. This package does:

| AIP-160 | This package |
| :- | :- |
| A time is written as text, without quotes | A time is a string in quotes: the `:` of the time is not text, so `t > 2026-01-01T00:00:00Z` is a syntax error |
| `arg = comparable`: the value on the right may be a name | A name on the right is the value by its text, never a field; a function on the right is refused; a literal on the left is the path of a field |
| A field is present when its value is not the default | A field of the top level that is not an object, a list or a map is always present, with `0` and `false` too; a field of a nested object is present when it is set |
| A duration is a number with the suffix `s` | The whole notation of the duration of CEL: `1h30m`, `-1h30m`, `1.5s` |
| `null` has no defined meaning | For a wrapper, an object and a dynamic value `= null` is the absence; for a string field `null` is the string; for other types it is an error |
| An order of a `bool` or of an enumeration is not advised | `=`, `!=` and `:` are allowed; an order is `InvalidFilterRestriction` |
| The function `arguments` is special | There is no special function: `arguments` is a function of the environment, if the environment declares one |
| Restrictions do not depend on white space | White space inside a name or a call is not allowed: `a . b` is an error, `f (x)` is two values |
| A sequence is a fuzzy AND | A sequence is `AND`; there is no fuzzy mode |
| A star in the middle of a value and on both sides | A star at the start, at the end or on both sides of a string in quotes is a pattern; a star in the middle, after a backslash, or outside the quotes is a character |
| A value without a field matches anywhere | It is a substring, with the case kept, in the search fields of the options |
| Names of fields are `snake_case` | Names are those of the variables and fields of the environment, as they are |
| A field of a message type that is not set reads as an empty message in a protobuf item | For an item that is a protobuf message the guard `x != null` of a field of the top level cannot tell not set from set and empty: `author:*` is true and `author.name != "X"` matches an item without `author`; a nested field is checked with `has()` and is exact. An item of a .NET type with a nullable member is exact |
| `f(a):v` is not defined | A function with `:` is `InvalidFilterRestriction`; a function on the right is refused too |
| A star in a value | A star in a value that is not `=` or `!=` of a string (`<`, `>`, a function result, a key of a map) is `InvalidFilterValue`; a star outside the quotes is a character |
| The value of a `decimal` field | Checked as a number only; more than 34 digits is an error of evaluation of the item, not of the translation |

## Test corpus

AIP-160 has no official test suite, so the package is checked by a corpus of its own: the rules of the tables above are covered by cases that translate and by cases that are an error, and the corpus runs on every platform that the library supports, except `browser-wasm`.

AIP-160: own test corpus, 369 cases, run on 2026-11-24, [the corpus](https://github.com/great-parts/great-parts-cel/tree/main/tests/filtering/testdata).

The corpus is a set of cases, not a measure of conformance: there is no share and no badge for it.

## For AI coding assistants

The package carries a file `AGENTS.md` in its root: when to use the package, what the translation does, and the mistakes that are easy to make. The NuGet MCP server returns it with its tool `get_package_context` for a package that is restored on the machine.

## Documentation

The documentation of a release is the content of the repository at the tag of the release. Until the release of 2.0, the links on this page point to the `main` branch of the repository.

- [Getting Started](https://github.com/great-parts/great-parts-cel/blob/main/docs/getting-started.md): seven scenarios, from the simple entry to a web service
- [README of the repository](https://github.com/great-parts/great-parts-cel/blob/main/README.md)
- [Type mapping](https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md)
- [Error codes](https://github.com/great-parts/great-parts-cel/blob/main/docs/error-codes.md)
- [Versioning and compatibility](https://github.com/great-parts/great-parts-cel/blob/main/docs/versioning.md)
- [Changelog](https://github.com/great-parts/great-parts-cel/blob/main/CHANGELOG.md)

## License

[Apache License 2.0](https://github.com/great-parts/great-parts-cel/blob/main/LICENSE).
