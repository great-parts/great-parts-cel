# Environment description in JSON

`CelEnvironmentJson.Apply` in the namespace `Great.Parts.Cel.Configuration` reads an environment description in JSON into a `CelEnvironmentBuilder`:

```text
public static ImmutableArray<CelError> CelEnvironmentJson.Apply(CelEnvironmentBuilder builder, ReadOnlySpan<byte> utf8Json)
```

The format is a subset of the JSON form of the environment configuration of cel-go, the CEL implementation in Go, so that the library and the oracle of the differential run against cel-go read one file with one meaning. `Apply` maps the description one to one onto calls of the builder and makes no decisions of its own: an environment from JSON is the environment that the same calls in code build, and its build errors are those of the same calls.

Not in this version of the page: the extension sets. The catalog of sets is empty until the sets are added, so every `name` in `extensions` is an error, and each set adds its name with its version. The type records `timestamp`, `duration`, `google.protobuf.Timestamp`, `google.protobuf.Duration` and `optional_type<E>` are errors that name the type until the library supports it.

## Reading

| Rule |
| :- |
| The input is UTF-8 text of JSON as RFC 8259 defines it: no comments and no trailing commas. The reader is `Utf8JsonReader` of .NET, so it needs no reflection and works under Native AOT. One UTF-8 byte order mark at the start is skipped. |
| Text that is not UTF-8 — a wrong, truncated or overlong byte sequence, a surrogate, a value above U+10FFFF — is one error `InvalidConfiguration` at the first byte that does not start a well-formed sequence, with the label `environment JSON`. Nothing else of the document is read, and there is no other error. cel-go refuses such text too. |
| A string of JSON, a key or a value, with an escaped lone surrogate — `\uD800`–`\uDFFF` without its pair, a pair in the reverse order — is an error `InvalidConfiguration` at the place of the string: of the key for a key, of the value for a value. It is reported for every such string and the document is read on; the value of a key with such an error is skipped. A well-formed pair is not an error. |
| The reading is strict. An unknown key, a repeated key in one object, a value of the wrong kind (a string instead of an array, a number instead of a string) and a missing required key are errors. Nesting deeper than 64 levels is an error. |
| An error of the reading or of the schema is `InvalidConfiguration` (4009), the stage `Build`, with the place in the JSON text. Every schema error is reported, and the reading goes on with the next key or element; malformed JSON ends the reading at the first malformed token. |
| A value that a builder method would reject as an invalid argument, such as an empty variable name, is an error of the description with its place: the input is data, and errors in it come as results, not as exceptions. |
| The keys of cel-go that this library does not support are an error `InvalidConfiguration` that names the key (see [Keys of cel-go outside the format](#keys-of-cel-go-outside-the-format)). |
| With an error of the reading or of the schema the builder does not change at all: `Apply` applies the whole document or nothing. It reads the whole document into a list of calls first, and makes the calls only when the first phase found no error. |
| The meaning of the keys is that of cel-go v0.32.0, because the library and the oracle read one manifest. |

## Top-level keys

An empty array, an empty object and a missing key are the same: no call of the builder.

| Key | Value | Required | Call of the builder | Errors |
| :- | :- | :- | :- | :- |
| `name` | string | no | none: metadata, the label of the description in errors | a value of the wrong kind is `InvalidConfiguration` |
| `description` | string | no | none: metadata | the same |
| `container` | a full name: identifiers separated by dots | no | `UseContainer(value)` | not a full name is `InvalidConfiguration`. The empty string is the same as no key: no call. A different non-empty container set in code is the build error `ContainerConflict` (4017) with both sources; the same container is one declaration |
| `extensions` | an array of objects, see [extensions](#extensions) | no | `AddExtension` for each element | see below |
| `variables` | an array of objects, see [variables](#variables) | no | `AddVariable(name, type)` for each element | see below |
| `functions` | an array of objects, see [functions](#functions) | no | `AddFunction(CelFunctionDecl)` for each element; the implementations are registered in code by the identifier of the overload | see below |
| `greatPartsCel` | an object, see [greatPartsCel](#greatpartscel) | no | `AddTypes(CelTypeProvider.Create(…))` | see below |

## extensions

| Key | Value | Required | Meaning | Errors |
| :- | :- | :- | :- | :- |
| `name` | string | yes | the short name of a set of the catalog of the library | a name outside the catalog is `InvalidConfiguration` with the name and the place: this includes the names of cel-go that the catalog has no set for, such as `regex`, the full names `cel.lib.ext.*`, and `block`, the internal set of the conformance runner. In this version the catalog is empty, so every name is that error |
| `version` | a string of decimal digits whose value fits `uint` | yes | the version of the set; the set is `AddExtension(CelExtensions.<Set>(version))` | a missing key, an empty string, `latest`, a number of JSON and a text that is not a number are `InvalidConfiguration` with the place |

The name and the version are checked independently, so both can be errors. Build errors that `Build` reports for the calls of `AddExtension`, as for an environment built in code:

| Case | Code |
| :- | :- |
| a version that the set does not have | `UnknownExtensionVersion` (4004) |
| one set in two versions | `ExtensionVersionConflict` (4003) with both sources |
| the same set of the same version twice | none: one declaration |
| a set of the catalog from JSON and an application set made in code with the same name and version | `DuplicateExtension` (4018) with both sources |
| no set on which an added set depends | `MissingExtensionDependency` (4005) with both sets |

The syntax flags of a set, such as `.?`, `[?` and `{?` of `optional`, are enabled by the set; there is no key for them.

## variables

| Key | Value | Required | Meaning | Errors |
| :- | :- | :- | :- | :- |
| `name` | string | yes | the name of the variable | an empty name is `InvalidConfiguration`; a name that is not an identifier or a qualified name is the build error `InvalidVariableName` (4016), as for a declaration in code |
| `type` | a type record, a string, see [Type records](#type-records) | yes, unless there is `type_name` | the CEL type of the variable | see the type records |
| `type_name` | a string: a type name without parameters, without white space | yes, unless there is `type` | the CEL type of the variable, together with `params` | not a type name is `InvalidConfiguration` at the value |
| `params` | an array of type records | no; only with `type_name` | the parameters of `type_name` | `params` without `type_name` is `InvalidConfiguration` |
| `description` | string | no | none: metadata | a value of the wrong kind is `InvalidConfiguration` |

- `type` and `type_name` together are `InvalidConfiguration`: cel-go takes `type_name` silently, and `Apply` makes no decision of its own;
- neither `type` nor `type_name` is `InvalidConfiguration`;
- a type parameter (`~T`) as the type of a variable is `InvalidConfiguration`;
- the build errors of the call `AddVariable` are those of an environment built in code: one name for two different variables is `DuplicateVariable` (4000) with both sources, a name of an identifier of the standard library is `StandardLibraryConflict` (4006), a type that no source declares is `TypeNotDeclared` (4008).

## functions

| Key | Value | Required | Meaning | Errors |
| :- | :- | :- | :- | :- |
| `name` | string | yes | the name of the function | an empty name is `InvalidConfiguration`; a name that is not an identifier or a qualified name, such as the names of operators `_+_` and `@in`, is the build error `InvalidFunctionName` (4015) |
| `description` | string | no | none: metadata | a value of the wrong kind is `InvalidConfiguration` |
| `overloads` | a non-empty array of objects | yes | the overloads | an empty array or no key is `InvalidConfiguration` |
| `overloads[].id` | string | yes | the identifier of the overload; the implementation is the one that the application registered in code under this identifier with `AddFunctionImplementation(id, delegate)` | an empty identifier is `InvalidConfiguration` |
| `overloads[].target` | a type record | no | the type of the receiver: the overload is called as `x.f(…)` | see the type records |
| `overloads[].args` | an array of type records | no; no key is no arguments | the types of the arguments in order, without the receiver | see the type records |
| `overloads[].return` | a type record | yes | the type of the result | no key is `InvalidConfiguration` |
| `overloads[].examples` | an array of strings | no | none: metadata; the strings are not parsed | a value of the wrong kind is `InvalidConfiguration` |

A type record of an overload can have type parameters `~A`–`~Z` (generic overloads). The call is `CelOverloadDecl.Receiver(id, target, args, return)` with a `target`, otherwise `CelOverloadDecl.Global(id, args, return)`.

Implementations are registered in code, before or after `Apply`; a description cannot carry them. The build errors of the call `AddFunction` are those of an environment built in code:

| Case | Code |
| :- | :- |
| an overload is declared and no implementation has its identifier | `MissingImplementation` (4007) |
| an implementation has an identifier that no declaration has | `UnusedImplementation` (4010) |
| the signature of an implementation does not fit the declared overload by the [type mapping](type-mapping.md) | `ImplementationSignatureMismatch` (4011) |
| the signatures of two overloads are the same or overlap after the type parameters are erased | `OverlappingOverloads` (4002) with both sources |
| an overload is the same as or overlaps an overload of the standard library; a function has the name and the form of a macro | `StandardLibraryConflict` (4006) |
| one identifier for two different overloads — in the application, the JSON, the sets or the standard library of the chosen version | `DuplicateOverloadId` (4013) with both sources; a full repeat of an overload is one declaration. The identifier that the delegate form of `AddFunction` makes in code, such as `isInn_string`, is compared with the identifiers of the JSON |
| two implementations with one identifier | `DuplicateImplementation` (4014) |

## greatPartsCel

The object holds what cel-go has no key for: object types with a dictionary carrier. It is the only form of [types described at run time](runtime-types.md) that data can describe without code.

| Key | Value | Required | Call of the builder | Errors |
| :- | :- | :- | :- | :- |
| `greatPartsCel.objectTypes` | an array of objects | no | all the types of the array are one provider: `AddTypes(CelTypeProvider.Create(label, types…))` | — |
| `objectTypes[].name` | string: the full CEL name of the type | yes | `CelObjectTypeBuilder.ForDictionary(name)` | not a full name is `InvalidConfiguration` |
| `objectTypes[].fields` | an array of objects | no | the fields in the order of the array; the number of a field is the order of its declaration | — |
| `fields[].name` | string | yes | `AddField(name, type)` | a field name that is not an identifier, or that repeats another field of the same type, is `InvalidConfiguration` with the place: in code it is the `ArgumentException` of `CelObjectTypeBuilder.AddField` |
| `fields[].type` | a type record, a string | yes | the CEL type of the field | see the type records |

- There are no other keys in `greatPartsCel` and its parts: an unknown key is `InvalidConfiguration`. The description has no enumeration types.
- A field of such a type is present when the dictionary has its key. Reading a field without a value is `FieldNotSet` (3018), and a value of another type than declared is `FieldTypeMismatch` (3019), see [Types described at run time](runtime-types.md).
- One CEL name for two descriptions is the build error `DuplicateType` (4001) with both sources.
- A manifest of the differential run has no `greatPartsCel`: cel-go would skip the object silently and the environments of the two sides would differ, so the run refuses such a manifest.

## Keys of cel-go outside the format

Any key of the table is `InvalidConfiguration` with the name of the key and its place. The format is complete with these exclusions.

| Key | Where | What it does in cel-go | Why it is not here |
| :- | :- | :- | :- |
| `imports` (`name`) | top level | abbreviations of type names | not in the format |
| `stdlib` (`disabled`, `disable_macros`, `include_macros`, `exclude_macros`, `include_functions`, `exclude_functions`) | top level | a subset of the standard library | the version of the standard library is the argument of the constructor of the builder, not a key |
| `context_variable` (`type_name`) | top level | the fields of a protobuf message as variables of the top level | not in the format |
| `validators` (`name`, `config`) | top level | checks of the syntax tree after type checking | not in the format |
| `features` (`name`, `enabled`) | top level | flags of features | not in the format |
| `limits` (`name`, `value`) | top level | limits of parsing and checking | the library sets the [limits](limits.md) itself |
| `is_type_param` | a variable | the type of the variable is a type parameter | in cel-go it is always an error; a type parameter is written `~T` in a type record |
| `type_name`, `params`, `is_type_param` as an object that is a type record (`"type": {"type_name": …}`) | any type record | the object form of a type record | the library takes a type record as a string; in a variable `type_name` and `params` are allowed directly |

Values that cel-go v0.32.0 reads and this library does not:

| Value | Where | In cel-go | Here |
| :- | :- | :- | :- |
| `latest`, an empty string, no key | `extensions[].version` | the latest version; `0` | `InvalidConfiguration` |
| a number of JSON (`"version": 2`) | `extensions[].version` | read as the string `2` | `InvalidConfiguration`: the version is a string |
| the full names `cel.lib.ext.strings` and the like | `extensions[].name` | the same as the short names | `InvalidConfiguration` |
| `regex` | `extensions[].name` | the set `ext.Regex` | `InvalidConfiguration`: the catalog has no such set |

## Type records

The grammar is that of `ParseTypeDesc` of cel-go v0.32.0:

```text
type       = "~" letter | name [ "<" type { "," type } ">" ]
name       = [ "." ] ident { "." ident }
ident      = ( letter | "_" ) { letter | digit | "_" }
letter     = "A"…"Z" | "a"…"z"
```

White space between lexemes — a space, a tab, `\n`, `\r` — is allowed in `type`, in the elements of `params`, in `target`, `args`, `return` and in the `type` of a field. The value of `type_name` is a type name alone, without white space, because cel-go looks it up as it is. A type parameter is one letter. A string that does not follow the grammar, or characters after the type, is `InvalidConfiguration` with the place.

A name is looked up as a full name: the `container` does not apply to it, as in cel-go.

| Record | CEL type | Parameters |
| :- | :- | :- |
| `int`, `uint`, `double`, `bool`, `string`, `bytes` | the simple types | none |
| `null_type`, `null` | `null_type` | none |
| `dyn` | `dyn` | none |
| `bool_wrapper`, `bytes_wrapper`, `double_wrapper`, `int_wrapper`, `uint_wrapper`, `string_wrapper` | the wrappers | none |
| `list<E>` | `list(E)` | exactly 1 |
| `map<K, V>` | `map(K, V)` | exactly 2 |
| `type`, `type<E>` | `type` | 0 or 1 |
| `~A` … `~Z`, `~a` … `~z` | a type parameter; only in a record of an overload | none |
| `any` | `google.protobuf.Any` | none |
| another full name; a leading `.` is dropped | the object type of a source added to the environment, resolved when the environment is built | none |

The errors:

| Case | Code | Where |
| :- | :- | :- |
| the grammar is broken | `InvalidConfiguration` | at the value |
| a wrong number of parameters of `list`, `map` or `type`, or parameters of a name that takes none | `InvalidConfiguration` | at the value |
| a type parameter as the type of a variable or of a field | `InvalidConfiguration` | at the value |
| a name that no source or extension declares | `TypeNotDeclared` (4008) with the name | the build error of the call of the builder, at `Build` |

The name `any` needs the protobuf type provider: without it the name `google.protobuf.Any` is not declared and the record is `TypeNotDeclared`.

## Sources and positions of errors

Every declaration of a description has a source label: `environment JSON '<name>'` with the value of the `name` key, or `environment JSON` without it. The provider of `greatPartsCel` has the same label as its `SourceName`. A build error that names a declaration of the JSON names it by this label, in the place of `type source '…'`.

The position of an error in the JSON text is `CelError.SourceLocation`, a `CelSourceLocation` whose `SourceName` is the label. It is the position of the declaration that the error names first. An error that names no declaration of the JSON has no position, with these exceptions: `MissingImplementation` and `ImplementationSignatureMismatch` carry the position of the overload from the JSON, and `TypeNotDeclared` that of the variable, overload or field from the JSON whose type no source declares, with the label as the source name although their text names no source. For example, `ContainerConflict` between `UseContainer("a")` and `"container": "b"` is `name resolution container 'a' of application conflicts with container 'b' of environment JSON 'm'` at the value `"b"`.

An error of the description has the message `invalid environment description at line {line}, column {column}: {detail}`; the detail is English text that names the key, the value or the type. Its `CelError.Location` is `default`, and its `SourceLocation` is the position in the JSON text. The label is known after the first phase; only text that is not UTF-8 and malformed JSON that ends the reading before the `name` key have the label `environment JSON`.

Positions count UTF-16 code units of the decoded text; the line and the column start at 1; the line breaks are `\n`, `\r\n` and `\r`; the byte order mark is not counted.

| Error | Position of |
| :- | :- |
| an unknown, repeated or unsupported key; a key with an escaped lone surrogate | the key |
| a value of the wrong kind; an empty name or `id`; an invalid container, type name, field name or version; an unknown extension name; an invalid, unsupported or misplaced type record; a repeated field name; a string value with an escaped lone surrogate | the value |
| a missing required key; `type` with `type_name`; `params` without `type_name`; an overload or a function that its constructor rejects | the `{` of the object |
| text that is not UTF-8 | the first byte that does not start a well-formed UTF-8 sequence |
| malformed JSON; nesting that is too deep | the place where `Utf8JsonReader` stopped |

The build errors of the declarations — `DuplicateVariable`, `DuplicateType`, `ContainerConflict`, `TypeNotDeclared`, `MissingImplementation` and the others — are not errors of the description: `Apply` returns no error for them, and `Build` reports them with their sources. When two declarations conflict, the one that sorts first by its source is named first, whatever the order of the calls.

## Run manifest of the differential run

The differential run of the library against cel-go describes the environment of a run in this format, and both sides read the same file: the oracle with `cel.FromConfig` of cel-go, the library with `Apply`.

| Rule |
| :- |
| The manifest is written in this format and read by both sides. |
| The manifest has no `greatPartsCel` and no `validators`: the run refuses such a manifest before it reads it. |
| `block` and `decimal` are not in the manifest: `block` is the internal set of the runner and has no name in a description, and cel-go has no set `decimal`, so the oracle would not read it. |
| `network` is in the manifest under the name `network`. `cel.FromConfig` does not know the name, so the oracle passes its own factory, `ext.Network(ext.NetworkVersion(v))`, with the version from the manifest. |
| The oracle always adds in code, on top of `cel.FromConfig(manifest)`, the validator of regular expression literals (`cel.ValidateRegexLiterals()`) and the flags that the conformance runner of cel-go v0.32.0 sets: `cel.EagerlyValidateDeclarations(true)`, `cel.EnableErrorOnBadPresenceTest(true)` and `cel.EnableIdentifierEscapeSyntax()`. The manifest has no keys for them. |
| The manifest carries no version of the standard library: the library takes it from the constructor of the builder, the oracle uses its own current one. |

## What JSON does not carry

These are set in code, next to `Apply`. A key for them is an unknown key and an error.

| What | How it is set |
| :- | :- |
| the version of the standard library | `new CelEnvironmentBuilder(CelStandardLibrary.V1)` |
| the time zone database | `CelTimeZoneDatabase` and `UseTimeZones` of the builder |
| the implementations of functions | `AddFunctionImplementation(id, delegate)`, before or after `Apply`: they are bound to the overloads by the identifier at `Build`, and the order of the calls does not change the environment |
| the types of .NET from type contexts, and the protobuf provider | `AddTypes` |
| the flag of the `decimal` literal | a method of the builder, on `net11.0` only |

## Differences from cel-go v0.32.0

- cel-go reads the JSON form by the YAML reader (`go.yaml.in/yaml/v3`), not by `encoding/json`. The format here is stricter: the documents that this library accepts are a subset of those that cel-go reads, by their keys and values, apart from the exception below.
- The key names are those of cel-go v0.32.0: `type_name` and `context_variable`.
- cel-go takes a version as a number of JSON and an empty version as `0`, and takes `type_name` silently when `type` is also written; here they are errors.
- cel-go accepts parameters for a type name that takes none: `TypeDesc.Validate` checks the number of parameters only for `list`, `map`, `optional_type` and `type`, and any other name becomes an opaque type with the parameters. Here it is `InvalidConfiguration`.
- go-yaml v3 refuses the whole document when a string has an escaped surrogate, a well-formed pair included, or the characters U+007F, U+0080–U+009F except U+0085, U+FFFE and U+FFFF. Here an escaped lone surrogate is an error, as in cel-go. A well-formed pair and these characters are accepted: in keys, names of variables and type records they are an error anyway, so the difference touches only the metadata `name` and `description`, which do not change the environment.
