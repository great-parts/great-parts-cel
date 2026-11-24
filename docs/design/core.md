# Core of the library

This page defines the core of `Great.Parts.Cel` and lists the paths that belong to it, so that the paths of a change tell whether the change touches the core.

## What the core is

The core is the code that decides the value or the error of an expression by the semantics of CEL:

- type checking — `Great.Parts.Cel.Checking`;
- the planner and the evaluator — `Great.Parts.Cel.Evaluation`;
- the standard library and the extension sets with the implementations of their functions, including `matches`, `decimal` and the time functions — `Great.Parts.Cel.Library` and its sub-namespaces;
- the value rules that evaluation relies on: assignability of types in `Great.Parts.Cel.Types`; CEL equality, ordering, hashing of map keys and conversions of values in `Great.Parts.Cel.Values`.

Not core: the parser, the macros, the public tree, the factory of nodes, the printing and the codec of `ParsedExpr` (`Great.Parts.Cel.Syntax`), the codec of `CheckedExpr` (`Checking/Protobuf/`), the public facade, declarations, diagnostics, the environment description in JSON (`Great.Parts.Cel.Configuration`), the value model in `Types` and `Values` outside the rule files, the text of error messages, including the names of types in it (`Values/ValueTypeNames.cs`), the source generator, the type providers, the type descriptions and their builders outside the rule files, the adapters of application collections and of `JsonElement` outside the rule files, the packages `Great.Parts.Cel.Protobuf`, `Great.Parts.Cel.DependencyInjection`, `Great.Parts.Cel.AspNetCore` and `Great.Parts.Cel.Filtering` (`src/Great.Parts.Cel.Filtering/`, which only builds a tree of the core and leaves its meaning to the core), `eng/` and the tests.

A defect in the core gives a wrong value instead of a visible error. Core changes are therefore also checked against the reference implementation with `eng/difftest`.

## Layout of `src/Great.Parts.Cel`

Each namespace of the assembly is a directory, and the namespace of a file equals its directory, with one exception: the subdirectories `Protobuf/` of `Syntax/` and `Checking/` hold the codec of the protobuf messages and keep the namespace of their layer. A namespace of their own would make a cycle with the layer (`ToProtobuf` calls the codec, the codec creates nodes), and the layer table would need rows for it. The allowed references between namespaces are the layer table in `eng/layers.txt`; this layout adds no namespace to it.

| Directory | Namespace | Visibility | Core |
| :- | :- | :- | :- |
| `src/Great.Parts.Cel/`, files directly in it | `Great.Parts.Cel`, the facade | public | no |
| `src/Great.Parts.Cel/Configuration/` | `Great.Parts.Cel.Configuration` | public | no |
| `src/Great.Parts.Cel/Diagnostics/` | `Great.Parts.Cel.Diagnostics` | public | no |
| `src/Great.Parts.Cel/Types/` | `Great.Parts.Cel.Types` | public | rule files only |
| `src/Great.Parts.Cel/Values/` | `Great.Parts.Cel.Values` | public | rule files only |
| `src/Great.Parts.Cel/Declarations/` | `Great.Parts.Cel.Declarations` | public | no |
| `src/Great.Parts.Cel/Syntax/` | `Great.Parts.Cel.Syntax` | public: the tree, the factory; internal: the lexer, the parser, the macros, the printing | no |
| `src/Great.Parts.Cel/Syntax/Protobuf/` | `Great.Parts.Cel.Syntax` | internal | no |
| `src/Great.Parts.Cel/Checking/` | `Great.Parts.Cel.Checking` | public: `CelCheckedExpr`, `CelReference`; internal: the checker | yes |
| `src/Great.Parts.Cel/Checking/Protobuf/` | `Great.Parts.Cel.Checking` | internal | no |
| `src/Great.Parts.Cel/Library/` | `Great.Parts.Cel.Library` | internal | yes |
| `src/Great.Parts.Cel/Library/Unicode/` | `Great.Parts.Cel.Library.Unicode` | internal, generated code | yes |
| `src/Great.Parts.Cel/Library/Text/` | `Great.Parts.Cel.Library.Text` | internal, no dependencies | yes |
| `src/Great.Parts.Cel/Library/Regex/` | `Great.Parts.Cel.Library.Regex` | internal | yes |
| `src/Great.Parts.Cel/Library/Time/` | `Great.Parts.Cel.Library.Time` | internal | yes |
| `src/Great.Parts.Cel/Library/Decimals/` | `Great.Parts.Cel.Library.Decimals` | internal, `net11.0` only | yes |
| `src/Great.Parts.Cel/Library/Extensions/` | `Great.Parts.Cel.Library.Extensions` | internal | yes |
| `src/Great.Parts.Cel/Library/Extensions/Strings/` | `Great.Parts.Cel.Library.Extensions.Strings` | internal | yes |
| `src/Great.Parts.Cel/Library/Extensions/Network/` | `Great.Parts.Cel.Library.Extensions.Network` | internal | yes |
| `src/Great.Parts.Cel/Library/Extensions/Formats/` | `Great.Parts.Cel.Library.Extensions.Formats` | internal | yes |
| `src/Great.Parts.Cel/Evaluation/` | `Great.Parts.Cel.Evaluation` | internal | yes |
| `src/Great.Parts.Cel/PublicAPI/` | public API snapshot files | — | no |

The public catalogs `CelStandardLibrary` and `CelExtensions` belong to the facade: they are handles to tables that live in `Library`.

## Extension sets

The extension sets and their functions are core: they are in `Library/**`, which the paths below already cover, and the new namespaces `Library.Extensions`, `Library.Extensions.Strings` and `Library.Extensions.Network` join `eng/layers.txt` ([extensions](extensions.md#layers)). What stays outside the core: the macros of the sets in `Syntax/ExtensionMacros.cs`, which are parser code ([extension-macros](extension-macros.md)); the model of a set in `Declarations/` ([extensions](extensions.md)); and `Values/AbstractValue.cs`, the base class of the values of an abstract type, which is part of the value model, while the rules that treat the kind `Abstract` — assignability, equality, the fit — are in the existing rule files ([extension-network](extension-network.md#abstract-types)). The public members `CelTypeKind.Abstract` and `CelValueKind.Abstract` are model files, not core.

The set `optional` and the internal set `block` follow the same split ([extension-optional](extension-optional.md), [extensions](extensions.md#the-sets-optional-and-block)). Core: `Library/Extensions/OptionalExtension.cs`, `OptionalFunctions.cs`, `OptionalOperators.cs` and `BlockExtension.cs`, which match `Library/**`; the nodes of the optional syntax and of `or` and `orValue` in `Evaluation/`; the rules of `Checker.cs`; the rows of the kind `Optional` in `AssignabilityRules.cs`, `EqualityRules.cs`, `ValueTypeRules.cs`, `KeyHashRules.cs` and `ConversionRules*.cs`, which are existing rule files, so **no rule file is added**. Not core: `CelOptional.cs` and `OptionalBox.cs` of `Values`, the model of the value, whose members delegate to the rule files and keep no rule of their own; the macros `optMap`, `optFlatMap`, `cel.block`, `cel.index`, `cel.iterVar` and `cel.accuVar` in `Syntax/ExtensionMacros.cs`; the check of the kind `Optional` in `Declarations/DeclarationChecks.cs`, which decides whether a declaration is accepted and not what an expression gives. The only new type of `Values` that holds logic is therefore `OptionalBox`, which has none: one field and a constructor.

## Rule files

`Types` and `Values` hold the public model — `CelType`, `CelValue`, collections, type descriptions, attributes — and the internal rules next to it. The rules cannot move to a namespace of their own: public members such as `CelValue.CelEquals` and the key lookup of `CelMap` use them, and a separate namespace would create a reference cycle. They live in their own files instead:

- the name of a rule file ends with `Rules`, optionally followed by a part name: `EqualityRules.cs`, `ConversionRules.Result.cs`;
- a rule file holds internal static classes with rule logic and nothing else, and no other file in `Types` or `Values` holds rule logic;
- a public member that exposes a rule delegates to the rule class and keeps no logic of its own.

Any code added to `Values` follows the same rule: logic that computes the value or the error of an expression — for example time zone offsets or the text form of `decimal` — goes into a rule file.

| Rule file | Rules | First needed for |
| :- | :- | :- |
| `src/Great.Parts.Cel/Types/AssignabilityRules.cs` | assignability of CEL types, the most general of two types, the join of two types, erasure | overload candidates of simple types |
| `src/Great.Parts.Cel/Types/SubstitutionRules.cs` | substitution of type parameters | overloads with type parameters |
| `src/Great.Parts.Cel/Values/EqualityRules.cs` | CEL equality | keys of map literals |
| `src/Great.Parts.Cel/Values/KeyHashRules.cs` | hash of map keys, consistent with equality across `int` and `uint`; lookup of a key of another numeric kind | keys of map literals |
| `src/Great.Parts.Cel/Values/OrderingRules.cs` | CEL ordering | relations |
| `src/Great.Parts.Cel/Values/ValueTypeRules.cs` | the runtime type of a value; whether a value fits a declared type | `type()`, bindings, results of application functions |
| `src/Great.Parts.Cel/Values/ConversionRules.cs` | .NET values to CEL values by the input rows of the type mapping table | bindings of simple types |
| `src/Great.Parts.Cel/Values/ConversionRules.Result.cs` | CEL values to the requested .NET type by the result rows of the type mapping table | results of simple types |
| `src/Great.Parts.Cel/Values/ConversionRules.Collections.cs` | the collections of the application: conversion of elements and keys, the lookup of a CEL key back to .NET, the order of enumeration | bindings of collections |
| `src/Great.Parts.Cel/Values/JsonRules.cs` | `JsonElement`: kinds, numbers, the counting key lookup, repeated names | bindings of `JsonElement` |
| `src/Great.Parts.Cel/Values/ObjectRules.cs` | the description of an object value and the place rule; the field value of a type described at run time (presence, `FieldNotSet`, `FieldTypeMismatch`); the default equality of objects | objects of the application |
| `src/Great.Parts.Cel/Values/DoubleTextRules.cs` | the shortest digits of a double that read back to the same double, shared by the text form of the library and the printer of syntax trees | `string(double)` and printing of a literal |
| `src/Great.Parts.Cel/Values/TimeTextRules.cs` | RFC 3339 and duration text: reading, printing, the length of the printed text | `timestamp(string)`, `duration(string)`, `string()` of time |
| `src/Great.Parts.Cel/Values/CalendarRules.cs` | the proleptic Gregorian calendar: days and dates, weekday, day of the year | the receivers of `timestamp`, the grammar of RFC 3339 |
| `src/Great.Parts.Cel/Values/TimeZoneRules.cs`, `TimeZoneRules.Tzif.cs`, `TimeZoneRules.Posix.cs` | the resolution of a zone name; reading TZif; the POSIX TZ rule; the offset of a zone at an instant ([time zones](time-zones.md)) | `UseTimeZones`, the receivers with a zone |
| `src/Great.Parts.Cel/Values/TimeZoneNameRules.cs`, `SystemZoneSource.cs` | the check of a zone name by the IANA grammar; the zones of the operating system through `TimeZoneInfo` and the constant of their cost ([time-zones-system](time-zones-system.md)) | `CelTimeZoneDatabase.FromSystem()`, the receivers with a zone |
| `src/Great.Parts.Cel/SimpleEntry/SimpleValues.cs` | a value of the simple entry to its CEL type and its binding ([simple-entry](simple-entry.md)) | `CelExpression.Evaluate` |

## Rule files of `decimal`

The set [decimal](extension-decimal.md) has its files in `Values/` and a directory in `Library/`, compiled for both targets; the core paths below cover the files that hold rules, the platform files and the directory.

| File | Rules | Core |
| :- | :- | :- |
| `src/Great.Parts.Cel/Values/DecimalTextRules.cs` | the reading of a literal and of a string, the rounding to the precision of the target, the text of a value, `double` to a decimal and back ([decimal-text](decimal-text.md)) | yes: `Values/*Rules.cs` |
| `src/Great.Parts.Cel/Values/DecimalPlatform.Decimal128.cs`, `DecimalPlatform.SystemDecimal.cs` | the arithmetic and the cohort of the platform type of the target: the ideal exponent of a quotient and the zeros of IEEE on `net10.0` ([decimal-net10](decimal-net10.md)) | yes: a path below |
| `src/Great.Parts.Cel/Values/DecimalNumberRules.cs` | `int`, `uint` and `System.Decimal` to and from a value | yes: `Values/*Rules.cs` |
| `src/Great.Parts.Cel/Values/DecimalParts.cs` | the struct of sign, coefficient and exponent, and the BID bits; no rule of the language | no: it is the model of the rules, as `CelTimestamp` is the model of the rules of time |
| `src/Great.Parts.Cel/Library/Decimals/**` | the set: the table of overloads, the implementations, the cost functions | yes: `Library/**` |

A defect in a core file is a wrong answer for a decimal and no visible error, but `eng/difftest` cannot see it: cel-go has no `decimal`. Its guard is the exact reference of [tests](tests.md#tests-of-decimal), which a change of these files must pass: it is accepted by the comparison with that reference, and the differential run finds nothing about it by construction.

## Types of 2.0 and the formats

The set [calendar](calendar.md) (`Library/Time/CalendarExtension.cs`, `CalendarFunctions.cs`, `CalendarCosts.cs`), the set [formats](extension-formats.md) (`Library/Extensions/Formats/**`) and the rounding of `decimal` ([decimal-rounding](decimal-rounding.md)) are in `Library/**` and `Values/*Rules.cs`, which the paths below cover: `Values/CalendarTextRules.cs`, `WideRules.cs`, `DecimalRoundingRules.cs`, `ConversionRules.Calendar.cs`, `ConversionRules.Wide.cs`. Not core: the model files `CelValue.Delta.cs`, `CelObjectType.Delta.cs`, `CelType.Delta.cs` and the files of the generator, whose decisions are in the rule files and in [object-literals](object-literals.md).

## Regex and Unicode

`Library/Regex/` and `Library/Unicode/` are internal sub-namespaces of `Library`, so both are core by the paths below: a defect in them is a wrong answer of `matches`, not a visible error, and a change goes through `eng/difftest`. They hold the code of [regex-syntax](regex-syntax.md) and [regex-engine](regex-engine.md) and the generated tables of [unicode-tables](unicode-tables.md).

- **Layers** (`eng/layers.txt`): `Library.Regex` references `Library.Unicode`, `Library.Text`, `Declarations`, `Values` and `Diagnostics`; `Library.Unicode` and `Library.Text`, the search for a substring that `Library` and `Library.Regex` share, reference no namespace of the library; `Library` references all three. `Checking` and `Evaluation` reference neither: they reach the preparation of a pattern through the types of `Declarations` ([declarations](declarations.md), "Constant arguments"), which the library fills in.
- **Generated files.** `src/Great.Parts.Cel/Library/Unicode/*.g.cs` are written by the generator in `eng/unicode/`, which is not core; the files are, since they match `Library/**`, so a regenerated table is checked like a change of the code that reads it. The source checks skip them ([eng-checks-source](eng-checks-source.md#generated-code)).
- **No other directory** is added: the parse, the engine and the cache are files of `Library/Regex/`, and the implementation of the two overloads is `Library/Matches.cs` ([library](library.md)).

## Partial and asynchronous evaluation

The rules that decide a value are core and live in `Evaluation/**`, which the paths below cover: the propagation of unknown values ([partial-evaluation](partial-evaluation.md#unknown-values-in-the-error-channel)), `AttributePlanNode`, `ConditionalAttributePlanNode`, `KeyPlanNode`, `RecordPlanNode`, `PartialPlanner` and `AsyncCallPlanNode` ([async-evaluation](async-evaluation.md#planning)). The unknown payload `Values/UnknownSet.cs` is a model file of `Values`, not a rule file. Not core, since they decide no value of an expression: the driver `AsyncDriver.cs` and `AsyncEvaluation.cs` and the builder of the residual `ResidualSource.cs`, which are files of the root namespace (the facade), because the layer `Evaluation` cannot reference the public result types of the root and the residual needs `Library.Time`; the public `CelAttributePattern`, `CelUnknown` and `CelPartialEvalResult<T>` of the root. A change of the driver is checked by its tests and by Native AOT and `browser-wasm`, not by `eng/difftest`.

## Core paths

Paths are relative to the repository root, with `/` separators, as git prints them. `*` matches any characters except `/`; `**` matches any number of whole path segments. Matching is case-sensitive.

```text
src/Great.Parts.Cel/Checking/*.cs
src/Great.Parts.Cel/Evaluation/**
src/Great.Parts.Cel/Library/**
src/Great.Parts.Cel/Types/*Rules.cs
src/Great.Parts.Cel/Types/*Rules.*.cs
src/Great.Parts.Cel/Values/*Rules.cs
src/Great.Parts.Cel/Values/*Rules.*.cs
src/Great.Parts.Cel/Values/DecimalPlatform.*.cs
src/Great.Parts.Cel/Values/SystemZoneSource.cs
src/Great.Parts.Cel/SimpleEntry/SimpleValues.cs
```

A change is a core change when at least one path printed by the following command matches a pattern:

```text
git diff --name-only --no-renames <base>...<head>
```

`--no-renames` prints both the old and the new path of a moved file, so moving a file into or out of the core counts as a core change; a deleted core file counts too. Test projects live under `tests/` and never match.

Moving code across the line — a new rule file outside this naming, a rule in a model file, a new core directory — changes this page in the same change.

The pattern for `Checking/` names the files directly in the directory, so that `Checking/Protobuf/` is not core: the codec of `CheckedExpr` writes what the checker found and decides no value of an expression ([ast-protobuf](ast-protobuf.md)). The two public files `CelCheckedExpr.cs` and `CelReference.cs` match the pattern and count as core, which is the safe side.

The packages `Great.Parts.Cel.DependencyInjection` and `Great.Parts.Cel.AspNetCore` are not core: their paths match no pattern above, and they use only the public API of `Great.Parts.Cel`, so no part of the core serves them.

## Protobuf messages

The package `Great.Parts.Cel.Protobuf` is not core: `src/Great.Parts.Cel.Protobuf/**` matches no pattern above, and a defect of it is caught by its tests, the conformance cases and the Native AOT publication ([protobuf](protobuf.md)). The part of the core that serves it is small, and none of it needs a new directory:

| Path | Core | What |
| :- | :- | :- |
| `src/Great.Parts.Cel/Types/WellKnownRules.cs` | yes | a new rule file: which full names of protobuf stand for which CEL type ([types](types.md#well-known-names)); `CelType.ObjectNamed` delegates to it |
| `src/Great.Parts.Cel/Types/AssignabilityRules.cs`, `Values/ValueTypeRules.cs` | yes | `Any` as a type that takes every value ([types](types.md#any), [values](values.md#any-and-the-known-types-in-the-value-rules)) |
| `src/Great.Parts.Cel/Values/ObjectRules.cs` | yes | the dispatch of a custom equality and the charge of `FieldReadCost`, [objects](objects.md#members-for-protobuf-messages) |
| `src/Great.Parts.Cel/Checking/*.cs` | yes | the check of a message literal and of the type `Any` ([checking](checking.md#message-literals-of-constructible-types)) |
| `src/Great.Parts.Cel/Evaluation/**` | yes | `MessagePlanNode`, the plan of a literal ([evaluation](evaluation.md#message-literals)) |
| `src/Great.Parts.Cel/Library/Extensions/ProtosExtension.cs` | yes | the catalog row of the set `protos` ([library](library.md#the-set-protos)) |
| `src/Great.Parts.Cel/Syntax/ExtensionMacros.cs` | no | the two macros of `protos`, parser code |
| `src/Great.Parts.Cel/Values/CelObjectType.cs`, `Types/CelType.cs` | no | model files: the two new members of the description and `Any`; their decisions are in the rule files above |
| `src/Great.Parts.Cel/Configuration/TypeRecordResolver.cs` | no | the description in JSON |

## Examples

| Path | Core | Why |
| :- | :- | :- |
| `src/Great.Parts.Cel/Evaluation/Planner.cs` | yes | evaluation |
| `src/Great.Parts.Cel/Checking/Checker.cs` | yes | type checking |
| `src/Great.Parts.Cel/Library/StandardLibraryV1.cs` | yes | standard library |
| `src/Great.Parts.Cel/Library/Regex/Compiler.cs` | yes | sub-namespace of `Library` |
| `src/Great.Parts.Cel/Library/Extensions/Strings/StringFunctions.cs` | yes | sub-namespace of `Library` |
| `src/Great.Parts.Cel/Syntax/ExtensionMacros.cs` | no | parser, the macros of the sets |
| `src/Great.Parts.Cel/Values/AbstractValue.cs` | no | value model; its rules are in the rule files |
| `src/Great.Parts.Cel/Types/AssignabilityRules.cs` | yes | rule file in `Types` |
| `src/Great.Parts.Cel/Values/EqualityRules.cs` | yes | rule file in `Values` |
| `src/Great.Parts.Cel/Values/ConversionRules.Result.cs` | yes | part of a rule file |
| `src/Great.Parts.Cel/Values/CelValue.cs` | no | value model; `CelEquals` only delegates |
| `src/Great.Parts.Cel/Values/CelList.cs` | no | value model |
| `src/Great.Parts.Cel/Values/CelTimestamp.cs` | no | value model; its text and its conversions delegate to `TimeTextRules.cs` |
| `src/Great.Parts.Cel/Values/TimeZoneRules.Tzif.cs` | yes | part of a rule file |
| `src/Great.Parts.Cel/Library/Time/TimeReceivers.cs` | yes | sub-namespace of `Library` |
| `src/Great.Parts.Cel/Values/ObjectRules.cs` | yes | rule file in `Values` |
| `src/Great.Parts.Cel/Values/CelObjectTypeBuilder.cs` | no | type description; its decisions are in `ObjectRules.cs` |
| `src/Great.Parts.Cel/Values/JsonObjectMap.cs` | no | adapter; its decisions are in `JsonRules.cs` |
| `src/Great.Parts.Cel/Types/CelType.cs` | no | type model |
| `src/Great.Parts.Cel/Syntax/Parser.cs` | no | parser |
| `src/Great.Parts.Cel/CelEnvironment.cs` | no | facade |
| `src/Great.Parts.Cel/Configuration/EnvironmentReader.cs` | no | environment description in JSON |
| `src/Great.Parts.Cel/Diagnostics/CelErrorCode.cs` | no | diagnostics |
| `src/Great.Parts.Cel/PublicAPI/PublicAPI.Unshipped.txt` | no | snapshot |
| `src/Great.Parts.Cel.Protobuf/CelProtobufTypes.cs` | no | other package |
| `src/Great.Parts.Cel.DependencyInjection/CelServiceCollectionExtensions.cs` | no | other package |
| `src/Great.Parts.Cel.AspNetCore/CelAuthorizationHandler.cs` | no | other package |
| `tests/Great.Parts.Cel.Tests/Examples/EndToEndTests.cs` | no | tests |
| `eng/layers.txt` | no | `eng/` |

A change that moves `src/Great.Parts.Cel/Values/EqualityRules.cs` to `src/Great.Parts.Cel/Values/Equality.cs` prints both paths with `--no-renames`; the first one matches, so the change is a core change.

Related pages: [facade](facade.md), [syntax](syntax.md), [types](types.md), [values](values.md), [time zones](time-zones.md), [objects](objects.md), [declarations](declarations.md), [environment description in JSON](configuration.md).
