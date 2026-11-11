# API concepts

This page lists every public type of the `Great.Parts.Cel` and `Great.Parts.Cel.Protobuf` assemblies. For each type it names the cel-spec concept the type represents, or the purpose it serves when the type has no counterpart in cel-spec, and explains how the name differs from the cel-spec term.

cel-spec sources:

- the language definition, `doc/langdef.md`, cited by section title;
- `checked.proto`, `syntax.proto`, `value.proto` and `eval.proto` from `proto/cel/expr/`.

The term "cel-go" refers to the CEL implementation in Go.

How to read the table:

- the full name of a type is the namespace, a dot and the type name, for example `Great.Parts.Cel.CelCompileResult<T>`. Code formatting is not part of the name;
- generic type parameters are written as in the C# declaration;
- the set of public types is the same for `net10.0` and `net11.0`: on .NET 11 the `decimal` type adds members to existing types, not new types;
- the build checks this page against the assemblies: a public type without a row, a row without a public type, or a row with an empty cell is a build error.

| Type | Namespace | cel-spec concept or requirement | Name and difference from the term |
| :- | :- | :- | :- |
| `CelEnvironment` | `Great.Parts.Cel` | langdef "Evaluation Environment": declarations, function bindings, name resolution container. Immutable; compiles any number of expressions | cel-spec term "environment" with the `Cel` prefix |
| `CelEnvironmentBuilder` | `Great.Parts.Cel` | Purpose: describes an environment in code (variables, functions with overloads and .NET implementations, types, extensions with explicit versions) and builds an immutable environment, reporting all conflicts at once | Not a cel-spec concept: the builder of an immutable `CelEnvironment` |
| `CelBuildResult` | `Great.Parts.Cel` | Purpose: the outcome of building an environment, either the environment or build errors; an error about a conflict names both conflicting sources | Not a cel-spec concept: the result of `CelEnvironmentBuilder.Build()` |
| `CelStandardLibrary` | `Great.Parts.Cel` | langdef "Standard Definitions", "Standard Environment": the standard library, selected by version | cel-spec term "standard library"; versions are specific to this library, cel-spec does not version the standard library |
| `CelExtensions` | `Great.Parts.Cel` | langdef "Extension Functions": the catalog of the built-in extension sets, each requested with an explicit version | Not a cel-spec term: a catalog of `CelExtension` instances |
| `CelCompileOptions` | `Great.Parts.Cel` | Purpose: compilation parameters: the expected CEL type of the result, the source name and the start position of the expression in its source document | Not a cel-spec concept: compilation parameters |
| `CelCompileResult<T>` | `Great.Parts.Cel` | Purpose: the outcome of compiling an expression (parse, type-check and plan), either a program or all errors found, each with a location | Not a cel-spec concept: the result of `CelEnvironment.Compile<T>` |
| `CelProgram<T>` | `Great.Parts.Cel` | `checked.proto` `CheckedExpr`: a type-checked expression bound to an environment and to function implementations, ready to evaluate; reports the CEL type of its result | "Program" is the cel-go term for a checked expression ready to evaluate |
| `CelBindings` | `Great.Parts.Cel` | langdef "Evaluation Environment": the binding context that binds identifiers to values; the `bindings` field of cel-spec conformance tests | cel-spec term "bindings"; cel-go calls it "activation" |
| `CelEvalOptions` | `Great.Parts.Cel` | Purpose: evaluation parameters, including the cost limit that bounds both time and allocated memory | Not a cel-spec concept: evaluation parameters |
| `CelEvalResult<T>` | `Great.Parts.Cel` | `eval.proto` `ExprValue`: a value or an error. Holds the result converted to the requested .NET type, or the errors of compilation or evaluation | Not a cel-spec term: the result of an evaluation |
| `CelCompilationCache` | `Great.Parts.Cel` | Purpose: a bounded, thread-safe cache of compilations created explicitly by the application from an environment, for the one-step evaluation of expression text | Not a cel-spec concept: a compilation cache |
| `CelVariableDecl` | `Great.Parts.Cel.Declarations` | `checked.proto` `Decl.IdentDecl`: an identifier declaration with its type | langdef calls declared identifiers "variables"; `checked.proto` calls the declaration `IdentDecl` |
| `CelFunctionDecl` | `Great.Parts.Cel.Declarations` | `checked.proto` `Decl.FunctionDecl`: a function declaration with its overloads | Same as the cel-spec term, with the `Cel` prefix |
| `CelOverloadDecl` | `Great.Parts.Cel.Declarations` | `checked.proto` `Decl.FunctionDecl.Overload`: an overload with its identifier, parameter types, result type and receiver style | Same as the cel-spec term, with the `Cel` prefix |
| `CelFunctionImplementation` | `Great.Parts.Cel.Declarations` | langdef "Functions": the computation of an overload. Delegate that implements an overload in .NET | cel-spec does not name the implementation of an overload; "implementation" is the .NET code bound to an overload |
| `CelCostFunction` | `Great.Parts.Cel.Declarations` | Purpose: a delegate that declares the evaluation cost of an application function in cost units | Not a cel-spec concept: cost of an application function |
| `CelExtension` | `Great.Parts.Cel.Declarations` | langdef "Extension Functions": an extension set with a name and a version, its function declarations with implementations, macros, syntax flags and dependencies on other extension sets | cel-spec term "extension"; one instance is one version of one extension set |
| `CelValue` | `Great.Parts.Cel.Values` | `value.proto` `Value`: a CEL value; an error value follows `eval.proto` | Same as the cel-spec term, with the `Cel` prefix |
| `CelValueKind` | `Great.Parts.Cel.Values` | `value.proto` `Value.kind`: the kind of a value | Differs from `Value.kind`: adds the kinds `error`, `optional` and `abstract`, the kind of a value of an abstract type of an extension set such as `net.IP`; has separate kinds `timestamp` and `duration`, which `value.proto` carries as messages in `object_value`; has no `enum` kind, an enumeration value is an `int`. New kinds may be added, such as `decimal` on .NET 11 |
| `CelList` | `Great.Parts.Cel.Values` | `value.proto` `ListValue`; langdef type `list` | Same as the cel-spec term, with the `Cel` prefix |
| `CelMap` | `Great.Parts.Cel.Values` | `value.proto` `MapValue`; langdef type `map` | Same as the cel-spec term, with the `Cel` prefix |
| `CelTimestamp` | `Great.Parts.Cel.Values` | CEL type `timestamp`, `google.protobuf.Timestamp`: a point in time with nanosecond precision | Same as the cel-spec term, with the `Cel` prefix |
| `CelDuration` | `Great.Parts.Cel.Values` | CEL type `duration`, `google.protobuf.Duration`: a time span with nanosecond precision | Same as the cel-spec term, with the `Cel` prefix |
| `CelOptional<T>` | `Great.Parts.Cel.Values` | `optional_type(T)` of the optional extension (cel-spec test data `optionals.textproto`): an optional value that distinguishes `optional.none()` from `optional.of(null)` | cel-spec term "optional" with the `Cel` prefix |
| `CelObjectType` | `Great.Parts.Cel.Values` | `checked.proto` `Type.message_type`; langdef "Name Resolution" (message names): the description of an object type and its fields. Abstract; derived types come from generated code, from `CelObjectTypeBuilder` and from the protobuf package | cel-spec says "message type"; here the concept covers .NET types and types described at run time, not only protobuf messages |
| `CelObjectField` | `Great.Parts.Cel.Values` | A field of a message type (`checked.proto` `Type.message_type`; langdef "Field Selection"): name, CEL type and field number | cel-spec says "field"; "object" follows `CelObjectType` |
| `CelEnumType` | `Great.Parts.Cel.Values` | langdef "Protocol Buffer Data Conversion": enumeration types, whose values are `int`. .NET enumerations are read the same way | Same as the cel-spec term "enum type", with the `Cel` prefix |
| `CelTypeProvider` | `Great.Parts.Cel.Values` | Purpose: a named source of object and enumeration types that the application adds to an environment; the source name appears in build errors | Not a cel-spec concept; cel-go uses the term "type provider" |
| `CelTypeContext` | `Great.Parts.Cel.Values` | Purpose: the base class of a type context, a partial class in which the application lists the .NET types available to expressions and the source generator describes them at build time | Not a cel-spec concept: the base of a generated type context, in the style of `JsonSerializerContext` in System.Text.Json |
| `CelObjectAttribute` | `Great.Parts.Cel.Values` | Purpose: declares a .NET type, including a type from another assembly, as available to expressions in a type context | Not a cel-spec concept: the attribute `[CelObject]` |
| `CelTypeContextOptionsAttribute` | `Great.Parts.Cel.Values` | Purpose: options of a type context, such as the field naming policy and whether System.Text.Json attributes are taken into account | Not a cel-spec concept: type context options |
| `CelNamingPolicy` | `Great.Parts.Cel.Values` | Purpose: the rule that turns .NET member names into CEL field names: camel case (the default), as is, or lower snake case | Not a cel-spec concept: naming policy, in the style of `JsonNamingPolicy` |
| `CelIgnoreAttribute` | `Great.Parts.Cel.Values` | Purpose: hides a member of a .NET type from expressions | Not a cel-spec concept: the attribute `[CelIgnore]`; equivalent to the cel-go struct tag `cel:"-"` |
| `CelNameAttribute` | `Great.Parts.Cel.Values` | Purpose: sets the CEL field name of a member of a .NET type | Not a cel-spec concept: the attribute `[CelName]`; equivalent to the cel-go struct tag `cel:"name"` |
| `CelObjectTypeBuilder<TCarrier>` | `Great.Parts.Cel.Values` | Purpose: describes an object type at run time, without a .NET type, over any carrier class of the application, with delegates that read a field and test its presence | Not a cel-spec concept: the builder of a `CelObjectType` over carrier `TCarrier` |
| `CelObjectTypeBuilder` | `Great.Parts.Cel.Values` | Purpose: entry point for run-time object types; describes a type over a dictionary carrier `IReadOnlyDictionary<string, object?>` | Not a cel-spec concept: the non-generic companion of `CelObjectTypeBuilder<TCarrier>` |
| `CelTimeZoneDatabase` | `Great.Parts.Cel.Values` | langdef "Timezones" (`LongTZ`): the IANA time zone database, built from TZif data that the application passes with `CelEnvironmentBuilder.UseTimeZones`; the library itself reads no files, operating system data or environment, so without a database only `UTC` and fixed offsets are time zone names (see [Time zone database](recipes/time-zone-database.md)) | Not a cel-spec concept: the time zone database supplied by the application |
| `CelType` | `Great.Parts.Cel.Types` | `checked.proto` `Type`: a CEL type | Same as the cel-spec term, with the `Cel` prefix |
| `CelTypeKind` | `Great.Parts.Cel.Types` | `checked.proto` `Type.type_kind`: the kind of a CEL type | Same as the cel-spec term, with the `Cel` prefix |
| `CelError` | `Great.Parts.Cel.Diagnostics` | `eval.proto` `Status` and `ErrorSet`; langdef "Runtime Errors": an error with a code, a stage, an English message, a location and the original exception, if any | One shape for errors of all stages: build, parse, check and evaluation |
| `CelErrorCode` | `Great.Parts.Cel.Diagnostics` | Purpose: the published list of error codes with explicit numbers; each error has one code | Not a cel-spec concept; includes the langdef runtime errors `no_matching_overload` and `no_such_field` |
| `CelErrorStage` | `Great.Parts.Cel.Diagnostics` | Purpose: the stage at which an error occurred: build, parse, check or evaluate | Not a cel-spec concept: error stage |
| `CelSourceLocation` | `Great.Parts.Cel.Diagnostics` | `syntax.proto` `SourceInfo` (positions and line offsets): offset from the start of the text, line and column in UTF-16 code units | cel-spec term "source info"; here one location, not the whole table |
| `CelException` | `Great.Parts.Cel.Diagnostics` | Purpose: the exception thrown by the `Get…OrThrow()` methods of an unsuccessful result; carries the list of errors | Not a cel-spec concept: errors are returned as results, this exception exists only for `Get…OrThrow()` |
| `CelEnvironmentJson` | `Great.Parts.Cel.Configuration` | Purpose: reads an environment description from JSON (variables, types, extensions with versions, function declarations with implementations registered in code) into a `CelEnvironmentBuilder`; the format is a subset of the cel-go environment configuration format | Not a cel-spec concept: the JSON reader of an environment description |
| `CelProtobufTypes` | `Great.Parts.Cel.Protobuf` | langdef "Protocol Buffer Data Conversion", "Dynamic Values": a type provider for protobuf messages built from file descriptors | Not a cel-spec term: the provider of protobuf message types |
| `CelProtobufTypesBuilder` | `Great.Parts.Cel.Protobuf` | Purpose: builds a `CelProtobufTypes` provider from `.proto` file descriptors and typed proto2 extensions | Not a cel-spec concept: the builder of `CelProtobufTypes` |
