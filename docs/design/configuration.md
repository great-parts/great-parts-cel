# Environment description in JSON

`Great.Parts.Cel.Configuration` reads an environment description in JSON into a `CelEnvironmentBuilder`:

```text
public static ImmutableArray<CelError> CelEnvironmentJson.Apply(CelEnvironmentBuilder builder, ReadOnlySpan<byte> utf8Json)
```

The format is a subset of the JSON form of the environment configuration of cel-go, the CEL implementation in Go, so that the library and the oracle of the differential run read one file with one meaning. `Apply` maps the description one to one onto calls of the builder and makes no decisions of its own: an environment from JSON is the environment that the same calls in code build.

The walking skeleton reads the top-level keys `name`, `description` and `variables`, and variables of simple types and `dyn`. The rest of the format comes later; until then its keys are errors (see below).

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Configuration/CelEnvironmentJson.cs` | the public entry point |
| `src/Great.Parts.Cel/Configuration/EnvironmentReader.cs` | reading the document into pending builder calls |
| `src/Great.Parts.Cel/Configuration/TypeRecordParser.cs` | the type record grammar |
| `src/Great.Parts.Cel/Configuration/JsonPositions.cs` | positions in the JSON text |

## Mapping onto builder calls

| Key | Value | Builder call |
| :- | :- | :- |
| `name` | string, optional | none: metadata; it names the description in errors |
| `description` | string, optional | none: metadata |
| `variables` | array of objects, optional | `AddVariable(name, type)` for each element, in the order of the array |
| `variables[].name` | non-empty string, required | the name of the variable |
| `variables[].type` | type record, a string | the `CelType` of the variable |
| `variables[].type_name` | type name, a string | the `CelType` of the variable, with `params` |
| `variables[].params` | array of type records, only with `type_name` | the type parameters |
| `variables[].description` | string, optional | none: metadata |

An empty array is the same as a missing key. A variable has exactly one of `type` and `type_name`.

The type record grammar is the one of cel-go, read in full: a name, optionally with parameters in angle brackets, or `~` and one letter for a type parameter. The walking skeleton maps the six simple names and `dyn`: `int` to `CelType.Int`, `uint` to `CelType.UInt`, `double` to `CelType.Double`, `bool` to `CelType.Bool`, `string` to `CelType.String`, `bytes` to `CelType.Bytes` and `dyn` to `CelType.Dyn`; `params` must then be missing or empty. Any other well-formed record is an error that names the type until the reader supports it. A type parameter as the type of a variable is always an error.

The value of `type_name` is a type name alone — identifiers separated by dots, optionally after a leading dot — with no white space anywhere: cel-go looks the name up as it is, without parsing it, so ` int` is not `int` there. White space between lexemes is allowed only in type records: in `type` and in the elements of `params`.

**Same state as code.** For a description with the variables `v1` … `vn`, the builder holds the same declarations as after `AddVariable(v1.name, v1.type)` … `AddVariable(vn.name, vn.type)` in code: the same names with equal types. Declarations are a set, so the order of the calls does not matter. Tests compare the states through the internal member `CelEnvironmentBuilder.VariableDeclarations`, an `ImmutableArray<CelVariableDecl>` of the declarations collected so far, by the pairs of name and type.

The source of a declaration — the label of the description and the position of the element in the JSON text — reaches the builder together with the build errors that name sources. In the walking skeleton `Build` reports no errors about declarations, so `Apply` calls the public method.

## Reading

`Apply` reads in two phases. The first phase reads the whole document with `Utf8JsonReader` and turns it into a list of pending builder calls; it never touches the builder. The second phase runs only when the first one found no errors, and makes the calls. So when the description has an error, the builder does not change at all.

- The reader uses the default options of `Utf8JsonReader`: JSON as RFC 8259 defines it, without comments or trailing commas, nested no deeper than 64 levels. One UTF-8 byte order mark at the start is skipped. Before reading, the whole text is checked to be UTF-8, as RFC 8259 requires: text that is not UTF-8 is a single error at the first byte that does not start a well-formed UTF-8 sequence, and nothing else is read or reported. cel-go rejects such text too.
- The reader keeps the key names of each object it reads. An unknown key, a repeated key, a value of the wrong kind, a missing required key, `type` together with `type_name`, and `params` without `type_name` are errors.
- Keys of the format that the walking skeleton does not read yet — `container`, `extensions`, `functions` and `greatPartsCel` — are errors that name the key until the reader supports them. Keys of cel-go that the library does not support at all — `imports`, `stdlib`, `context_variable`, `validators`, `features` and `limits` at the top level, `is_type_param` in a variable — are always errors that name the key.
- Every schema error is reported, and reading goes on with the next key or element. A document that is not well-formed JSON ends reading at the first malformed token.
- A value that a builder method would reject as an invalid argument, such as an empty variable name, is an error of the description, not an exception.
- A string — a key or a value — with an escaped lone surrogate (`\uD800` to `\uDFFF` without its pair) is an error at the string, as in cel-go, which rejects such a document. `Utf8JsonReader` reads past such a string but cannot decode it, so the reader reports the error, skips the value of such a key, and goes on. An escaped surrogate pair is not an error.

## Errors and positions

Every error has the code `InvalidConfiguration` (4009), the stage `Build` and the message `invalid environment description at line {line}, column {column}: {detail}`. `CelError.Location` is `default`: the error has no position in an expression. `CelError.SourceLocation` is the position in the JSON text, and its source name is the label of the description: `environment JSON '<name>'` with the value of the `name` key, or `environment JSON` when the key is missing. Schema errors get the label after the first phase, so all of them carry the same label; only text that is not UTF-8 and malformed JSON that ends reading before the `name` key get `environment JSON`.

Positions use the units of positions in an expression: the offset, the line and the column count UTF-16 code units of the decoded JSON text, the line and the column start at 1, the line breaks are `\n`, `\r\n` and `\r`, and the byte order mark is not counted.

| Error | Position of |
| :- | :- |
| unknown, repeated, unsupported or not yet supported key; key with an escaped lone surrogate | the key |
| value of the wrong kind, empty name, invalid or unsupported type record, string value with an escaped lone surrogate | the value |
| missing required key; `type` with `type_name`; `params` without `type_name` | the `{` of the object |
| text that is not UTF-8 | the first byte that does not start a well-formed UTF-8 sequence |
| malformed JSON, too deep nesting | the place where `Utf8JsonReader` stopped |

`Utf8JsonReader` gives byte indexes. `JsonPositions` turns a byte index into a position: it decodes the bytes before the index, after the byte order mark, and computes the line and the column with the same `LineTable` as the parser (see [syntax](syntax.md)). For malformed JSON the exception of the reader gives a line counted by `\n` and a byte position in that line; `JsonPositions` turns them into a byte index first. Positions are computed only when there is an error.

## Run manifest of the differential run

`eng/difftest` describes the environment of a run in this format, and both sides read the same file: the oracle with `cel.FromConfig` of cel-go, the library with `Apply`. On the library side the stand creates `new CelEnvironmentBuilder(CelStandardLibrary.V1)` — the latest version of the standard library, since the manifest carries no version — calls `Apply` and then `Build`. A manifest has neither `greatPartsCel` nor `validators`; the stand rejects such a manifest itself, before `Apply`, because the oracle would read it differently. In the walking skeleton a manifest holds `name`, `description` and `variables` of simple types and `dyn`.

Related pages: [facade](facade.md), [core](core.md).
