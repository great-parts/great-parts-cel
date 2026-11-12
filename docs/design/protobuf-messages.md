# Protobuf: literals, `Any`, JSON-like values and equality

This page covers what the package does beyond reading: it creates messages for object literals, packs and unpacks `google.protobuf.Any`, converts values to the JSON-like types `Value`, `Struct` and `ListValue`, and compares messages. The provider, the descriptions, reading and presence are on [protobuf](protobuf.md); the part of the core that checks and plans a literal is [checking](checking.md#message-literals-of-constructible-types) and [evaluation](evaluation.md#message-literals). The sources are `LiteralType.cs`, `LiteralWriter.cs`, `JsonWriter.cs`, `AnyPacker.cs`, `AnyUnpacker.cs` and `MessageEquality.cs` of the package. Every error value of this page is made with `CelObjectType.FieldError(number, code, text)`, where the text is the whole message of the code from the error table, formed by the package; the evaluation adds the position ([objects](objects.md#members-for-protobuf-messages)).

## Literals

`IsConstructible` is `true` for every message description and `CreateInstance(fieldNumbers, fieldValues)` creates the message. The core calls it with distinct numbers, in the order of the literal, and with values that type checking accepted for the fields; without type checking the values can be of any kind, and every kind that does not fit is the error below, so the method never throws for a value of the language.

1. **The empty message** is `descriptor.Parser.ParseFrom(ReadOnlySpan<byte>.Empty)`: no reflection and no `Activator`, so it works under Native AOT. A message of a registered proto2 extension set is parsed by the parser of the descriptor as well.
2. **Each field in order** is converted by the table below and set with `IFieldAccessor.SetValue`; a `repeated` field gets its elements by `Add` to the `IList` that `GetValue` returns, a `map` field its pairs by the indexer of the `IDictionary`; an extension is set by its `set` delegate ([protobuf](protobuf.md#proto2-extensions)). A `oneof` member replaces the one that was set before, as `SetValue` does. The first error ends the creation and is the value; the fields that follow are not converted.
3. **The result** of an ordinary message is the message as a bare object (`CelObjectType.FromInstance`). The result of a literal of a known type is not a message ([below](#literals-of-the-known-types)). The created message is not changed afterwards: nothing in the package writes to a message that an expression has seen, and a message value put into a field is that instance, shared, not copied.

**Conversion of a value to a field.** `value` is a `CelValue`; the error of a kind that is not in the table is `FieldTypeMismatch` (3019), the text `field '{field}' of type '{type}' has a value of type '{actual}', expected '{declared}'`, `{actual}` the name of the runtime type without parameters and `{declared}` the CEL type of the field in its text form:

| Field | Accepted value | Written as |
| :- | :- | :- |
| `int32`, `sint32`, `sfixed32` | `int` from −2³¹ to 2³¹ − 1; another `int` is `ConversionRange` (3007), `{value} is out of the range of int32` | the number |
| `int64`, `sint64`, `sfixed64` | `int` | the number |
| `uint32`, `fixed32` | `uint` up to 2³² − 1; above is `ConversionRange`, `… of uint32` | the number |
| `uint64`, `fixed64` | `uint` | the number |
| `float` | `double` | narrowed to `float`: a value too small for a `float` becomes 0 or a subnormal number, a value too large `±Infinity`, no error — `1e-50` is `0`, `1.4e55` is `+Infinity`; a NaN stays a NaN |
| `double`, `bool`, `string` | the same kind | as it is |
| `bytes` | `bytes` | `ByteString.CopyFrom` of the span: a copy |
| enumeration, `google.protobuf.NullValue` included | `int` from −2³¹ to 2³¹ − 1, else `ConversionRange`, `… of int32` | the number, also one the enumeration does not declare; `null` is not accepted for any enumeration, `NullValue` too, as in cel-go: `TestAllTypes{null_value: null}` is a type mismatch (`TypeMismatch` of the checker, `FieldTypeMismatch` without it), and `TestAllTypes{null_value: 0}` is set to `NULL_VALUE` |
| message `M`, not a known type | an object that is an instance of the class of `M`; `null` leaves the field unset | the instance |
| `Any` | any value except an error | [packed](#values-into-value-and-any) |
| `Value` | any value except an error | [as a `Value`](#values-into-value-and-any); `null` is a `Value` holding `NULL_VALUE`, which is set |
| `Struct` | a `map` whose keys are strings; another key kind is `InvalidArgument` (3008), `invalid argument to '{type}.{field}': a key of the type {actual} is not a string` | a `Struct` of the values as `Value`s |
| `ListValue` | a `list` | a `ListValue` of the elements as `Value`s |
| `Timestamp` | `timestamp`; `null` leaves the field unset | `seconds` and `nanos` |
| `Duration` | `duration`; `null` leaves the field unset | `seconds` and `nanos` of the same sign |
| wrapper | the primitive of the wrapper, 32-bit ranges as above; `null` leaves the field unset | a wrapper holding the number, also the default: `0` is a set `Int32Value` |
| `repeated T` | a `list` | each element by the row of `T` |
| `map<K, V>` | a `map` | each key by the row of `K` — a 32-bit key is range checked, and `ConversionRange` names the key — and each value by the row of `V` |

`null` as the value of any field not named above — a scalar, an enumeration, `Struct`, `ListValue`, `repeated`, `map` — is `FieldTypeMismatch` with `null_type` as the actual type.

**Null in a container.** In a `repeated` field and in a `map` field, an element or a value that is `null` is left out when the element type is `Timestamp`, `Duration` or a wrapper, and the entry is not made, so `[timestamp(1), null]` gives a list of one element; it is kept when the element type is `Any` — as an `Any` holding a `Value` with `NULL_VALUE` — or `Value`, and reads back as `null`; for any other element type it is `FieldTypeMismatch`. The rule follows the conformance cases: an absent element of a nullable type is dropped, one of a dynamic type stays `null`.

**Errors and their place.** Each error value is created with the number of the field that failed, and the core gives it the position of the literal, the `{` of the message name. The core gives the position because the package returns one value for the whole literal; the text names the field and the type.

**Extension fields** are set by their name in backquotes, `` T{`pkg.ext`: 1} ``, like any other field; the field is in the description, so the checker types the value by the CEL type of the extension.

### Literals of the known types

`CreateInstance` of the thirteen descriptions of the known types ([protobuf](protobuf.md#the-known-types)) creates the message of the known type by the rows above — its fields are `value`, `type_url`, `fields`, `values`, `null_value`, `number_value`, `string_value`, `bool_value`, `struct_value`, `list_value` and the rest of the descriptor — and then returns what the type converts to:

| Literal | Value |
| :- | :- |
| `BoolValue{value: v}`, `StringValue`, `BytesValue`, `DoubleValue` | the primitive; the empty literal is the default: `BoolValue{}` is `false`, `BytesValue{}` is `b''`; never `null` |
| `Int32Value{value: v}`, `Int64Value`, `UInt32Value`, `UInt64Value` | `int` or `uint`; a 32-bit range error is `ConversionRange`; `Int32Value{}` is `0` |
| `FloatValue{value: v}` | the `double` of the `float` that `v` narrows to: `FloatValue{value: 1.333} == 1.333` is `false` |
| `Value{…}` | by the field that is set: `Value{bool_value: true}` is `true`, `Value{number_value: 1.5}` is `1.5`, `Value{struct_value: {'a': 1}}` is a map, `Value{list_value: []}` is a list; `Value{}` and `Value{null_value: 0}` are `null`, and so is `Value{null_value: 1}` — the member `null_value` is an enumeration, which takes a number and not `null`, so `Value{null_value: null}` is a type mismatch as above; the kind of a `Value` decides what it reads as, and the member selects the kind `null_value` whatever its number |
| `Struct{fields: m}` | a map; the values of `m` are converted as `Value`s |
| `ListValue{values: l}` | a list |
| `Any{type_url: u, value: b}` | the content, unpacked as a field would ([below](#any)); `Any{}` and an unknown type URL are `InvalidAny` |

None of them is an object, so `Int64Value{value: 1}.value` has nothing to select: type checking reports `NoSuchField`, and evaluation without type checking `FieldSelectionNotSupported` ([evaluation](evaluation.md#nodes)). Their static type is the CEL type of the description, which the core takes from the full name ([types](types.md#well-known-names)): `wrapper(int)` for `Int32Value`, `dyn` for `Value`, `map(string, dyn)` for `Struct`, `list(dyn)` for `ListValue`, `Any` for `Any`. Such a literal is therefore comparable with its value: `google.protobuf.Int32Value{value: 1} == 1` and `google.protobuf.BoolValue{} != null` are `true`.

`google.protobuf.Empty{}` and `google.protobuf.FieldMask{paths: […]}` are ordinary messages. `google.protobuf.Timestamp{…}` and `google.protobuf.Duration{…}` are not supported: their names are type identifiers of the standard library, which `NotAType` rejects at a literal, as without the package; `timestamp(…)` and `duration(…)` create them. cel-go accepts such literals; the difference is recorded ([below](#differences-from-cel-go)).

## Values into `Value` and `Any`

A value assigned to a field of the type `Value`, to a `Struct` or `ListValue` field, or to an `Any` field, becomes protobuf data by these rows. Nested lists and maps are walked with an explicit check of the thread stack: `RuntimeHelpers.TryEnsureSufficientExecutionStack()` before each level, and a value nested more deeply is `InvalidArgument`, `invalid argument to '{type}.{field}': the value is nested too deeply`.

| CEL value | As a `Value` | In an `Any` |
| :- | :- | :- |
| `null` | `null_value` | a `Value` holding `null_value` |
| `bool` | `bool_value` | `BoolValue` |
| `int` | `number_value` when the value is from −(2⁵³ − 1) to 2⁵³ − 1, else `string_value` with the decimal digits: a `double`, the number of a `Value`, holds the integers of that range exactly, and the others travel as text, as protobuf's JSON mapping does; the same boundary is that of cel-go v0.32.0, so `9007199254740991` is the number and `9007199254740992` the string `"9007199254740992"`, and `4294967296` is the number | `Int64Value` |
| `uint` | `number_value` up to 2⁵³ − 1, else `string_value`; the same boundary | `UInt64Value` |
| `double` | `number_value`, a NaN and an infinity included | `DoubleValue` |
| `string` | `string_value` | `StringValue` |
| `bytes` | `string_value`, the standard Base64 with padding: `b'foo'` is `"Zm9v"` | `BytesValue` |
| `timestamp` | `string_value`, the text of `string(timestamp)`: RFC 3339 with the fraction without trailing zeros | `Timestamp` |
| `duration` | `string_value`, the text of `string(duration)`: `1000000s`, `1.5s` | `Duration` |
| `list` | `list_value`, each element as a `Value` | `ListValue` |
| `map` | `struct_value`, each value as a `Value`; a key that is not a string is `InvalidArgument` | `Struct` |
| an object, an instance of a message | the canonical JSON of protobuf for that message — a `Struct` of its fields by their JSON names, and the special forms of the known messages: `Empty` is `{}`, `FieldMask` the string `"foo,bar"`, `Any` … — made by `JsonFormatter` with a `TypeRegistry` of the files of the provider and read back with `Value.Parser.ParseJson`; a message that holds an `Any` which the registry cannot resolve, or whose content does not parse, is `InvalidArgument`, `the message cannot be written as JSON: …`, as in cel-go it is an error | the message itself, packed with the type URL `type.googleapis.com/<full name>` (`Any.Pack`) |
| a `type` value, an error | `FieldTypeMismatch` | the same |

The literal of a wrapper is an `int`, `uint` or `double` of the CEL value, so it takes the same rows: `TestAllTypes{single_value: google.protobuf.Int64Value{value: 9007199254740992}}.single_value` is the string `"9007199254740992"` and `Int64Value{value: 5000000000}` the number `5000000000.0`.

So `TestAllTypes{single_any: TestAllTypes{}.single_value}.single_any` is `null`: an unset `Value` reads `null`, and `null` packs as the `Value` of `NULL_VALUE`. An unset `Any` field reads `null` as well, and nothing is unpacked; only the literal `Any{}`, which makes an `Any` with no type URL, is `InvalidAny`, and so is an `Any` that is set and has no type URL.

## `Any`

An `Any` field is read as the message it contains. The unpack is done in three places, and in no other: when a field of the type `Any` — or an element of a `repeated` field, a value of a `map` field of `Any` — is read; when the literal `Any{…}` is evaluated; and in equality, where a failure is not an error ([below](#equality)). A bare .NET `Google.Protobuf.WellKnownTypes.Any` in a `dyn` place is not unpacked: it is not described, so it is `TypeNotAccessible` like any .NET well-known type. An application that holds an `Any` as data puts it in a field of a message.

**Registry.** The provider keeps, built by `Build` in a frozen dictionary by the full name that protobuf gives its types, one entry for each message the provider describes, the known types that CEL converts and `Empty` and `FieldMask` included — `Timestamp` and `Duration` among them, which have no description but are in the table — with the parser of the message and its kind of conversion. The keys are the names of the descriptors, which the files give, never text of an expression or data; the name of the `type_url` is looked up as a `ReadOnlySpan<char>`, so no string is made for the lookup. The registry is that of this provider only: a message of another provider or of no provider is not found.

**Unpack**, once for each level of nesting:

1. The type URL is the text of `Any.TypeUrl`. The name is what follows the last `/`: `type.googleapis.com/cel.expr.conformance.proto2.TestAllTypes` names `cel.expr.conformance.proto2.TestAllTypes`.
2. The name is looked up. The content, `Any.Value.Span`, is parsed by `MessageParser.ParseFrom` of the entry, with a parser that holds the `ExtensionRegistry` of the provider ([protobuf](protobuf.md#proto2-extensions)); nothing is cached, so each read parses again.
3. A message of the known types that CEL converts becomes its value by the table of [protobuf](protobuf.md#the-known-types): `Int64Value` an `int`, `UInt32Value` a `uint`, `FloatValue` a `double`, `Value` its kind, `Struct` a map view, `ListValue` a list view, `Timestamp` and `Duration` the time values with the bounds of the reading rows. A content that is itself an `Any` is unpacked again, up to 16 levels of `Any` in a chain; the seventeenth is `InvalidAny`, the detail `Any is nested deeper than 16 levels`. cel-go has no such limit; the difference is recorded ([below](#differences-from-cel-go)). Any other message is the message, a bare object whose description the type index finds by the class.

**The error** `InvalidAny` (3020) has the text `cannot unpack google.protobuf.Any with type URL '{typeUrl}': {detail}`, the `{detail}` of one of these. `{typeUrl}` is the type URL of the `Any` that failed to unpack: in a chain it is the URL of the inner `Any` that failed, not that of the outer one. The place is the node that read the field or evaluated the literal; for an element of a `repeated` field or a value of a `map` field, whose `Any` is unpacked when the element is read, it is the node that read the element — the index or the key lookup, the call of `size`, `in` or `==`, the range of a comprehension, the identifier or the call whose value is checked against a declared type — as the table of errors of [evaluation](evaluation.md#errors-as-values) says. So `m.repeated_any[1]` fails at the index, `m.repeated_any` and `size(m.repeated_any)` do not fail, and `m.repeated_any[0]` is read as usual when only `[1]` does not unpack:

| Case | `{detail}` |
| :- | :- |
| the type URL is empty — the literal `Any{}`, an `Any` that is set and has no URL; an unset field is not unpacked and reads `null` | `the type URL is empty` |
| the type URL has no `/` | `the type URL has no '/'` |
| the name after the `/` is empty or not in the registry | `the message type '{name}' is not described by the type source '{sourceName}'` |
| the content does not parse | `the content is not a valid {name}: {message}`, `{message}` the message of `InvalidProtocolBufferException` |
| a chain of more than 16 `Any` | `Any is nested deeper than 16 levels` |

**Cost.** An unpack costs `1 + ⌈(U + B)/10⌉`, U the length of the type URL in UTF-16 units and B the bytes of the content, charged before the parse; for a field that is read the amount is the `FieldReadCost` of the description ([protobuf](protobuf.md#cost-of-a-read)); for the literal `Any{…}` the unpack is paid by the cost of the literal, which is 1 + n + ⌈L/10⌉ with L the lengths of the string and bytes values it writes — the `type_url` and the `value`, so that the amount is that of the read ([evaluation](evaluation.md#message-literals)). B and U count the whole chain: inner `Any`s are inside the content. The memory of an unpack, the new message and the text of an error, is within 64 bytes per unit charged; the text of an error holds the type URL, which U has already paid for.

## Equality

Two messages of one description are compared by `CelEquals`; the core knows the description does so from `EqualityCost` and calls `CelEquals` after charging it ([objects](objects.md#members-for-protobuf-messages)). The cost is `1 + ⌈(Sₗ + Sᵣ)/10⌉`, charged once before the comparison, S the size of a message in bytes of the protobuf wire format, `IMessage.CalculateSize()`; the work of the comparison that grows with the data reads each byte of the messages a bounded number of times, so the bytes are its measure, and the pair-by-pair accounting of ordinary objects is not needed. A comparison of two messages that differ in a field read first still costs the sizes: the cost is an upper bound known before the work, as for two `bytes`. The check of each field of the description is a constant of the description and is not charged: the reflection of `Google.Protobuf` boxes the value of a proto3 field without presence, about 24 bytes for each field of the description for each pair of messages compared, outside the 64 bytes per unit.

The comparison does not use the `Equals` of the generated class, which compares NaNs as equal, nor the order of the serialized bytes. It takes the fields in the order of their numbers, the registered extensions after the others, and:

1. A field that is present in one message and absent from the other makes them unequal ([presence](protobuf.md#presence)); a field absent from both is skipped. So in proto3 `single_int64: 0` equals the empty message, and in proto2 it does not.
2. Values of present fields are compared by CEL equality of what reading gives, without making the `CelValue`s:
   - integers, enumerations, `bool`, strings and bytes by value, strings ordinal, bytes by content; `float` and `double` with `==`, so a NaN is equal to nothing, itself included: `TestAllTypes{single_double: double('NaN')} == TestAllTypes{single_double: double('NaN')}` is `false`, and a message with a present NaN is unequal even to itself;
   - a message by this algorithm, for the nested messages of one class; two messages of different classes in a `dyn` place are unequal before the call;
   - `Timestamp` and `Duration` by `seconds` and `nanos`; a wrapper by the wrapped value, a set wrapper holding the default being unequal to an unset one by rule 1; `Value`, `Struct` and `ListValue` as the JSON-like values they read as — `null` equals `null`, numbers by `==`, maps with the same keys and equal values, lists pairwise;
   - `repeated`: the same count and the elements pairwise by their kind; `map`: the same count and, for each key of the left, the same key in the right with an equal value;
   - `Any`: both are unpacked, the registry of the provider and the limit of 16 levels as for a read, without charge; when both unpack, the two results are compared by CEL equality, so `Any` holding `Int64Value{1}` equals one holding `DoubleValue{1.0}`, and two `TestAllTypes` are compared by this algorithm; when either does not unpack — no type URL, a type the registry lacks, a content that does not parse — the two `Any` messages are equal when their type URLs are equal and their contents are equal byte for byte. So an equality never gives `InvalidAny`, and two `Any`s that name a type in different byte orders but have the same content are equal.
3. The result is `CelValue.FromBoolean`; the comparison gives no other error. A pair of nested messages waits on a stack that the comparison keeps in an array of its own, so the depth of the messages is bounded by memory and never by the thread stack; the unpack of `Any` is bounded by its 16 levels.

Unknown fields are not compared: the generated classes of `Google.Protobuf` give no access to them, so a message that holds unknown fields equals one that does not when the known fields are equal. This differs from cel-go, which compares unknown fields by their bytes; the difference is recorded ([below](#differences-from-cel-go)). Outside an evaluation — `CelValue.CelEquals` of two values made with `CelValue.FromObject` — the same algorithm runs without a meter.

## Differences from cel-go

Three differences from cel-go v0.32.0 are decided and are four rows of the registry of known differences of the conformance project (`tests/Great.Parts.Cel.Conformance/Regression/known-differences.md`, class `by-design`); each has a minimal case in the regression corpus (`Regression/protobuf-differences.textproto`) and an entry in its skip list, whose expectation is the answer of cel-go:

| Difference | The library | cel-go | Signature of the row |
| :- | :- | :- | :- |
| unknown fields in the equality of messages | not compared: two messages that differ only in unknown fields are equal | compared by their bytes | `equals(cel.expr.conformance.proto3.TestAllTypes, cel.expr.conformance.proto3.TestAllTypes) value-mismatch` |
| literals `google.protobuf.Timestamp{…}` and `google.protobuf.Duration{…}` | `NotAType`: the names are type identifiers of the standard library | the value, `Timestamp{seconds: 1} == timestamp(1)` is `true` | `message(google.protobuf.Timestamp, int) we-error`, `message(google.protobuf.Duration, int) we-error` |
| the depth of nested `Any` | at most 16 `Any` in a chain, then `InvalidAny` | unpacks a chain of any length | `message(google.protobuf.Any, string, bytes) we-error` |

In the signatures of the node `message` the arguments are the full name of the type of the literal and then the types of the values of its fields in the order of the text; the three rows are as narrow as their cases: a literal of the named type with fields of the named kinds. The limit of the depth and the equality of unknown fields hold for messages read from fields as well, and each case names one form. Everything else of the pages of the package — the unset `Any`, `Timestamp` and `Duration`, `NullValue` as `int`, the numbers in `Value` — follows cel-go and is not a difference.

Related pages: [protobuf](protobuf.md), [objects](objects.md), [values](values.md), [checking](checking.md), [evaluation](evaluation.md), [library](library.md).
