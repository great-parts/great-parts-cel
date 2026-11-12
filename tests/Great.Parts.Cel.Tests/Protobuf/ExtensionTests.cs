using System.Threading;
using Great.Parts.Cel.Protobuf;
using Xunit;
using Proto2 = global::Cel.Expr.Conformance.Proto2;

namespace Great.Parts.Cel.Tests.Protobuf;

// Proto2 extensions beyond the examples: two extensions with one short name, one at the top level of the file and one declared in a
// message, are two fields with their own full names and their own values, and the typed objects read what the message holds
// for each of them.
[Trait("Requirement", "F-2")]
public sealed class ExtensionTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddTypes(new CelProtobufTypesBuilder("extensions")
            .AddFile(Proto2.TestAllTypesExtensionsReflection.Descriptor)
            .AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.NestedEnumExt)
            .AddProtoExtension(Proto2.Proto2ExtensionScopedMessage.Extensions.NestedEnumExt)
            .AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.TestAllTypesExt)
            .AddProtoExtension(Proto2.Proto2ExtensionScopedMessage.Extensions.MessageScopedNestedExt)
            .AddProtoExtension(Proto2.Proto2ExtensionScopedMessage.Extensions.MessageScopedRepeatedTestAllTypes)
            .Build())
        .AddExtension(CelExtensions.Protos(0))
        .AddVariable<Proto2.TestAllTypes>("p")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public static void ExtensionsWithOneShortName_AreTwoFieldsWithTheirOwnValues()
    {
        var message = new Proto2.TestAllTypes();
        message.SetExtension(Proto2.TestAllTypesExtensionsExtensions.NestedEnumExt, Proto2.TestAllTypes.Types.NestedEnum.Bar);
        message.SetExtension(Proto2.Proto2ExtensionScopedMessage.Extensions.NestedEnumExt, Proto2.TestAllTypes.Types.NestedEnum.Baz);

        Assert.True(IsTrue("proto.getExt(p, cel.expr.conformance.proto2.nested_enum_ext) == 1", message));
        Assert.True(IsTrue("proto.getExt(p, cel.expr.conformance.proto2.Proto2ExtensionScopedMessage.nested_enum_ext) == 2", message));
        Assert.True(IsTrue("proto.hasExt(p, cel.expr.conformance.proto2.nested_enum_ext) && proto.hasExt(p, cel.expr.conformance.proto2.Proto2ExtensionScopedMessage.nested_enum_ext)", message));
    }

    // Only the extension that was set is set: the other one with the same short name reads its default.
    [Fact]
    public static void ExtensionWithTheShortNameOfAnother_IsNotSetByIt()
    {
        var message = new Proto2.TestAllTypes();
        message.SetExtension(Proto2.Proto2ExtensionScopedMessage.Extensions.NestedEnumExt, Proto2.TestAllTypes.Types.NestedEnum.Baz);

        Assert.False(IsTrue("proto.hasExt(p, cel.expr.conformance.proto2.nested_enum_ext)", message));
        Assert.True(IsTrue("proto.hasExt(p, cel.expr.conformance.proto2.Proto2ExtensionScopedMessage.nested_enum_ext)", message));
        Assert.True(IsTrue("proto.getExt(p, cel.expr.conformance.proto2.nested_enum_ext) == 0", message));
    }

    // A message extension reads the instance that was set, and a repeated one its list; the top-level and the message-scoped extensions
    // of one message type are separate, and an extension that is not set is not present.
    [Fact]
    public static void MessageExtension_ReadsItsInstance()
    {
        var message = new Proto2.TestAllTypes();
        message.SetExtension(Proto2.TestAllTypesExtensionsExtensions.TestAllTypesExt, new Proto2.TestAllTypes { SingleInt32 = 11 });
        message.SetExtension(Proto2.Proto2ExtensionScopedMessage.Extensions.MessageScopedNestedExt, new Proto2.TestAllTypes { SingleInt32 = 12 });
        message.GetOrInitializeExtension(Proto2.Proto2ExtensionScopedMessage.Extensions.MessageScopedRepeatedTestAllTypes).Add(new Proto2.TestAllTypes { SingleInt32 = 13 });

        Assert.True(IsTrue("proto.getExt(p, cel.expr.conformance.proto2.test_all_types_ext).single_int32 == 11", message));
        Assert.True(IsTrue("proto.getExt(p, cel.expr.conformance.proto2.Proto2ExtensionScopedMessage.message_scoped_nested_ext).single_int32 == 12", message));
        Assert.True(IsTrue("proto.getExt(p, cel.expr.conformance.proto2.Proto2ExtensionScopedMessage.message_scoped_repeated_test_all_types)[0].single_int32 == 13", message));
        Assert.True(IsTrue("size(proto.getExt(p, cel.expr.conformance.proto2.Proto2ExtensionScopedMessage.message_scoped_repeated_test_all_types)) == 1", message));
        Assert.False(IsTrue("proto.hasExt(p, cel.expr.conformance.proto2.test_all_types_ext)", new Proto2.TestAllTypes()));
    }

    private static bool IsTrue(string expression, Proto2.TestAllTypes message)
    {
        CelCompileResult<bool> compiled = Environment.Compile<bool>(expression);
        Assert.True(compiled.IsSuccess, expression);
        return compiled.GetProgramOrThrow().Evaluate(new CelBindings().Add("p", message), cancellationToken: CancellationToken.None).GetValueOrThrow();
    }
}
