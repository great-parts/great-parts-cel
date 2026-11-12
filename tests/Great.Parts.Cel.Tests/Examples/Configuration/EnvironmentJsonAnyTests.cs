using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Configuration;

// The type any of the environment description in JSON (the page of the description, "Type records"): it is the name
// google.protobuf.Any, so with a provider of protobuf in the builder the variable has the type any, and without one the name
// is declared by no source and Build says TypeNotDeclared, with the place of the variable in the JSON. The same holds for the name of a message
// of the provider: it is the type of the variable when a provider describes it and TypeNotDeclared when none does.
// A type record is read when the description is applied and resolved when the environment is built, so the
// provider can be added before or after the description.
[Trait("Requirement", "F-7")]
[Trait("Requirement", "F-2")]
public sealed class EnvironmentJsonAnyTests
{
    private const string Json = """{"name": "m", "variables": [{"name": "x", "type": "any"}]}""";

    [Fact]
    public void Any_WithAProviderOfProtobuf_IsTheTypeAny()
    {
        CelEnvironmentBuilder before = JsonExample.NewBuilder().AddTypes(ProtobufExample.Types3);
        CelEnvironmentBuilder after = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(before, Json));
        Assert.Empty(JsonExample.Apply(after, Json));
        after.AddTypes(ProtobufExample.Types3);

        foreach (CelEnvironmentBuilder builder in new[] { before, after })
        {
            Assert.Equal(new[] { ("x", CelType.Any) }, JsonExample.Variables(builder));
            Assert.True(builder.Build().IsSuccess);
        }
    }

    [Fact]
    public void Any_WithoutAProviderOfProtobuf_IsTypeNotDeclaredAtTheVariable()
    {
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, Json));

        CelError error = JsonExample.BuildError(builder);

        Assert.Equal(CelErrorCode.TypeNotDeclared, error.Code);
        Assert.StartsWith("variable 'x' has type '", error.Message, StringComparison.Ordinal);
        Assert.Contains("which no type source or extension of the environment declares", error.Message, StringComparison.Ordinal);
        CelSourceLocation place = Assert.NotNull(error.SourceLocation);
        Assert.Equal(
            ("environment JSON 'm'", Json.IndexOf("{\"name\": \"x\"", StringComparison.Ordinal)),
            (place.SourceName, place.Offset));
    }

    // The name of a message is the type of a variable when a provider describes the message; the name of a type of a provider that
    // is not added is TypeNotDeclared.
    [Fact]
    public void NameOfAMessage_IsDeclaredByTheProviderThatDescribesIt()
    {
        const string Description = """{"name": "m", "variables": [{"name": "x", "type": "cel.expr.conformance.proto3.TestAllTypes"}, {"name": "ys", "type": "list<cel.expr.conformance.proto3.TestAllTypes.NestedMessage>"}]}""";
        var with = JsonExample.NewBuilder().AddTypes(ProtobufExample.Types3);
        var without = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(with, Description));
        Assert.Empty(JsonExample.Apply(without, Description));

        CelEnvironment environment = with.Build().GetEnvironmentOrThrow();
        CelBuildResult missing = without.Build();

        Assert.Equal(CelType.ObjectNamed(ProtobufExample.TypeName3), environment.Compile<CelValue>("x").GetProgramOrThrow().ResultType);
        Assert.Equal(CelType.ListOf(CelType.ObjectNamed(ProtobufExample.NestedName3)), environment.Compile<CelValue>("ys").GetProgramOrThrow().ResultType);
        Assert.All(missing.Errors, error => Assert.Equal(CelErrorCode.TypeNotDeclared, error.Code));
        Assert.Equal(2, missing.Errors.Length);
    }
}
