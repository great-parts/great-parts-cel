# Protobuf messages

The package `Great.Parts.Cel.Protobuf` gives an environment the messages of protobuf: expressions read their fields, test their presence, create messages with object literals, compare them and unpack `google.protobuf.Any`. The package depends on `Great.Parts.Cel` and `Google.Protobuf` only, reads and writes fields through the field accessors of `Google.Protobuf` without reflection, and works under trimming and Native AOT.

## Adding the package

Add the package `Great.Parts.Cel.Protobuf` next to `Great.Parts.Cel`, build a provider from the descriptors of the files that `protoc` generated for C#, and give it to the environment:

The example uses `google.protobuf.Api`, a message that `Google.Protobuf` itself ships; the file of your own messages is added the same way:

```csharp
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel;
using Great.Parts.Cel.Protobuf;

CelProtobufTypes types = new CelProtobufTypesBuilder("api")
    .AddFile(ApiReflection.Descriptor)
    .Build();

CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
    .AddTypes(types)
    .UseContainer("google.protobuf")
    .AddVariable<Api>("api")
    .Build()
    .GetEnvironmentOrThrow();

CelProgram<string> label = environment.Compile<string>("api.name + ' v' + api.version").GetProgramOrThrow();
Api value = new() { Name = "orders", Version = "2" };
Console.WriteLine(label.Evaluate(new CelBindings().Add("api", value)).GetValueOrThrow()); // Output: orders v2
```

- **Files.** `AddFile` takes the descriptor of a file and describes its messages, with the messages nested in them, and its enumerations, and does the same for every file it imports, directly or not. A file that was added before, and a file that two files import, is one declaration. A file with a message that has no .NET class, such as a descriptor read from a serialized `FileDescriptorSet`, is an `ArgumentException`: its fields could not be read.
- **Names.** A type has its full protobuf name, `google.protobuf.Api`, and an enumeration constant is `google.protobuf.Syntax.SYNTAX_PROTO3`. `UseContainer` lets an expression write the short name.
- **Variables.** `AddVariable<Api>("api")` declares a variable of the object type of the generated class. The message is passed as it is, without a copy.
- **The known types.** The messages of `google/protobuf` that CEL converts — `Any`, `Value`, `Struct`, `ListValue`, `Timestamp`, `Duration` and the nine wrappers — are always described, together with `Empty` and `FieldMask`, whether the application added their files or not. They are not objects of an expression: they are read as the values of the table below. A variable of the .NET type of one of them, such as `AddVariable<Timestamp>`, is `TypeNotDeclared`.
- **One provider.** Every provider describes the known types, so two providers in one environment are the build error `DuplicateType`, also when they have no file in common. Put all files of an environment into one builder.
- **Extensions.** `AddProtoExtension` takes the typed extension object that `protoc` generates for a proto2 extension. The file that declares the extension and the file of the extended message have to be added; otherwise `Build` throws `InvalidOperationException`. An extension is a field of its message with its full name, which an expression selects in backquotes, `` m.`acme.orders.priority` ``, or through the macros `proto.getExt` and `proto.hasExt`.
- **Threads.** A provider is immutable and shared by any number of threads and environments; a builder is not thread-safe. A message must not be changed while an evaluation reads it.

## Fields of a message

| Protobuf | CEL type | Value |
| :- | :- | :- |
| `int32`, `sint32`, `sfixed32`, `int64`, `sint64`, `sfixed64` | `int` | the number |
| `uint32`, `fixed32`, `uint64`, `fixed64` | `uint` | the number |
| `float`, `double` | `double` | a `float` is widened: a field that holds `1.333f` reads 1.3329999446868896 |
| `bool`, `string` | `bool`, `string` | as they are |
| `bytes` | `bytes` | the memory of the message, without a copy |
| an enumeration | `int` | the number, also a number the enumeration does not declare; `google.protobuf.NullValue` too: `NULL_VALUE` is the `int` 0 |
| a message `M` | the object type `M` | the instance, without a copy |
| `repeated T` | `list(T)` | a view over the field, without a copy |
| `map<K, V>` | `map(K, V)` | a view over the field, without a copy, in the order of insertion |
| `google.protobuf.Any` | `any` | the message it contains, see [Any](#any) |
| `google.protobuf.Value` | `dyn` | `null`, `double`, `string`, `bool`, `map(string, dyn)` or `list(dyn)` by the kind that is set |
| `google.protobuf.Struct` | `map(string, dyn)` | a map view |
| `google.protobuf.ListValue` | `list(dyn)` | a list view |
| `google.protobuf.Timestamp`, `google.protobuf.Duration` | `timestamp`, `duration` | the time value |
| `BoolValue`, `BytesValue`, `StringValue` | `wrapper(bool)`, `wrapper(bytes)`, `wrapper(string)` | the value, or `null` for a field that is not set |
| `Int32Value`, `Int64Value` | `wrapper(int)` | the same |
| `UInt32Value`, `UInt64Value` | `wrapper(uint)` | the same |
| `FloatValue`, `DoubleValue` | `wrapper(double)` | the same, a float widened to `double` |

- **A field that is not set** reads as the default of its type, a message field as an empty message, `Timestamp` as `1970-01-01T00:00:00Z`, `Duration` as `0s`, `Struct` as the empty map, `ListValue` as the empty list, a wrapper, `Any` and `Value` as `null`. A wrapper that is set to the default reads the default: `Int32Value{}` is `0`.
- **`Timestamp` and `Duration`** outside the range of CEL are the error `ConversionRange`, `{value} is out of the range of timestamp` or `of duration`. For a `Timestamp` the value is the `seconds` when they are outside 0001-01-01 to 9999-12-31, otherwise the `nanos` when they are outside 0 to 999,999,999. For a `Duration` the value is the `nanos` when they are beyond ±999,999,999 or of the other sign than the `seconds`, otherwise the `seconds` when the total in nanoseconds does not fit a 64-bit number.
- **`oneof`.** The members of a `oneof` are fields of their own; a member that is not the one set reads as the default of its type.
- **A list or a map of messages** gives the instances; a list of `Any` unpacks each element when it is read.

### Presence

`has(m.f)` follows the language definition:

| Field | `has()` is true when |
| :- | :- |
| a proto2 field, a proto3 `optional` field, a message field | the field has been set, also to its default |
| a member of a `oneof` | it is the member that is set, also at its default |
| `repeated`, `map` | the field is not empty |
| a proto3 scalar or enumeration without `optional` | the value differs from the default of its type; a `float` or `double` is the default only for `+0.0`, so `-0.0` and a NaN are present |
| an extension | the extension is set; for a repeated extension, the list is not empty |

A name that the message does not have is the checking error `NoSuchField`, and without type checking the evaluation error `EvalNoSuchField`.

## Object literals

`M{f: v, …}` creates a message of any type that the provider describes, in the order of the text; a field named twice keeps its last value. The message is created empty, and each field is set from its value:

| Field | Value | The field gets |
| :- | :- | :- |
| `int32`, `sint32`, `sfixed32` | `int` from −2³¹ to 2³¹ − 1; another `int` is `ConversionRange`, `{value} is out of the range of int32` | the number |
| `int64`, `sint64`, `sfixed64`; `uint64`, `fixed64` | `int`; `uint` | the number |
| `uint32`, `fixed32` | `uint` up to 2³² − 1; above is `ConversionRange`, `… of uint32` | the number |
| `float` | `double` | the nearest `float`: a value that is too small becomes 0 or a subnormal number, one that is too large an infinity, and a NaN stays a NaN; none of them is an error |
| `double`, `bool`, `string` | the same kind | as it is |
| `bytes` | `bytes` | a copy |
| an enumeration | `int` from −2³¹ to 2³¹ − 1, else `ConversionRange`, `… of int32` | the number, also one that the enumeration does not declare; `null` is not accepted for any enumeration, `NullValue` included |
| a message `M` | an object that is an instance of the class of `M`; `null` leaves the field unset | that instance, shared and not copied |
| `Any` | any value | packed, see [Any](#any) |
| `Value` | any value; `null` is a `Value` of `NULL_VALUE`, which is set | see [`Value`, `Struct` and `ListValue`](#value-struct-and-listvalue) |
| `Struct` | a `map` with string keys | a `Struct` |
| `ListValue` | a `list` | a `ListValue` |
| `Timestamp`, `Duration` | `timestamp`, `duration`; `null` leaves the field unset | the time |
| a wrapper | the primitive of the wrapper, 32-bit ranges as above; `null` leaves the field unset | a wrapper holding the number, also the default: `0` is a set `Int32Value` |
| `repeated T` | a `list`; each element as `T` | the elements |
| `map<K, V>` | a `map`; each key as `K` — a 32-bit key is checked, and `ConversionRange` names the key — and each value as `V` | the pairs |

- **Errors.** A value whose kind does not fit its field is `FieldTypeMismatch`, `field '{field}' of type '{type}' has a value of type '{actual}', expected '{declared}'`. With type checking a value that does not fit is already `TypeMismatch` at the entry; the evaluation error comes from a `dyn` value or an expression compiled without checking. A key of a `Struct` that is not a string is `InvalidArgument`, `invalid argument to '{type}.{field}': a key of the type {actual} is not a string`. The first error ends the creation. The place of every error is the `{` of the literal.
- **`null` in a container.** In a `repeated` or a `map` field an element or a value that is `null` is left out when the element type is `Timestamp`, `Duration` or a wrapper — `[timestamp(1), null]` gives a list of one element — and kept when it is `Any` or `Value`, where it reads back as `null`; for any other element type it is `FieldTypeMismatch`.
- **The result** is the message as an instance of its generated class; an expression that returns it gives the instance to `Compile<T>`. A literal makes a new message each time it is evaluated, and no expression changes a message it has read.
- **Extensions** are set by their name in backquotes, `` T{`acme.orders.priority`: 1} ``.
- **The known types.** A literal of a known type is the value that the type converts to: `google.protobuf.Int32Value{value: 1}` is `1`, `BoolValue{}` is `false`, `Value{string_value: 'a'}` is `'a'`, `Value{}` is `null`, `Struct{fields: {'a': 1}}` is a map, `ListValue{values: [1]}` is a list, and `Any{type_url: u, value: b}` is the content of the `Any`. `Empty{}` and `FieldMask{paths: […]}` are ordinary messages. `google.protobuf.Timestamp{…}` and `google.protobuf.Duration{…}` are not supported — their names are type identifiers of the standard library, a literal is `NotAType` — write `timestamp(…)` and `duration(…)`.

### `Value`, `Struct` and `ListValue`

A value written into a `Value`, a `Struct` or a `ListValue` field takes these rows; a list and a map are written element by element, and a value nested deeper than the thread stack allows is `InvalidArgument`, `the value is nested too deeply`:

| CEL value | As a `Value` |
| :- | :- |
| `null` | `null_value` |
| `bool` | `bool_value` |
| `int` | `number_value` from −(2⁵³ − 1) to 2⁵³ − 1, the integers that a `double` holds exactly; otherwise `string_value` with the decimal digits, as protobuf's JSON mapping does: `9007199254740991` is a number and `9007199254740992` is the string `"9007199254740992"` |
| `uint` | `number_value` up to 2⁵³ − 1, otherwise `string_value` |
| `double` | `number_value`, a NaN and an infinity included |
| `string` | `string_value` |
| `bytes` | `string_value`, standard Base64 with padding: `b'foo'` is `"Zm9v"` |
| `timestamp` | `string_value`, the text of `string(timestamp)` |
| `duration` | `string_value`, the text of `string(duration)`: `1000000s`, `1.5s` |
| `list` | `list_value`, each element as a `Value` |
| `map` | `struct_value`, each value as a `Value`; a key that is not a string is `InvalidArgument` |
| an object, an instance of a message | the canonical JSON of protobuf for that message: a `Struct` of its fields by their JSON names, with 64-bit integers as text; a message that holds an `Any` which the provider cannot resolve, or whose content does not parse, is `InvalidArgument`, `the message cannot be written as JSON: …`, as in cel-go it is an error |
| a type, an error | `FieldTypeMismatch` |

## `Any`

An `Any` field is read as the message that it contains. The type URL names the type: what follows its last `/`, such as `acme.orders.Order` in `type.googleapis.com/acme.orders.Order`, is looked up among the messages of the provider, the known types included, and the content is parsed as that message. The result is the message, an object; the content of a wrapper is its value, so an `Any` that holds an `Int64Value` reads as an `int`, one that holds a `Struct` as a map, `Value` as the kind it holds, `Timestamp` and `Duration` as time. An `Any` that holds an `Any` is unpacked until the content is not one, for up to 16 `Any` messages in a chain.

The unpacking is done when a field of the type `Any` is read — also an element of a `repeated` field or a value of a `map` field, when the element is read — when the literal `Any{…}` is evaluated, and in the comparison of messages, where a failure is not an error. `has()` does not unpack. An `Any` field that is not set reads `null`. The provider unpacks only the types it describes: a message of another provider, or of no provider, is not found.

A value written into an `Any` field is packed under the type URL `type.googleapis.com/<full name>`: a message as itself, `null` as a `Value` of `NULL_VALUE`, `bool`, `int`, `uint`, `double`, `string` and `bytes` as `BoolValue`, `Int64Value`, `UInt64Value`, `DoubleValue`, `StringValue` and `BytesValue`, `timestamp` and `duration` as `Timestamp` and `Duration`, a list as `ListValue` and a map as `Struct`.

### Error 3020

An `Any` that cannot be unpacked is the error `InvalidAny` (3020) at the node that read the field or evaluated the literal; for an element of a `repeated` field or a value of a `map` field it is the node that read the element — the index or the key lookup, `size`, `in`, `==`, the range of a macro — so `m.repeated_any[1]` fails at the index, while `m.repeated_any` and `size(m.repeated_any)` do not. The text is `cannot unpack google.protobuf.Any with type URL '{typeUrl}': {detail}`; the `{typeUrl}` is the address of the `Any` that failed, which for a chain is the one inside:

| `{detail}` | When |
| :- | :- |
| `the type URL is empty` | an `Any` that is set and has no type URL, as `Any{}` is |
| `the type URL has no '/'` | the address has no `/` |
| `the message type '{name}' is not described by the type source '{sourceName}'` | the name after the last `/` is empty or is no message of the provider; `{sourceName}` is the name the builder was given |
| `the content is not a valid {name}: {message}` | the content does not parse as the named message; `{message}` is that of the parser |
| `Any is nested deeper than 16 levels` | the seventeenth `Any` of a chain |

## Equality

Two messages of one type are equal by CEL equality, not by `Equals` of the generated class: fields are taken in the order of their numbers, the registered extensions after them. A field that is set in one message and not in the other makes them unequal, so in proto3 `single_int64: 0` equals the empty message and in proto2 it does not. Values of fields are compared by CEL equality: numbers with `==`, so a NaN is equal to nothing, itself included, and a message with a NaN is unequal even to a copy of itself; strings and bytes by content; nested messages by this rule; `Value`, `Struct` and `ListValue` as the JSON-like values they read as; `repeated` pairwise; `map` by key. Two messages of different types are not equal. An `Any` is unpacked in each message, and when both unpack the contents are compared by CEL equality, so an `Any` of `Int64Value{1}` equals one of `DoubleValue{1.0}`; when either does not unpack, the two are equal when their type URLs are equal and their contents are equal byte for byte. A comparison never gives `InvalidAny`.

Unknown fields are not compared: the generated classes give no access to them, so a message with unknown fields equals one without when the known fields are equal. cel-go compares unknown fields by their bytes.

## Cost

| Operation | Units |
| :- | :- |
| a field selection, also of a field that is not set | 1 |
| `has()` of a field | 1 |
| unpacking an `Any`, in addition to the selection of the field that holds it | 1 + ⌈(U + B)/10⌉, U the length of the type URL in UTF-16 units and B the bytes of the content, charged before the content is parsed, also when it does not unpack; for a `repeated` or `map` field of `Any`, the sum over its elements, charged when the field is read |
| a literal of n entries | 1 + n, and ⌈L/10⌉ for the sum L of the lengths of the strings and bytes it writes; the `type_url` and `value` of `Any{…}` count |
| the equality of two messages | 1 + ⌈(Sₗ + Sᵣ)/10⌉, S the size of a message in bytes of the protobuf wire format, charged once before the comparison |
| `proto.getExt`, `proto.hasExt` | those of a selection and of `has()` |

A cost limit stops an evaluation before the work that the next charge pays for, so a limit that does not cover an unpack stops it with `CostLimitExceeded` and not with `InvalidAny`. The rows of every node are on the page [Cost](cost.md).

## The set `protos`

The set `protos` version 0 adds the macros `proto.getExt(m, pkg.name)` and `proto.hasExt(m, pkg.name)` for the extensions of proto2, which a type source describes as fields of their message:

```csharp skip: the example needs the classes and the extension object that protoc generates for a proto2 file
CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
    .AddTypes(new CelProtobufTypesBuilder("messages")
        .AddFile(MessagesReflection.Descriptor)
        .AddProtoExtension(MessagesExtensions.Priority)
        .Build())
    .AddExtension(CelExtensions.Protos(0))
    .AddVariable<Message>("m")
    .Build()
    .GetEnvironmentOrThrow();
```

`proto.getExt(m, acme.priority) + 1` is `` m.`acme.priority` + 1 ``. See [Extension sets](extensions.md) for the rules and the errors.

## Differences from cel-go

- Unknown fields are not compared in the equality of messages.
- The literals `google.protobuf.Timestamp{…}` and `google.protobuf.Duration{…}` are `NotAType`; use `timestamp(…)` and `duration(…)`.
- A chain of `Any` is unpacked up to 16 levels; cel-go has no limit.

Everything else on this page — the unset `Any`, `Timestamp` and `Duration`, `NullValue` read as `int`, the numbers in a `Value` — follows cel-go.

Related pages: [Type mapping](type-mapping.md), [Error codes](error-codes.md), [Cost](cost.md), [Extension sets](extensions.md), [Types described at run time](runtime-types.md).
