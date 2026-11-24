# Filtering: AIP-160 filters as CEL expressions

`src/Great.Parts.Cel.Filtering/` is the package `Great.Parts.Cel.Filtering`. It parses a list filter written in the AIP-160 syntax, translates it into a tree of the core ([public-ast.md](public-ast.md)) with `CelExprFactory`, and compiles the tree with `CelEnvironment.CompileParsed<bool>`. The package uses only the public API of the core and has no `InternalsVisibleTo`. The public types are `CelFilterCompiler` and `CelFilterOptions`; everything else is internal to the namespace `Great.Parts.Cel.Filtering`. The tests are described in [tests.md](tests.md#tests-of-the-filtering-package), the corpus in [filtering-corpus.md](filtering-corpus.md).

## Components

| Type | Role |
| :- | :- |
| `CelFilterCompiler` | The entry. The constructor validates the options and builds `FieldCatalog`. `Parse` checks the length, runs `FilterParser` (it uses `FilterLexer`) and `FilterTranslator`; `Compile` adds `CompileParsed<bool>` |
| `FieldCatalog` | Read once from the environment: variables by name, object types, enumeration types with the value of each constant, functions by name, the enumeration map of the options, the search fields resolved to paths. Immutable, shared by all calls. It resolves a dotted name to a `ResolvedPath` |
| `FilterLexer`, `FilterToken` | Tokens with their ranges in UTF-16 units: text, string (with the unescaped value and the places of `\*`), `.`, `(`, `)`, `,`, comparator, minus, white space, end |
| `FilterParser`, `FilterExpr` | Recursive descent with a depth counter over the AIP-160 grammar into a small tree (`And`, `Or`, `Not`, restriction, global value, call, empty), each node with its range |
| `FilterTranslator` | Walks the filter tree and creates the CEL tree with one `CelExprFactory(filter)`. Partial class: `Restrictions` (comparisons, null, wildcards, composite values, search), `Paths` (keys of maps, guards, presence), `Has` (the `:` operator and loops over lists), `Calls` (functions) |
| `FilterValues` | Reads the text of a value by the type of its field into a constant, or into a call of `timestamp`, `duration` or `decimal` with the text as written; gives the reason when it does not fit |
| `FieldRef`, `ResolvedPath`, `PathPlace` | A field of the filter: its parts and types, the keys of the steps through maps, and the ranges of each part in the filter |

## Core members

- `CelExprFactory.CreateError(code, message, subject, expectedType, actualType)` computes the span from `SourceText` and `Position` with the line table of the node positions, so `ToDisplayString` and the places of type-checking errors agree. The stage comes from the range of the code; codes 9000–9999, unknown codes and an empty message throw `ArgumentException`.
- `CelParseResult.FromParsedExpr`, `FromErrors` and `CelCompileResult<T>.FromErrors` wrap the internal constructors; `FromErrors` copies the sequence once and rejects an empty one and a `null` element.
- `CelTimestamp.TryParse` and `CelDuration.TryParse` call the routine that `timestamp(string)` and `duration(string)` use, so the functions and `TryParse` cannot diverge.
- `eng/layers.txt`: `Great.Parts.Cel.Syntax` may use `Great.Parts.Cel.Types`; the package has its own section with one namespace.

## Parser

- Length first: more than 65,536 UTF-16 units or more than 65,536 bytes of UTF-8 is `ExpressionTooLong`, with no lexing. An empty filter or one of white space is `true`.
- Lexing: white space is space, tab, CR, LF. A text is a run of characters other than white space, `.`, `(`, `)`, `,`, `:`, `=`, `<`, `>`, `!`, `"` and `'`, so `*`, `+` and `-` (not first) belong to it. A `-` at the start of a term or an argument is its own token. A string is in `"` or `'`; a backslash makes the next character literal.
- Keywords are a text equal to `AND`, `OR` or `NOT`, except after `.` and in a function name. `AND` and `OR` need white space on both sides and `NOT` after it; otherwise, and at the end of the text, it is a syntax error.
- The grammar functions follow the EBNF one to one. Recursion happens only through `composite` and function arguments; the depth counter stops at 250 with `NestingTooDeep` at the `(`.
- A number with a point in a member (`1.5`, `2.5s`) is one member whose `Source` is the text as written; the translator reads the value from `Source`.
- The first error stops the parse: `FilterSyntaxError` at the token, with a message naming what was expected and what was found.

## Translation

The translation table and the table of values are in the README of the package; the corpus covers the rules of the tables with cases that translate and cases that are an error, not one case for each row (for example, a `decimal` has no field in the test `Item`). Points of the implementation:

- **Fields.** A member on the left is the longest prefix of parts that is a variable, then selections. The type of each step comes from the variable, `CelObjectType.TryGetField`, the value type of a map, or the element type of a list (only on a `:` path). An unknown name keeps the node as written; type checking reports it. A path of more than 250 parts is `NestingTooDeep`; the parts are counted before the names are resolved, for a field and for a member that is an argument of a function, so a member of more than 250 parts is never read as a value.
- **Values** are read by the type of the field: integers and doubles with invariant parsing and a check of the notation; `decimal`, `timestamp` and `duration` as calls with the text (`timestamp` and `duration` are checked with `TryParse`; `decimal` only by the notation, a longer number fails at evaluation); `bytes` as UTF-8; enumerations by constant name through `FieldCatalog`. A value that is a dotted member and not a number is taken from its source text. A function on the right is `InvalidFilterRestriction`.
- **Guards.** For each step the translator decides from the type whether a guard is needed: `x != null` for a variable of an object, wrapper or `dyn` type that is traversed or compared; `has(prefix)` built with `ExpandMacro("has", …)` so that the macro call is recorded; `k in m` for each step through a map and, when the value of the map is a wrapper, an object or `dyn`, `m[k] != null`. A value type of .NET needs no guard. A field of a message type at the top level of a protobuf item reads as an empty message when it is not set, so `x != null` cannot tell not set from empty there; nested fields use `has()` and are exact.
- **The `:` operator.** By the type of the last field: list, `@in` or an `exists` loop; map, `@in` on the key; object, `has(field.<value>)`; a scalar, as `=`; `*`, presence. A list in the middle of the path splits it: the prefix is the range of `exists`, the rest is translated against the loop variable, recursively. The loop variable is `e`, then `e1`, `e2`, … skipping the names of the environment and of enclosing loops. A function on the left, and a value of `dyn` or an unknown field other than `*`, are `InvalidFilterRestriction`.
- **Wildcards.** A string value in quotes with a leading or trailing `*` that is not escaped becomes `endsWith`, `startsWith` or `contains`; a lone `*` is presence; a star elsewhere is a character, and a star in a value that is not `=` or `!=` of a string is `InvalidFilterValue`.
- **Composite values.** `f op (e)` translates `e` with every value `v` replaced by the restriction `f op v`; `NOT` is kept.
- **Balancing.** `AND`, sequences and `OR` are flattened into lists and built as balanced trees with the same split as the parser of CEL, so the tree equals the tree of the CEL text with the same operands and the depth does not grow with a chain.
- **Search.** A value without a field becomes `||` of `contains` over the search fields; none is `InvalidFilterRestriction`.
- **Errors.** `InvalidFilterValue` and `InvalidFilterRestriction` are collected with `CreateError` and the position of the value or comparator; after 100 errors a `TooManyErrors` ends the list. With errors the result is `CelParseResult.FromErrors` and the tree is not type-checked; otherwise `FromParsedExpr(factory.Build(root))`.

## Positions

`factory.Position` is set before each node: the part for `Ident` and `Select`; the comparator for a comparison; the value for constants and conversion calls; the whole field for guards, their `has`, `@in` and `exists`; the value for the `has` of `object:field`, so that `NoSuchField` points at the name; the keyword for `&&` and `||` of `AND` and `OR`; from the start of the left to the end of the right operand for the `&&` of a sequence; `NOT` or `-` for `!`; the function name for a call. An error of type checking therefore points into the filter.

## Errors

| Code | Stage | Raised by | Place |
| :- | :- | :- | :- |
| `ExpressionTooLong` | Parse | the length check | none |
| `NestingTooDeep` | Parse | the depth of parentheses, the length of a path | the `(`, the path |
| `FilterSyntaxError` | Parse | lexer, parser | the token; the end of the text has length 0 |
| `InvalidFilterValue` | Check | `FilterValues`, keys of maps | the value; subject, expected type |
| `InvalidFilterRestriction` | Check | the translator | the comparator, or the global value; subject, actual type |
| `TooManyErrors` | Parse | the translator after 100 errors | none |
| codes of type checking | Check | `CompileParsed<bool>` | the node |

`Compile` returns `CelCompileResult<bool>.FromErrors(parse.Errors)` when `Parse` fails, otherwise the result of `CompileParsed<bool>`.
