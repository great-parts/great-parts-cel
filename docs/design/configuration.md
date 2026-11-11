# Environment description in JSON

`Great.Parts.Cel.Configuration` reads an environment description in JSON into a `CelEnvironmentBuilder`:

```text
public static ImmutableArray<CelError> CelEnvironmentJson.Apply(CelEnvironmentBuilder builder, ReadOnlySpan<byte> utf8Json)
```

The format is a subset of the JSON form of the environment configuration of cel-go, the CEL implementation in Go, so that the library and the oracle of the differential run read one file with one meaning. `Apply` maps the description one to one onto calls of the builder and makes no decisions of its own: an environment from JSON is the environment that the same calls in code build, and its build errors are those of the same calls.

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Configuration/CelEnvironmentJson.cs` | the public entry point |
| `src/Great.Parts.Cel/Configuration/EnvironmentReader.cs` | reading the document into pending builder calls; a key section with many keys may move to a file of its own |
| `src/Great.Parts.Cel/Configuration/TypeRecordParser.cs` | the type record grammar |
| `src/Great.Parts.Cel/Configuration/JsonPositions.cs` | positions in the JSON text |

## Mapping onto builder calls

An empty array, an empty object and a missing key are the same: no call. Every key not listed is an unknown key.

| Key | Value | Builder call |
| :- | :- | :- |
| `name`, `description` | string, optional | none: metadata; `name` labels the description |
| `container` | a qualified name, or the empty string | `UseContainer(value)`; the empty string is no call |
| `extensions[]` | objects of `name`, `version`, both required | `AddExtension` of the set of that name and version — see [Extension sets](#extension-sets) |
| `variables[]` | objects of `name`, `type` or `type_name` with `params`, `description` | `AddVariable(name, type)` |
| `functions[]` | objects of `name`, `description`, `overloads` (a non-empty array, required) | `AddFunction(CelFunctionDecl)`: one declaration with its overloads in the order of the array, without implementations |
| `functions[].overloads[]` | objects of `id` (non-empty, required), `target`, `args`, `return` (required), `examples` (strings) | `CelOverloadDecl.Receiver(id, target, args, return)` with `target`, else `Global(id, args, return)`; `examples` are metadata |
| `greatPartsCel.objectTypes[]` | objects of `name` (required), `fields` | all types of the array form one provider: `AddTypes(CelTypeProvider.Create(label, …))`, each type `CelObjectTypeBuilder.ForDictionary(name)` |
| `objectTypes[].fields[]` | objects of `name`, `type`, both required | `AddField(name, type)` in the order of the array, which gives the field numbers |

`description` of a variable and of a function, and `examples` of an overload are metadata: the kind of their value is checked, nothing else. Implementations of the overloads are registered in code with `AddFunctionImplementation(id, …)`, before or after `Apply`; a description cannot carry them, nor the standard library version, the time zone database, type sources of .NET types or the `decimal` literal flag.

**Keys of cel-go outside the format** — `imports`, `stdlib`, `context_variable`, `validators`, `features` and `limits` at the top level, `is_type_param` in a variable — are errors that name the key. They stay errors: the format is complete with this part.

**A variable** has exactly one of `type` and `type_name`; `params` only with `type_name`. A name that is not an identifier or a qualified name passes to the builder, which reports `InvalidVariableName` at `Build`, as for code; only an empty name is an error of the description. The same for the name of a function and `InvalidFunctionName`.

## Type records

The grammar of cel-go, read in full: a name, optionally with parameters in angle brackets, or `~` and one letter. White space between lexemes is allowed in `type`, `params`, `target`, `args`, `return` and the `type` of a field; the value of `type_name` is a type name alone, without white space, since cel-go looks it up as it is. A record names its type exactly: the container does not apply to it.

| Record | `CelType` |
| :- | :- |
| `int`, `uint`, `double`, `bool`, `string`, `bytes`, `dyn` | the simple types and `dyn` |
| `null_type`, `null` | `Null` |
| `timestamp`, `duration` | `Timestamp`, `Duration` |
| `int_wrapper`, `uint_wrapper`, `double_wrapper`, `bool_wrapper`, `string_wrapper`, `bytes_wrapper` | `WrapperOf(P)` |
| `list<E>`, `map<K, V>` | `ListOf(E)`, `MapOf(K, V)` |
| `type`, `type<E>` | `TypeOf(Dyn)`, `TypeOf(E)` |
| `~A` … `~Z`, `~a` … `~z` | a type parameter; only in a record of an overload |
| `any` | as the name `google.protobuf.Any` |
| another name; a leading `.` is dropped | `ObjectNamed(name)`, resolved at `Build`; a name no source declares is `TypeNotDeclared` |

The parameter count of `list`, `map` and `type`, and parameters of any other name, are checked; a wrong count is an error. `optional_type<E>` is an error that names the type until its part brings the type. The full names `google.protobuf.Timestamp` and `google.protobuf.Duration` are no records: as any other name that no source declares they are `ObjectNamed`, and `Build` reports `TypeNotDeclared`. A description has no time zone database, so a variable of the type `timestamp` and a receiver with a zone work in an environment from JSON as in code: the database comes from `UseTimeZones`. A type parameter as the type of a variable or a field is an error.

## Extension sets

The name of a set is looked up in a table of the sets that the library implements: the short names of cel-go, such as `strings`, each with its factory in `CelExtensions`. `version` is a string of decimal digits whose value fits `uint` (`latest`, the empty string and a JSON number are errors); the set is `AddExtension(CelExtensions.<Set>(version))`, and an unknown version is `UnknownExtensionVersion` at `Build`. The name and the version are checked independently, so both can be errors.

In this part the table is empty: every name is an error at the value of `name`, and no description reaches `AddExtension`. Each part that brings a set adds its row. Names outside the catalog of the library stay errors in every part: `regex`, the full names `cel.lib.ext.*`, and `block`, the internal set of the runner, which has no name in a description.

## The sets of the catalog in a description

From the part of the extension sets on, the table of names of "Extension sets" is the catalog of [extensions](extensions.md#the-catalog): `strings`, `math`, `lists`, `sets`, `encoders`, `bindings`, `two-var-comprehensions` and `network`. The reader does not keep a list of its own: `EnvironmentReader.Extensions.cs` takes the names from `ExtensionCatalog.Names` and makes the call `AddExtension(CelExtensions.<Set>(version))` through an internal member of the facade, `CelExtensions.Create(name, version)`, that finds the factory of the name in the same catalog; a part that adds a set to the catalog thereby adds its name to descriptions and needs no change here. The names `optional` and `protos` stay errors at the value of `name` until their sets are in the catalog; `block`, `regex`, `decimal` and the full names `cel.lib.ext.*` stay errors for good.

- **Reading.** The name is looked up first, the version is read as a string of decimal digits whose value fits `uint`, and each is an error of its own at its value; an element is a call only when both are valid. The call carries the label of the description and the position of the `{` of the element, like a variable or a function ([Sources of declarations](#sources-of-declarations)).
- **The version is not checked while reading.** `strings` with `"version": "7"` reads without error, and `Build` reports `UnknownExtensionVersion` (4004), `extension 'strings' has no version 7; available versions: 5`, at the `{` of that element. `ExtensionVersionConflict` (4003) is at the element of the smaller version, whether the other comes from the description or from code; `MissingExtensionDependency` (4005) is at the element of the set that needs the other; `DuplicateExtension` (4018), which a catalog set from a description gets only against a set built by `CelExtension.Create`, is at the element of the description, and its sources are `application` and `extension <name> v<version>`. An error of the application set with no element in the text has no position.
- **Repeats.** Two elements with one name and one version are one set, as in code. The order of the elements does not matter: the set of sets, and so the environment and the errors, are the same for every order, and a test permutes them.
- **The same state as code.** The internal member `Extensions` (`ImmutableArray<CelExtension>`, ordered by name and then by version) of `CelEnvironmentBuilder` joins the members that the tests compare after `Apply` and after the equal calls in code.
- **The manifest of the differential run** may now carry `extensions`, with the names and versions of the sets of [eng-difftest](eng-difftest.md#profile-of-the-extension-sets); the oracle builds `network` with its own factory, the library with the catalog.

## Sources of declarations

Every call of `Apply` passes the source of each declaration through internal overloads of the builder methods: the label of the description — `environment JSON '<name>'` with the value of the `name` key, or `environment JSON` without it — and the position in the JSON text of the `{` of the element that declared it: a variable, a function, an overload, an object type, a field; for the container, the value. The provider of `greatPartsCel` gets the label as its `SourceName`, and its types and fields the positions of their elements.

A build error names a JSON declaration by its label, the provider of `greatPartsCel` included (not as `type source '…'`), in the order of sources of [declarations](declarations.md); where that order ties — two declarations of one label with one name or identifier — the one earlier in the text comes first. Its `CelError.SourceLocation` is the position of the JSON declaration it names first; an error that names no JSON declaration has none, except that `MissingImplementation` (4007) and `ImplementationSignatureMismatch` (4011) carry the position of the overload from the JSON that has no implementation or that the implementation does not fit, and `TypeNotDeclared` (4008) that of the variable, overload or field from the JSON whose type no source declares, with the label as the source name although their text names no source; `TypeNotDeclared` of a declaration in code, `UnusedImplementation` (4010) and `DuplicateImplementation` (4014) have none (the error texts do not change). So `ContainerConflict` between `UseContainer("a")` and `"container": "b"` is `name resolution container 'a' of application conflicts with container 'b' of environment JSON 'm'` at the value `"b"`, and `DuplicateType` of two object types of one description names the label twice. `ToBuilder` keeps the sources.

**Same state as code.** After `Apply` the builder holds the same declarations as after the equal calls in code: tests compare them through the internal members `VariableDeclarations`, `FunctionDeclarations` (`ImmutableArray<CelFunctionDecl>`), `TypeProviders` (`ImmutableArray<CelTypeProvider>`) and `Container` (the empty string without one) of `CelEnvironmentBuilder` — variables by name and type, functions by name and overload declarations, providers by source name and the name, number and type of each field. Declarations are sets, so the order of the calls does not matter.

## Reading

`Apply` reads in two phases. The first phase reads the whole document with `Utf8JsonReader` into a list of pending calls and never touches the builder; it checks every condition of the builder methods and of the constructors it will call, so the second phase, which runs only when the first found no errors and makes the calls, cannot throw. When the description has an error, the builder does not change at all.

- JSON as RFC 8259 defines it: the default options of `Utf8JsonReader`, no comments or trailing commas, nested no deeper than 64 levels. One UTF-8 byte order mark at the start is skipped. Text that is not UTF-8 is a single error at the first byte that does not start a well-formed UTF-8 sequence, and nothing else is read.
- The reader keeps the key names of each object. An unknown key, a repeated key, a value of the wrong kind — a JSON number for `version`, an object as a type record — and a missing required key are errors.
- A value that a builder method or a constructor would reject as an invalid argument is an error of the description: an empty variable or function name, an empty overload `id`, a `container` or an object type name that is not a qualified name, a field name that is not an identifier or that repeats one of the same type, an overload that `CelOverloadDecl` or a function that `CelFunctionDecl` rejects.
- A string — a key or a value — with an escaped lone surrogate is an error at the string; the value of such a key is skipped. An escaped surrogate pair is not an error.
- Every schema error is reported, and reading goes on with the next key or element. Malformed JSON ends reading at the first malformed token.

## Errors and positions

Every error of the description has the code `InvalidConfiguration` (4009), the stage `Build` and the message `invalid environment description at line {line}, column {column}: {detail}`; the detail is English text that names the key, the value or the type. `CelError.Location` is `default`. `CelError.SourceLocation` is the position in the JSON text, with the label as its source name. Schema errors get the label after the first phase; only text that is not UTF-8 and malformed JSON that ends reading before the `name` key get `environment JSON`.

Positions count UTF-16 code units of the decoded text; the line and the column start at 1; the line breaks are `\n`, `\r\n` and `\r`; the byte order mark is not counted. `JsonPositions` turns a byte index of `Utf8JsonReader` into a position with the `LineTable` of the parser ([syntax](syntax.md)), in the same pass for an element that declares something, since a build error needs the position later, and when the error is found for an error of the description.

| Error | Position of |
| :- | :- |
| unknown, repeated or unsupported key; key with an escaped lone surrogate | the key |
| value of the wrong kind; empty name or `id`; invalid container, type name, field name or version; unknown extension name; invalid, unsupported or misplaced type record; repeated field name; string value with an escaped lone surrogate | the value |
| missing required key; `type` with `type_name`; `params` without `type_name`; an overload or a function that its constructor rejects | the `{` of the object |
| text that is not UTF-8 | the first byte that does not start a well-formed UTF-8 sequence |
| malformed JSON, too deep nesting | the place where `Utf8JsonReader` stopped |

Build errors of the declarations — `DuplicateVariable`, `DuplicateType`, `ContainerConflict`, `TypeNotDeclared`, `MissingImplementation` and the rest — are not errors of the description: `Apply` returns no error for them, and `Build` reports them with their sources as above.

## Run manifest of the differential run

`eng/difftest` describes the environment of a run in this format, and both sides read the same file: the oracle with `cel.FromConfig` of cel-go, the library with `Apply`. On the library side the stand creates `new CelEnvironmentBuilder(CelStandardLibrary.V1)` — the latest version, since the manifest carries none — calls `Apply` and then `Build`. A manifest has neither `greatPartsCel`, which cel-go would skip, nor `validators`; the stand rejects such a manifest itself, before `Apply`. The oracle adds in code the flags and the validator of the cel-go conformance runner ([eng-oracle.md](eng-oracle.md)), and its own factory for the set `network`. A manifest holds `name`, `description`, `container`, `variables` and, from the part of extension sets on, `extensions`; `functions` would need the same implementations on both sides, so the profile has none ([eng-difftest.md](eng-difftest.md#profile)).

Related pages: [facade](facade.md), [declarations](declarations.md), [objects](objects.md), [core](core.md).
