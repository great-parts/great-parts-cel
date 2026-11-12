using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Protobuf;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using Proto2 = global::Cel.Expr.Conformance.Proto2;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Tests.Protobuf;

// What the builder collects and what the provider is, beyond what the examples of the provider fix: the order of the object and
// enumeration types and of the fields, the numbers of the fields, the entry message of a map that is no type, the well-known types
// that are present whatever was added, the constants of the enumerations, an extension that is registered twice or is not
// registered, the refusal of a file with no .NET class, the arguments of the API, a provider built twice and a provider shared by
// threads.
[Trait("Requirement", "F-2")]
[Trait("Requirement", "+R-8")]
public sealed class ProviderBuilderTests
{
    private static readonly string[] KnownMessages =
    [
        "google.protobuf.Any",
        "google.protobuf.BoolValue",
        "google.protobuf.BytesValue",
        "google.protobuf.DoubleValue",
        "google.protobuf.Empty",
        "google.protobuf.FieldMask",
        "google.protobuf.FloatValue",
        "google.protobuf.Int32Value",
        "google.protobuf.Int64Value",
        "google.protobuf.ListValue",
        "google.protobuf.StringValue",
        "google.protobuf.Struct",
        "google.protobuf.UInt32Value",
        "google.protobuf.UInt64Value",
        "google.protobuf.Value",
    ];

    private static readonly KeyValuePair<string, long>[] GlobalEnumConstants =
    [
        new("GOO", 0),
        new("GAR", 1),
        new("GAZ", 2),
    ];

    private static readonly KeyValuePair<string, long>[] NestedEnumConstants =
    [
        new("FOO", 0),
        new("BAR", 1),
        new("BAZ", 2),
    ];

    [Fact]
    public static void ObjectAndEnumTypes_AreInTheOrderOfTheirFullNames()
    {
        CelProtobufTypes provider = ProtobufExample.TypesAll;
        string[] objects = [.. provider.ObjectTypes.Select(type => type.Name)];
        string[] enums = [.. provider.EnumTypes.Select(type => type.Name)];

        Assert.Equal(objects.Order(StringComparer.Ordinal), objects);
        Assert.Equal(enums.Order(StringComparer.Ordinal), enums);
    }

    [Fact]
    public static void FieldNumbers_FollowTheOrderOfDeclaration()
    {
        CelObjectType type = Assert.Single(ProtobufExample.Types3.ObjectTypes, candidate => candidate.Name == ProtobufExample.TypeName3);
        string[] declared = [.. Proto3.TestAllTypes.Descriptor.Fields.InDeclarationOrder().Select(field => field.Name)];

        Assert.Equal(declared, type.Fields.Select(field => field.Name));
        Assert.Equal(Enumerable.Range(0, declared.Length), type.Fields.Select(field => field.Number));
    }

    [Fact]
    public static void Extensions_AreFieldsAfterTheDeclaredOnesInTheOrderOfTheirFullNames()
    {
        CelObjectType type = Assert.Single(ProtobufExample.TypesAll.ObjectTypes, candidate => candidate.Name == "cel.expr.conformance.proto2.TestAllTypes");
        int declared = Proto2.TestAllTypes.Descriptor.Fields.InDeclarationOrder().Count;
        string[] extensions = [.. type.Fields.Skip(declared).Select(field => field.Name)];

        Assert.Equal(5, extensions.Length);
        Assert.All(extensions, name => Assert.StartsWith("cel.expr.conformance.proto2.", name, StringComparison.Ordinal));
        Assert.Equal(extensions.Order(StringComparer.Ordinal), extensions);
        Assert.Contains("cel.expr.conformance.proto2.int32_ext", extensions);
        Assert.Contains("cel.expr.conformance.proto2.Proto2ExtensionScopedMessage.int64_ext", extensions);
    }

    [Fact]
    public static void Extension_OfAFileThatNobodyRegistered_IsNoField()
    {
        CelProtobufTypes provider = new CelProtobufTypesBuilder("p")
            .AddFile(Proto2.TestAllTypesExtensionsReflection.Descriptor)
            .AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.Int32Ext)
            .Build();
        CelObjectType type = Assert.Single(provider.ObjectTypes, candidate => candidate.Name == "cel.expr.conformance.proto2.TestAllTypes");

        Assert.True(type.TryGetField("cel.expr.conformance.proto2.int32_ext", out _));
        Assert.False(type.TryGetField("cel.expr.conformance.proto2.nested_ext", out _));
    }

    [Fact]
    public static void ExtensionRegisteredTwice_IsOneField()
    {
        CelProtobufTypes provider = new CelProtobufTypesBuilder("p")
            .AddFile(Proto2.TestAllTypesExtensionsReflection.Descriptor)
            .AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.Int32Ext)
            .AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.Int32Ext)
            .AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.RepeatedTestAllTypes)
            .AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.RepeatedTestAllTypes)
            .Build();
        CelObjectType type = Assert.Single(provider.ObjectTypes, candidate => candidate.Name == "cel.expr.conformance.proto2.TestAllTypes");

        Assert.Single(type.Fields, field => field.Name == "cel.expr.conformance.proto2.int32_ext");
        Assert.Single(type.Fields, field => field.Name == "cel.expr.conformance.proto2.repeated_test_all_types");
    }

    // The types of the fields of the extensions follow their descriptors: a number, an enumeration as an int, a message as an object, a
    // repeated extension as a list.
    [Fact]
    public static void ExtensionFields_HaveTheCelTypeOfTheirDescriptors()
    {
        CelObjectType type = Assert.Single(ProtobufExample.TypesAll.ObjectTypes, candidate => candidate.Name == "cel.expr.conformance.proto2.TestAllTypes");
        CelType message = CelType.ObjectNamed("cel.expr.conformance.proto2.TestAllTypes");

        AssertFieldType(type, "cel.expr.conformance.proto2.int32_ext", CelType.Int);
        AssertFieldType(type, "cel.expr.conformance.proto2.Proto2ExtensionScopedMessage.int64_ext", CelType.Int);
        AssertFieldType(type, "cel.expr.conformance.proto2.nested_enum_ext", CelType.Int);
        AssertFieldType(type, "cel.expr.conformance.proto2.nested_ext", message);
        AssertFieldType(type, "cel.expr.conformance.proto2.repeated_test_all_types", CelType.ListOf(message));
    }

    // The entry message of a map field is the machinery of the field, not a message an expression can name.
    [Fact]
    public static void MapEntryMessages_AreNoTypes()
    {
        string[] names = [.. ProtobufExample.TypesAll.ObjectTypes.Select(type => type.Name)];

        Assert.DoesNotContain(names, name => name.EndsWith("Entry", StringComparison.Ordinal));
        Assert.Contains(names, name => name == "cel.expr.conformance.proto3.TestAllTypes.NestedMessage");
    }

    [Fact]
    public static void ImportsReachedFromTwoFiles_AreDescribedOnce()
    {
        CelProtobufTypes provider = new CelProtobufTypesBuilder("p")
            .AddFile(Proto2.TestAllTypesExtensionsReflection.Descriptor)
            .AddFile(Proto2.TestAllTypesReflection.Descriptor)
            .Build();
        string[] names = [.. provider.ObjectTypes.Select(type => type.Name)];

        Assert.Equal(names.Distinct(StringComparer.Ordinal).Count(), names.Length);
    }

    [Fact]
    public static void WellKnownTypes_AreThoseThatCelDoesNotConvertToAnotherType()
    {
        CelProtobufTypes provider = new CelProtobufTypesBuilder("empty").Build();

        Assert.Equal(KnownMessages, provider.ObjectTypes.Select(type => type.Name));
        Assert.Equal("google.protobuf.NullValue", Assert.Single(provider.EnumTypes).Name);
        Assert.DoesNotContain(provider.ObjectTypes, type => type.Name is "google.protobuf.Timestamp" or "google.protobuf.Duration");
    }

    [Fact]
    public static void AddFile_OfAWellKnownFile_ChangesNothing()
    {
        CelProtobufTypes plain = new CelProtobufTypesBuilder("p").Build();
        CelProtobufTypes added = new CelProtobufTypesBuilder("p")
            .AddFile(TimestampReflection.Descriptor)
            .AddFile(StructReflection.Descriptor)
            .AddFile(WrappersReflection.Descriptor)
            .Build();

        Assert.Equal(plain.ObjectTypes.Select(type => type.Name), added.ObjectTypes.Select(type => type.Name));
        Assert.Equal(plain.EnumTypes.Select(type => type.Name), added.EnumTypes.Select(type => type.Name));
    }

    // An enumeration at the top level of a file has the full name of the package and no enclosing message; the constants are the names
    // and the numbers of its values.
    [Fact]
    public static void Enumerations_HaveTheirFullNamesAndConstants()
    {
        CelProtobufTypes provider = ProtobufExample.Types3;
        CelEnumType global = Assert.Single(provider.EnumTypes, type => type.Name == "cel.expr.conformance.proto3.GlobalEnum");
        CelEnumType nested = Assert.Single(provider.EnumTypes, type => type.Name == "cel.expr.conformance.proto3.TestAllTypes.NestedEnum");

        KeyValuePair<string, long>[] globalConstants = [.. global.Constants];
        KeyValuePair<string, long>[] nestedConstants = [.. nested.Constants];
        Assert.Equal(GlobalEnumConstants, globalConstants);
        Assert.Equal(NestedEnumConstants, nestedConstants);
    }

    // A file read from serialized data, as a descriptor set gives it, has messages with no .NET class: no field of it could be read.
    [Fact]
    public static void AddFile_OfAFileWithNoDotNetClass_IsArgumentException()
    {
        var proto = new FileDescriptorProto { Name = "plain.proto", Package = "plain", Syntax = "proto3" };
        var message = new DescriptorProto { Name = "Plain" };
        message.Field.Add(new FieldDescriptorProto
        {
            Name = "a",
            Number = 1,
            Type = FieldDescriptorProto.Types.Type.Int32,
            Label = FieldDescriptorProto.Types.Label.Optional,
        });
        proto.MessageType.Add(message);
        FileDescriptor file = FileDescriptor.BuildFromByteStrings([proto.ToByteString()])[0];
        var builder = new CelProtobufTypesBuilder("p");

        ArgumentException error = Assert.Throws<ArgumentException>(() => builder.AddFile(file));

        Assert.Contains("plain.proto", error.Message, StringComparison.Ordinal);
        Assert.Contains("plain.Plain", error.Message, StringComparison.Ordinal);
        Assert.Equal(KnownMessages, builder.Build().ObjectTypes.Select(type => type.Name));
    }

    [Fact]
    public static void Arguments_AreChecked()
    {
        var builder = new CelProtobufTypesBuilder("p");

        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new CelProtobufTypesBuilder(null!);
        });
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new CelProtobufTypesBuilder(string.Empty);
        });
        Assert.Throws<ArgumentNullException>(() => builder.AddFile(null!));
        Assert.Throws<ArgumentNullException>(() => builder.AddProtoExtension((Extension<Proto2.TestAllTypes, int>)null!));
        Assert.Throws<ArgumentNullException>(() => builder.AddProtoExtension((RepeatedExtension<Proto2.TestAllTypes, Proto2.TestAllTypes>)null!));
    }

    // Two providers that one builder gives are equal descriptions that are different objects, so they cannot share an environment.
    [Fact]
    public static void BuildTwice_GivesIndependentProviders()
    {
        var builder = new CelProtobufTypesBuilder("p").AddFile(Proto3.TestAllTypesReflection.Descriptor);
        CelProtobufTypes first = builder.Build();
        CelProtobufTypes second = builder.Build();

        Assert.NotSame(first, second);
        Assert.Equal(first.ObjectTypes.Select(type => type.Name), second.ObjectTypes.Select(type => type.Name));
        Assert.All(first.ObjectTypes.Zip(second.ObjectTypes), pair => Assert.NotSame(pair.First, pair.Second));
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddTypes(first).AddTypes(second).Build();
        Assert.False(result.IsSuccess);
        Assert.All(result.Errors, error => Assert.Equal(CelErrorCode.DuplicateType, error.Code));
    }

    // The provider is immutable and its environment is shared: many threads read fields of messages through it at once.
    [Fact]
    public static void Provider_IsSharedByManyThreads()
    {
        var results = new ConcurrentBag<bool>();

        Parallel.For(0, 64, index =>
        {
            var message = new Proto3.TestAllTypes { SingleInt32 = index, RepeatedInt32 = { 1, 2, index }, MapStringInt64 = { ["a"] = index } };
            results.Add(Reading.IsTrue("x.single_int32 + x.repeated_int32[2] == " + (2 * index) + " && x.map_string_int64['a'] == " + index, message));
        });

        Assert.Equal(64, results.Count);
        Assert.All(results, result => Assert.True(result));
    }

    // The order of the calls of the builder changes nothing: the three files, two extensions and a call made twice, in every one of the
    // 720 orders, give providers that are equal by the names and the order of the types, by the name, number and type of each field,
    // and by the constants of the enumerations.
    [Fact]
    public static void EveryOrderOfTheCalls_GivesTheSameProvider()
    {
        Action<CelProtobufTypesBuilder>[] calls =
        [
            builder => builder.AddFile(Proto3.TestAllTypesReflection.Descriptor),
            builder => builder.AddFile(Proto2.TestAllTypesReflection.Descriptor),
            builder => builder.AddFile(Proto2.TestAllTypesExtensionsReflection.Descriptor),
            builder => builder.AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.Int32Ext),
            builder => builder.AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.RepeatedTestAllTypes),
            builder => builder.AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.Int32Ext),
        ];
        string[] expected = Describe(Build(calls, [0, 1, 2, 3, 4, 5]));
        Assert.Contains(expected, line => line.EndsWith(" cel.expr.conformance.proto2.int32_ext int", StringComparison.Ordinal));
        Assert.Contains(expected, line => line.EndsWith(" cel.expr.conformance.proto2.repeated_test_all_types list(cel.expr.conformance.proto2.TestAllTypes)", StringComparison.Ordinal));

        var different = new List<string>();
        foreach (int[] order in Permutations([0, 1, 2, 3, 4, 5]))
        {
            if (!Describe(Build(calls, order)).SequenceEqual(expected, StringComparer.Ordinal))
            {
                different.Add(string.Join(' ', order));
            }
        }

        Assert.Empty(different);
    }

    // The extension whose file was not added is the same exception in every order of the calls: the same text, with every extension named.
    [Fact]
    public static void EveryOrderOfTheCalls_GivesTheSameExceptionWhenAFileIsMissing()
    {
        Action<CelProtobufTypesBuilder>[] calls =
        [
            builder => builder.AddFile(Proto3.TestAllTypesReflection.Descriptor),
            builder => builder.AddFile(Proto2.TestAllTypesReflection.Descriptor),
            builder => builder.AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.Int32Ext),
            builder => builder.AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.RepeatedTestAllTypes),
            builder => builder.AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.Int32Ext),
        ];
        string? expected = null;
        foreach (int[] order in Permutations([0, 1, 2, 3, 4]))
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Build(calls, order));

            expected ??= error.Message;
            Assert.Equal(expected, error.Message);
        }

        Assert.NotNull(expected);
        Assert.Contains("is declared in a file that was not added", expected, StringComparison.Ordinal);
        Assert.Equal(2, expected.Split(';').Length);
    }

    private static CelProtobufTypes Build(Action<CelProtobufTypesBuilder>[] calls, int[] order)
    {
        var builder = new CelProtobufTypesBuilder("p");
        foreach (int index in order)
        {
            calls[index](builder);
        }

        return builder.Build();
    }

    // Everything a provider declares, in its order: the object types with the number, name and type of each field, then the
    // enumerations with their constants.
    private static string[] Describe(CelProtobufTypes provider)
    {
        var lines = new List<string>();
        foreach (CelObjectType type in provider.ObjectTypes)
        {
            lines.Add("object " + type.Name + " " + type.Type);
            foreach (CelObjectField field in type.Fields)
            {
                lines.Add(string.Create(CultureInfo.InvariantCulture, $"{field.Number} {field.Name} {field.Type}"));
            }
        }

        foreach (CelEnumType type in provider.EnumTypes)
        {
            lines.Add("enum " + type.Name + " " + string.Join(',', type.Constants.Select(constant => string.Create(CultureInfo.InvariantCulture, $"{constant.Key}={constant.Value}"))));
        }

        return [.. lines];
    }

    private static IEnumerable<int[]> Permutations(int[] items)
    {
        if (items.Length <= 1)
        {
            yield return items;
            yield break;
        }

        for (int index = 0; index < items.Length; index++)
        {
            int[] rest = [.. items.Where((_, position) => position != index)];
            foreach (int[] tail in Permutations(rest))
            {
                yield return [items[index], .. tail];
            }
        }
    }

    private static void AssertFieldType(CelObjectType type, string name, CelType expected)
    {
        Assert.True(type.TryGetField(name, out CelObjectField? field), name);
        Assert.NotNull(field);
        Assert.Equal(expected, field.Type);
    }
}
