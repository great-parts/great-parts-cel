# Great.Parts.Cel.Protobuf: instructions for AI coding agents

This file is for an agent that helps a developer use the package `Great.Parts.Cel.Protobuf` in an application. It is not for the contributors of the library; they have `AGENTS.md` in the repository. The API is that of version 2.0 of `Great.Parts.Cel`; versions 1.x are not supported. Read the `AGENTS.md` of `Great.Parts.Cel` first: this package adds only the protobuf part.

## What it is

The package lets CEL expressions work on Protocol Buffers messages: read their fields, test their presence with `has()`, create messages with object literals, compare them, and unpack `google.protobuf.Any`. It depends on `Great.Parts.Cel` and `Google.Protobuf` only, reads and writes fields through the field accessors of `Google.Protobuf` without reflection, and works under trimming and Native AOT.

## When to use it, and when not

Use it when the data that rules read is already protobuf messages: gRPC requests, events, stored messages, configuration in protobuf.

Do not use it when the data are ordinary .NET classes: describe them with a `CelTypeContext` of `Great.Parts.Cel` instead (see its `AGENTS.md`); this package is not needed. Do not use it to parse or write protobuf: it reads messages that exist; `protoc` and `Google.Protobuf` do the rest.

## Install

```shell
dotnet add package Great.Parts.Cel
dotnet add package Great.Parts.Cel.Protobuf
```

Both packages target `net10.0` and `net11.0`. The classes of your messages come from `protoc` generated for C# (`Grpc.Tools` or `Google.Protobuf.Tools`); this package does not generate them.

## Use

Build one provider from the descriptors of the generated files and give it to the environment. The example uses `google.protobuf.Api`, which `Google.Protobuf` ships; the file of your own messages is added the same way (`MyMessagesReflection.Descriptor`):

```csharp
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel;
using Great.Parts.Cel.Protobuf;

CelProtobufTypes types = new CelProtobufTypesBuilder("api")
    .AddFile(ApiReflection.Descriptor)
    .Build();

CelEnvironment environment = CelEnvironment.Create(b => b
    .AddTypes(types)
    .UseContainer("google.protobuf")
    .AddVariable<Api>("api"));

CelProgram<string> label = environment.Compile<string>("api.name + ' v' + api.version").GetProgramOrThrow();
Api value = new() { Name = "orders", Version = "2" };
string text = label.Evaluate(new CelBindings().Add("api", value)).GetValueOrThrow(); // "orders v2"
```

## Rules to know

- **One provider per environment.** Every provider describes the known types of `google/protobuf` (`Any`, `Value`, `Struct`, `ListValue`, `Timestamp`, `Duration`, the wrappers, `Empty`, `FieldMask`), so two providers in one environment are the build error `DuplicateType`. Put all files into one `CelProtobufTypesBuilder`.
- **`AddFile` follows imports.** It describes the messages and enumerations of the file and of every file it imports, directly or not; adding a file twice is one declaration. Proto2 extensions need `AddProtoExtension` with the extension object that `protoc` generates; the file that declares the extension and the file of the extended message must both be added, or `Build()` throws `InvalidOperationException`.
- **Names.** A type has its full protobuf name (`google.protobuf.Api`); `UseContainer("google.protobuf")` lets an expression write the short name. An enumeration constant is `google.protobuf.Syntax.SYNTAX_PROTO3`.
- **Field names are the protobuf names**, in the form that `protoc` writes in the descriptor (`type_url`), not the .NET names (`TypeUrl`).
- **Presence.** `has(m.f)` is true for a set proto2 or `optional` field, for the set member of a `oneof`, for a non-empty `repeated` or `map`, and, for a proto3 scalar without `optional`, when the value differs from the default. Test `has()` when the difference between "absent" and "default" matters. Extensions are selected in backquotes: `` m.`acme.orders.priority` ``.
- **Numbers.** `int32`, `int64` and the like are CEL `int`; `uint32`, `uint64` are `uint`; `float` is widened to `double`. Object literals check ranges: an `int` outside the range of an `int32` field is `ConversionRange`.
- **Wrappers and null.** A wrapper field reads as `null` when it is not set. `google.protobuf.Value` reads as `dyn`; `Struct` as `map(string, dyn)`; `ListValue` as `list(dyn)`; `Timestamp` and `Duration` as `timestamp` and `duration`.
- **`Any`.** An `Any` field is read as the message it contains, if the provider describes the type of its type URL. A type the provider does not know is the error `InvalidAny` (3020), not a silent default. `has()` does not unpack.
- **Object literals** create a new message each time: `acme.orders.Order{total: 10}`. `google.protobuf.Timestamp{…}` and `Duration{…}` are not supported as literals; use `timestamp(…)` and `duration(…)`.
- **Threads.** A provider is immutable and shared by any number of threads and environments; the builder is not thread-safe. Do not change a message while an evaluation reads it: messages are read without a copy.

## Common mistakes

1. Adding the same descriptors to two providers, or one provider per file. Use one builder for the whole environment.
2. Using the .NET property names in expressions. Use the protobuf names.
3. Forgetting `has()` for proto3 scalars and being surprised that "absent" and "default" look the same.
4. Passing a message class that no added file describes: the error is `TypeNotDeclared` at build.
5. Building the provider and the environment on every request. Build them once and compile each expression once (`CelCompilationCache` of `Great.Parts.Cel`).
6. Expecting `Any` of an unknown type to be ignored. Add the file of that message to the provider.
7. Reading the text of an error message instead of its `CelErrorCode`.

## Documentation

The documentation of a release is the content of the repository at the tag of the release; these links point to the tag `v2.0.0` of the repository.

- [Protobuf messages](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/protobuf.md): files, names, fields and their presence, object literals, `Any`, the well-known types and extensions
- [Type mapping](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/type-mapping.md)
- [Error codes](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/error-codes.md)
- [Getting Started](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/getting-started.md): seven scenarios, from the simple entry to a web service
- [README of the repository](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/README.md)
- [Versioning and compatibility](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/docs/versioning.md)
- [Changelog](https://github.com/great-parts/great-parts-cel/blob/v2.0.0/CHANGELOG.md)
