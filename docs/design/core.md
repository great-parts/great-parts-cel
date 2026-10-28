# Core of the library

This page defines the core of `Great.Parts.Cel` and lists the paths that belong to it, so that the paths of a change tell whether the change touches the core.

## What the core is

The core is the code that decides the value or the error of an expression by the semantics of CEL:

- type checking — `Great.Parts.Cel.Checking`;
- the planner and the evaluator — `Great.Parts.Cel.Evaluation`;
- the standard library and the extension sets with the implementations of their functions, including `matches`, `decimal` and the time functions — `Great.Parts.Cel.Library` and its sub-namespaces;
- the value rules that evaluation relies on: assignability of types in `Great.Parts.Cel.Types`; CEL equality, ordering, hashing of map keys and conversions of values in `Great.Parts.Cel.Values`.

Not core: the parser and the macros (`Great.Parts.Cel.Syntax`), the public facade, declarations, diagnostics, the environment description in JSON (`Great.Parts.Cel.Configuration`), the value model in `Types` and `Values` outside the rule files, the source generator, the type providers, the `Great.Parts.Cel.Protobuf` package, `eng/` and the tests.

A defect in the core gives a wrong value instead of a visible error. Core changes are therefore also checked against the reference implementation with `eng/difftest`.

## Layout of `src/Great.Parts.Cel`

Each namespace of the assembly is a directory, and the namespace of a file equals its directory. The allowed references between namespaces are the layer table in `eng/layers.txt`; this layout adds no namespace to it.

| Directory | Namespace | Visibility | Core |
| :- | :- | :- | :- |
| `src/Great.Parts.Cel/`, files directly in it | `Great.Parts.Cel`, the facade | public | no |
| `src/Great.Parts.Cel/Configuration/` | `Great.Parts.Cel.Configuration` | public | no |
| `src/Great.Parts.Cel/Diagnostics/` | `Great.Parts.Cel.Diagnostics` | public | no |
| `src/Great.Parts.Cel/Types/` | `Great.Parts.Cel.Types` | public | rule files only |
| `src/Great.Parts.Cel/Values/` | `Great.Parts.Cel.Values` | public | rule files only |
| `src/Great.Parts.Cel/Declarations/` | `Great.Parts.Cel.Declarations` | public | no |
| `src/Great.Parts.Cel/Syntax/` | `Great.Parts.Cel.Syntax` | internal | no |
| `src/Great.Parts.Cel/Checking/` | `Great.Parts.Cel.Checking` | internal | yes |
| `src/Great.Parts.Cel/Library/` | `Great.Parts.Cel.Library` | internal | yes |
| `src/Great.Parts.Cel/Library/Unicode/` | `Great.Parts.Cel.Library.Unicode` | internal, generated code | yes |
| `src/Great.Parts.Cel/Library/Regex/` | `Great.Parts.Cel.Library.Regex` | internal | yes |
| `src/Great.Parts.Cel/Library/Time/` | `Great.Parts.Cel.Library.Time` | internal | yes |
| `src/Great.Parts.Cel/Library/Decimals/` | `Great.Parts.Cel.Library.Decimals` | internal, `net11.0` only | yes |
| `src/Great.Parts.Cel/Evaluation/` | `Great.Parts.Cel.Evaluation` | internal | yes |
| `src/Great.Parts.Cel/PublicAPI/` | public API snapshot files | — | no |

The public catalogs `CelStandardLibrary` and `CelExtensions` belong to the facade: they are handles to tables that live in `Library`.

## Rule files

`Types` and `Values` hold the public model — `CelType`, `CelValue`, collections, type descriptions, attributes — and the internal rules next to it. The rules cannot move to a namespace of their own: public members such as `CelValue.CelEquals` and the key lookup of `CelMap` use them, and a separate namespace would create a reference cycle. They live in their own files instead:

- the name of a rule file ends with `Rules`, optionally followed by a part name: `EqualityRules.cs`, `ConversionRules.Result.cs`;
- a rule file holds internal static classes with rule logic and nothing else, and no other file in `Types` or `Values` holds rule logic;
- a public member that exposes a rule delegates to the rule class and keeps no logic of its own.

Code that a later part of the library adds to `Values` follows the same rule: logic that computes the value or the error of an expression — for example time zone offsets or the text form of `decimal` — goes into a rule file.

| Rule file | Rules | First needed for |
| :- | :- | :- |
| `src/Great.Parts.Cel/Types/AssignabilityRules.cs` | assignability of CEL types | overload candidates of simple types |
| `src/Great.Parts.Cel/Types/SubstitutionRules.cs` | substitution of type parameters | overloads with type parameters |
| `src/Great.Parts.Cel/Values/EqualityRules.cs` | CEL equality | keys of map literals |
| `src/Great.Parts.Cel/Values/KeyHashRules.cs` | hash of map keys, consistent with equality across `int`, `uint` and `double` | keys of map literals |
| `src/Great.Parts.Cel/Values/OrderingRules.cs` | CEL ordering | relations |
| `src/Great.Parts.Cel/Values/ConversionRules.cs` | .NET values to CEL values by the input rows of the type mapping table | bindings of simple types |
| `src/Great.Parts.Cel/Values/ConversionRules.Result.cs` | CEL values to the requested .NET type by the result rows of the type mapping table | results of simple types |

## Core paths

Paths are relative to the repository root, with `/` separators, as git prints them. `*` matches any characters except `/`; `**` matches any number of whole path segments. Matching is case-sensitive.

```text
src/Great.Parts.Cel/Checking/**
src/Great.Parts.Cel/Evaluation/**
src/Great.Parts.Cel/Library/**
src/Great.Parts.Cel/Types/*Rules.cs
src/Great.Parts.Cel/Types/*Rules.*.cs
src/Great.Parts.Cel/Values/*Rules.cs
src/Great.Parts.Cel/Values/*Rules.*.cs
```

A change is a core change when at least one path printed by the following command matches a pattern:

```text
git diff --name-only --no-renames <base>...<head>
```

`--no-renames` prints both the old and the new path of a moved file, so moving a file into or out of the core counts as a core change; a deleted core file counts too. Test projects live under `tests/` and never match.

Moving code across the line — a new rule file outside this naming, a rule in a model file, a new core directory — changes this page in the same change.

## Examples

| Path | Core | Why |
| :- | :- | :- |
| `src/Great.Parts.Cel/Evaluation/Planner.cs` | yes | evaluation |
| `src/Great.Parts.Cel/Checking/Checker.cs` | yes | type checking |
| `src/Great.Parts.Cel/Library/StandardLibraryV1.cs` | yes | standard library |
| `src/Great.Parts.Cel/Library/Regex/Compiler.cs` | yes | sub-namespace of `Library` |
| `src/Great.Parts.Cel/Types/AssignabilityRules.cs` | yes | rule file in `Types` |
| `src/Great.Parts.Cel/Values/EqualityRules.cs` | yes | rule file in `Values` |
| `src/Great.Parts.Cel/Values/ConversionRules.Result.cs` | yes | part of a rule file |
| `src/Great.Parts.Cel/Values/CelValue.cs` | no | value model; `CelEquals` only delegates |
| `src/Great.Parts.Cel/Values/CelList.cs` | no | value model |
| `src/Great.Parts.Cel/Types/CelType.cs` | no | type model |
| `src/Great.Parts.Cel/Syntax/Parser.cs` | no | parser |
| `src/Great.Parts.Cel/CelEnvironment.cs` | no | facade |
| `src/Great.Parts.Cel/Configuration/EnvironmentReader.cs` | no | environment description in JSON |
| `src/Great.Parts.Cel/Diagnostics/CelErrorCode.cs` | no | diagnostics |
| `src/Great.Parts.Cel/PublicAPI/PublicAPI.Unshipped.txt` | no | snapshot |
| `src/Great.Parts.Cel.Protobuf/CelProtobufTypes.cs` | no | other package |
| `tests/Great.Parts.Cel.Tests/Examples/EndToEndTests.cs` | no | tests |
| `eng/layers.txt` | no | `eng/` |

A change that moves `src/Great.Parts.Cel/Values/EqualityRules.cs` to `src/Great.Parts.Cel/Values/Equality.cs` prints both paths with `--no-renames`; the first one matches, so the change is a core change.

Related pages: [facade](facade.md), [syntax](syntax.md), [environment description in JSON](configuration.md).
