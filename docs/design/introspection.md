# Functions of the application, the cache with defaults, introspection and the text of a value

`AddMethod` and `CelFunctionOptions`, descriptions of functions, `CelCompilationCache` with default capacities, the read-only description of an environment, `CelValue.ToString()` and `CelSyntax`. Members and documentation are in the XML documentation of the public API; numbers in `docs/limits.md` ("Caches and accelerations", "Text of a value").

## Functions of the application

`AddMethod<TReceiver, …, TResult>(name, implementation[, options])` is `AddFunction` with the call style "receiver": the first parameter of the delegate is the receiver, so `s.isKpp()` calls `implementation(s)`. Both go to one internal path that builds a `CelOverloadDecl` from the delegate type (the parameter and result types by the input table, as now), sets the receiver style, applies the options and adds the binding (`Declarations/DelegateDeclaration.cs`):

- `options.OverloadId` replaces the derived id; a derived id is formed as now (`{name}_{types}`, with the receiver first for a method: `string_isKpp`). A repeated id is one error `DuplicateOverloadId` (4013) at `Build`, as for any two overloads; the check of the implementations does not add `DuplicateImplementation` for the same id.
- `options.Cost` is the `CelCostFunction` of the overload, as `WithCost` sets it now.
- `options.Description` is stored on the declaration; `CelOverloadDecl.WithDescription(text)` sets it for a declaration made by hand. `null` or empty: no description.

**Descriptions of the library.** Each function of the standard library version 1 and of each extension set gets one English sentence, written from langdef and from the documentation of the set; it ends with a period and does not repeat the signature. The sentences are one table by function name, `Declarations/FunctionDescriptions.cs`, applied to each overload of the function when the tables of the standard library, of the sets of the catalog and of `decimal` are made: a table in `Library` could not serve `decimal`, whose layer does not reference `Library`. The test of the catalog checks that no function of the standard library V1 and of any set version has an empty description.

## The compilation cache with defaults

`new CelCompilationCache(environment)` is `new CelCompilationCache(environment, DefaultMaxEntryCount, DefaultMaxTotalTextLength)`. The cache keeps its entries in a `ConcurrentDictionary` with the clock ring of eviction under one lock, as in version 1. `Count` is the number of kept entries, a value that can be out of date when it is read. `Clear()` and `TryRemove(text)` take the lock of insertions; `TryRemove` removes every entry whose key has the text (ordinal), whatever its `T` and options, and moves the last slot of the ring into each freed one, so after a removal the order of eviction is not the order of insertion (the order of eviction is not specified). A program given out before the removal keeps working. `EvaluateInput<T>` is `GetOrCompile<T>` followed by `EvaluateInput` of the program with the input as `object`, as `Evaluate<T>` does with bindings.

## Introspection

The environment computes its description on the first read of `Variables`, `Functions`, `ObjectTypes` or `EnumTypes`, once (`Checking/EnvironmentDescription.cs`), from the declaration set it already holds, and keeps it in immutable arrays:

| Member | Content | Order |
| :- | :- | :- |
| `Variables` | the variable declarations, the fields of the input included | by name, `string.CompareOrdinal` |
| `Functions` | `CelFunctionDecl` of each function that an expression calls by name: the standard library of the chosen version, the sets, the application; operators and names with `@` excluded | by name; overloads in the order of their ids |
| `ObjectTypes`, `EnumTypes` | the types of the type sources, by CEL name | by name |
| `InputType` | the description of the input or `null` | — |
| `DefaultCostLimit` | the limit of the environment or `null` | — |

The arrays share the declaration objects, which are immutable; no member of them changes the environment. A type that two contexts describe with an equal form is listed once, under the description the index keeps. An environment that nobody asks costs nothing for the description.

## The text of a value

`CelValue.ToString()` writes the value in CEL syntax with an internal writer (`Values/ValueText.cs`), the same for every kind, with a depth counter: at depth 32 a nested list, map, optional or object is written `…`. The builder of the last text of a thread is kept when it did not grow large, so a log line does not allocate one. Strings and bytes use the literal forms of `CelSyntax` (`"…"`, `b"…"` with `\x` escapes for non-ASCII bytes); a lone surrogate, which `QuoteString` refuses, is written as `\u` and four digits; `int` as digits, `uint` with `u`, `double` as `string(double)` writes it with `.0` added to an integral value (`2.0`) and a non-finite value as `double("Infinity")`, `double("-Infinity")`, `double("NaN")`; `timestamp("…")` and `duration("…")` with the texts of `string()`; `decimal` as `string()` writes it plus `m` (`12.34m`, `1E+2m`); `null`; `optional.of(…)` and `optional.none()`; a type as its name; an error value as `error({Code}: {Message})`; a list `[a, b]`, a map `{k: v}` in its iteration order; an object `TypeName{field: value}` with the fields that are present, in the order of the description, read through the description (an exception of a getter passes). The text does not depend on the culture and is not a contract.

An object that leaves an evaluation as `T = CelValue` is paired with the description that the environment has for it, so its text names its CEL type and its fields (`shop.Order{…}`); the instance is the same one. An instance that no description reaches is written by the name of its declared type, or else of its .NET type, with `…` for its fields.

## `CelSyntax`

`QuoteString(value)`: `"` + each code point + `"`; `\\` and `\"` escaped; `\a \b \f \n \r \t \v` for those controls; any other code point below U+0020 and U+007F as `\x` with two upper-case hexadecimal digits (`\x01`, `\x7F`); other code points as they are, a surrogate pair as one; a lone surrogate is `ArgumentException`. `IsIdentifier(value)`: the `IDENT` of langdef — `[_a-zA-Z][_a-zA-Z0-9]*` — and not a reserved word of langdef (`as`, `break`, `const`, `continue`, `else`, `for`, `function`, `if`, `import`, `let`, `loop`, `package`, `namespace`, `return`, `var`, `void`, `while`) and not `true`, `false`, `null`, `in`; `null` is `ArgumentNullException`. The reserved lists are those the lexer uses, one table.

## Tests

The examples (`Examples/Declarations/FunctionOptionsTests.cs`, `Examples/Facade/CompilationCacheDefaultsTests.cs`, `IntrospectionTests.cs`, `Examples/Values/ValueTextTests.cs`, `Examples/Syntax/CelSyntaxTests.cs`) fix each member with named rows. The developer tests: the catalog test of descriptions, `QuoteString` and the parser as a property (any string without a lone surrogate parses back to itself), the kinds of `ToString` that the examples do not write and the depth 32 and 34, `TryRemove` and `Clear` under concurrent insertion, the order of overloads in `Functions`, `AddMethod` with two and three parameters.

Related pages: [declarations](declarations.md), [facade](facade.md), [values](values.md), [library](library.md).
