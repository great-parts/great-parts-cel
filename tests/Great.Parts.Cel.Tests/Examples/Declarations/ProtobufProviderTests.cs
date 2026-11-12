using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Protobuf;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using Proto2 = global::Cel.Expr.Conformance.Proto2;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Tests.Examples.Declarations;

// The provider of protobuf messages, CelProtobufTypesBuilder and CelProtobufTypes (the type mapping page, the rules of the
// table of protobuf messages that name the provider): AddFile adds the messages and the enumerations of a file, the nested
// ones included, and of the files it imports, and adding a file again has no effect; the well-known types are always
// described; an extension added with AddProtoExtension must have its declaring file in the builder, or Build
// throws InvalidOperationException; a provider built is not changed by what is added to the builder later. In one
// environment all files go into one provider: two providers of protobuf share the well-known types, so they are
// DuplicateType (4001), as are two that share a file, whichever is added first; one provider added twice is one declaration.
// A .NET class of a well-known type used outside a field of a message — AddVariable<Timestamp> — is TypeNotDeclared (4008), and a
// value of it in a dyn place TypeNotAccessible (3012); a message class that no provider describes is TypeNotDeclared too.
[Trait("Requirement", "F-2")]
[Trait("Requirement", "+R-8")]
public sealed class ProtobufProviderTests
{
    private static readonly (string TypeName, Func<CelEnvironmentBuilder, CelEnvironmentBuilder> Declare)[] WellKnownClasses =
    [
        ("Google.Protobuf.WellKnownTypes.Timestamp", builder => builder.AddVariable<Timestamp>("v")),
        ("Google.Protobuf.WellKnownTypes.Duration", builder => builder.AddVariable<Duration>("v")),
        ("Google.Protobuf.WellKnownTypes.Struct", builder => builder.AddVariable<Struct>("v")),
        ("Google.Protobuf.WellKnownTypes.Value", builder => builder.AddVariable<Value>("v")),
        ("Google.Protobuf.WellKnownTypes.ListValue", builder => builder.AddVariable<ListValue>("v")),
        ("Google.Protobuf.WellKnownTypes.Any", builder => builder.AddVariable<Any>("v")),
        ("Google.Protobuf.WellKnownTypes.Int64Value", builder => builder.AddVariable<Int64Value>("v")),
        ("Google.Protobuf.WellKnownTypes.StringValue", builder => builder.AddVariable<StringValue>("v")),
    ];

    [Fact]
    public void AddFile_Twice_HasNoEffect()
    {
        CelProtobufTypes once = new CelProtobufTypesBuilder("p").AddFile(Proto3.TestAllTypesReflection.Descriptor).Build();
        CelProtobufTypes twice = new CelProtobufTypesBuilder("p")
            .AddFile(Proto3.TestAllTypesReflection.Descriptor)
            .AddFile(Proto3.TestAllTypesReflection.Descriptor)
            .Build();

        Assert.Equal(Names(once), Names(twice));
        Assert.Equal(once.ObjectTypes.Length, twice.ObjectTypes.Length);
        Assert.Equal(once.EnumTypes.Length, twice.EnumTypes.Length);
    }

    // The messages and enumerations of a file, nested ones included, with the full names of protobuf; and the files that
    // the file imports, here the proto2 messages that the extension file imports.
    [Fact]
    public void AddFile_AddsNestedTypesAndImportedFiles()
    {
        CelProtobufTypes proto3 = new CelProtobufTypesBuilder("p").AddFile(Proto3.TestAllTypesReflection.Descriptor).Build();
        CelProtobufTypes extensions = new CelProtobufTypesBuilder("p").AddFile(Proto2.TestAllTypesExtensionsReflection.Descriptor).Build();

        Assert.Subset(
            Names(proto3).ToHashSet(),
            new HashSet<string>
            {
                "cel.expr.conformance.proto3.TestAllTypes",
                "cel.expr.conformance.proto3.TestAllTypes.NestedMessage",
                "cel.expr.conformance.proto3.NestedTestAllTypes",
            });
        Assert.Contains(proto3.EnumTypes, type => type.Name == "cel.expr.conformance.proto3.TestAllTypes.NestedEnum");
        Assert.Contains(proto3.EnumTypes, type => type.Name == "cel.expr.conformance.proto3.GlobalEnum");
        Assert.Subset(
            Names(extensions).ToHashSet(),
            new HashSet<string>
            {
                "cel.expr.conformance.proto2.Proto2ExtensionScopedMessage",
                "cel.expr.conformance.proto2.TestAllTypes",
                "cel.expr.conformance.proto2.TestAllTypes.NestedMessage",
            });
        Assert.DoesNotContain("cel.expr.conformance.proto3.TestAllTypes", Names(extensions));
    }

    // A provider with no file describes the well-known types, so the name any, a wrapper literal and the other types of
    // google/protobuf work.
    [Fact]
    public void WellKnownTypes_AreAlwaysDescribed()
    {
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(new CelProtobufTypesBuilder("empty").Build())
            .AddVariable("a", CelType.Any);
        CelEnvironment environment = builder.Build().GetEnvironmentOrThrow();

        Assert.True(environment.Compile<bool>("google.protobuf.Int64Value{value: 5} == 5 && google.protobuf.StringValue{value: 'a'} == 'a'")
            .GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // The extension is declared in a file of the builder, which brings with it the file of the extended message through its import;
    // an extension whose file is not in the builder, or whose file is only the one of the message it extends, is an error of Build.
    [Fact]
    public void ExtensionWhoseFileIsNotAdded_IsInvalidOperationExceptionAtBuild()
    {
        var none = new CelProtobufTypesBuilder("p").AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.Int32Ext);
        var messageOnly = new CelProtobufTypesBuilder("p")
            .AddFile(Proto2.TestAllTypesReflection.Descriptor)
            .AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.Int32Ext);
        var repeated = new CelProtobufTypesBuilder("p").AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.RepeatedTestAllTypes);
        var scoped = new CelProtobufTypesBuilder("p")
            .AddFile(Proto2.TestAllTypesReflection.Descriptor)
            .AddProtoExtension(Proto2.Proto2ExtensionScopedMessage.Extensions.Int64Ext);

        Assert.Throws<InvalidOperationException>(() => none.Build());
        Assert.Throws<InvalidOperationException>(() => messageOnly.Build());
        Assert.Throws<InvalidOperationException>(() => repeated.Build());
        Assert.Throws<InvalidOperationException>(() => scoped.Build());
    }

    [Fact]
    public void ExtensionWhoseFileIsAdded_BuildsInAnyOrderOfTheCalls()
    {
        CelProtobufTypes before = new CelProtobufTypesBuilder("p")
            .AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.Int32Ext)
            .AddFile(Proto2.TestAllTypesExtensionsReflection.Descriptor)
            .Build();
        CelProtobufTypes after = new CelProtobufTypesBuilder("p")
            .AddFile(Proto2.TestAllTypesExtensionsReflection.Descriptor)
            .AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.Int32Ext)
            .Build();

        Assert.Equal(Names(before), Names(after));
    }

    // A provider is a snapshot: the builder goes on, and the provider that it gave before does not change.
    [Fact]
    public void ProviderBuilt_IsNotChangedByLaterAdditions()
    {
        var builder = new CelProtobufTypesBuilder("p").AddFile(Proto3.TestAllTypesReflection.Descriptor);
        CelProtobufTypes first = builder.Build();
        int count = first.ObjectTypes.Length;

        CelProtobufTypes second = builder.AddFile(Proto2.TestAllTypesReflection.Descriptor).Build();

        Assert.Equal(count, first.ObjectTypes.Length);
        Assert.True(second.ObjectTypes.Length > count);
        Assert.Equal("p", first.SourceName);
    }

    // Two sources that share a file: each type of the file is declared twice, and the text names the sources in the order of
    // their texts, so the order of the calls changes nothing.
    [Fact]
    public void TwoProvidersThatShareAFile_AreDuplicateType()
    {
        CelProtobufTypes first = new CelProtobufTypesBuilder("first").AddFile(Proto3.TestAllTypesReflection.Descriptor).Build();
        CelProtobufTypes second = new CelProtobufTypesBuilder("second").AddFile(Proto3.TestAllTypesReflection.Descriptor).Build();

        BuildErrors forward = Errors(first, second);
        BuildErrors backward = Errors(second, first);

        Assert.Contains(
            "type 'cel.expr.conformance.proto3.TestAllTypes' is declared by both type source 'first' and type source 'second'",
            forward.Messages);
        Assert.Contains(
            "type 'cel.expr.conformance.proto3.NestedTestAllTypes' is declared by both type source 'first' and type source 'second'",
            forward.Messages);
        Assert.Equal(forward.Messages, backward.Messages);
        Assert.All(forward.Codes, code => Assert.Equal(CelErrorCode.DuplicateType, code));
    }

    // The well-known types are in every provider: two providers of protobuf with no file in common are DuplicateType too.
    [Fact]
    public void TwoProvidersWithDifferentFiles_AreDuplicateTypeInTheWellKnownTypes()
    {
        CelProtobufTypes proto3 = new CelProtobufTypesBuilder("proto3").AddFile(Proto3.TestAllTypesReflection.Descriptor).Build();
        CelProtobufTypes proto2 = new CelProtobufTypesBuilder("proto2").AddFile(Proto2.TestAllTypesReflection.Descriptor).Build();

        BuildErrors errors = Errors(proto3, proto2);

        Assert.NotEmpty(errors.Messages);
        Assert.All(errors.Codes, code => Assert.Equal(CelErrorCode.DuplicateType, code));
        Assert.All(errors.Messages, message => Assert.EndsWith("is declared by both type source 'proto2' and type source 'proto3'", message, StringComparison.Ordinal));
        Assert.DoesNotContain(errors.Messages, message => message.Contains("cel.expr.conformance", StringComparison.Ordinal));
    }

    [Fact]
    public void OneProviderAddedTwice_IsOneDeclaration()
    {
        CelProtobufTypes provider = new CelProtobufTypesBuilder("p").AddFile(Proto3.TestAllTypesReflection.Descriptor).Build();

        Assert.True(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddTypes(provider).AddTypes(provider).Build().IsSuccess);
        Assert.True(new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(new CelProtobufTypesBuilder("both").AddFile(Proto3.TestAllTypesReflection.Descriptor).AddFile(Proto2.TestAllTypesReflection.Descriptor).Build())
            .Build()
            .IsSuccess);
    }

    // The class of a well-known type has no type of its own outside a field of a message: it is TypeNotDeclared as a variable,
    // with a provider that describes the well-known types as well as without one; the instance in a dyn place is TypeNotAccessible,
    // at the selection that needs its description.
    [Fact]
    [Trait("Requirement", "F-3")]
    public void ClassOfAWellKnownType_IsTypeNotDeclaredAsAVariable()
    {
        foreach ((string typeName, Func<CelEnvironmentBuilder, CelEnvironmentBuilder> declare) in WellKnownClasses)
        {
            foreach (CelProtobufTypes? provider in new CelProtobufTypes?[] { null, ProtobufExample.Types3 })
            {
                CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1);
                if (provider is not null)
                {
                    builder.AddTypes(provider);
                }

                CelBuildResult result = declare(builder).Build();

                CelError error = Assert.Single(result.Errors);
                Assert.Equal(
                    (CelErrorCode.TypeNotDeclared, CelErrorStage.Build, "variable 'v' has type '" + typeName + "', which no type source or extension of the environment declares"),
                    (error.Code, error.Stage, error.Message));
            }
        }
    }

    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "F-4")]
    public void InstanceOfAWellKnownTypeInADynPlace_IsTypeNotAccessible()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(ProtobufExample.Types3)
            .AddVariable("v", CelType.Dyn)
            .Build()
            .GetEnvironmentOrThrow();

        CelError error = Assert.Single(environment.Compile<CelValue>("v.seconds").GetProgramOrThrow()
            .Evaluate(new CelBindings().Add<object>("v", new Timestamp { Seconds = 1 }), cancellationToken: CancellationToken.None).Errors);

        Assert.Equal((CelErrorCode.TypeNotAccessible, CelErrorStage.Evaluate, 1), (error.Code, error.Stage, error.Location.Offset));
        Assert.Equal(
            ".NET type 'Google.Protobuf.WellKnownTypes.Timestamp' is not available to expressions: no type source of the environment describes it",
            error.Message);
    }

    // A class that protoc generated is the message type of its description, found by the .NET type when Build resolves the
    // variable; without a provider, or with one that describes another file, it is TypeNotDeclared.
    [Fact]
    [Trait("Requirement", "F-3")]
    public void MessageClass_IsDeclaredByTheProviderThatDescribesItsFile()
    {
        const string Message = "variable 'x' has type 'Cel.Expr.Conformance.Proto3.TestAllTypes', which no type source or extension of the environment declares";
        CelProtobufTypes proto2 = new CelProtobufTypesBuilder("proto2").AddFile(Proto2.TestAllTypesReflection.Descriptor).Build();

        CelError withoutProvider = Assert.Single(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<Proto3.TestAllTypes>("x").Build().Errors);
        CelError otherProvider = Assert.Single(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddTypes(proto2).AddVariable<Proto3.TestAllTypes>("x").Build().Errors);
        CelEnvironment declared = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(ProtobufExample.Types3)
            .AddVariable<Proto3.TestAllTypes>("x")
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Equal((CelErrorCode.TypeNotDeclared, Message), (withoutProvider.Code, withoutProvider.Message));
        Assert.Equal((CelErrorCode.TypeNotDeclared, Message), (otherProvider.Code, otherProvider.Message));
        Assert.Equal(
            CelType.ObjectNamed(ProtobufExample.TypeName3),
            declared.Compile<CelValue>("x").GetProgramOrThrow().ResultType);
    }

    private static IEnumerable<string> Names(CelProtobufTypes provider) =>
        provider.ObjectTypes.Select(type => type.Name).Concat(provider.EnumTypes.Select(type => type.Name)).Order(StringComparer.Ordinal);

    private static BuildErrors Errors(params CelProtobufTypes[] providers)
    {
        CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);
        foreach (CelProtobufTypes provider in providers)
        {
            builder.AddTypes(provider);
        }

        CelBuildResult result = builder.Build();
        Assert.False(result.IsSuccess);
        return new BuildErrors(
            [.. result.Errors.Select(error => error.Message)],
            [.. result.Errors.Select(error => error.Code)]);
    }

    private sealed record BuildErrors(string[] Messages, CelErrorCode[] Codes);
}
