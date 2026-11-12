using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Evaluation;
using Great.Parts.Cel.Types;
using Xunit;
using static Great.Parts.Cel.Tests.Configuration.DescriptionTestSupport;

namespace Great.Parts.Cel.Tests.Configuration;

// The type any of an environment description in JSON inside the parameters of other types, the names of the known messages that
// stand for CEL types, the names that do not, and the set protos by version.
public sealed class AnyInDescriptionTests
{
    // The type any at any depth needs a description of google.protobuf.Any, and the error names the declaration and the type.
    [Theory]
    [InlineData("list<any>")]
    [InlineData("map<string, any>")]
    [InlineData("map<string, list<any>>")]
    [InlineData("type<any>")]
    public static void AnyInsideAType_WithoutAProvider_IsTypeNotDeclaredAtTheVariable(string type)
    {
        string json = """{"name": "m", "variables": [{"name": "x", "type": "TYPE"}]}""".Replace("TYPE", type, System.StringComparison.Ordinal);
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, json));

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.TypeNotDeclared,
            "variable 'x' has type 'any', which no type source or extension of the environment declares",
            "environment JSON 'm'",
            IndexOf(json, "{\"name\": \"x\""));
    }

    [Fact]
    public static void AnyInsideAType_WithTheDescriptionOfTheMessage_Builds()
    {
        const string Json = """{"variables": [{"name": "x", "type": "list<any>"}, {"name": "y", "type": "map<string, any>"}, {"name": "z", "type": "any"}]}""";
        CelEnvironmentBuilder builder = NewBuilder().AddTypes(new ObjectSamples.SampleProvider("known", [MessageSamples.KnownAny()], []));
        Assert.Empty(Apply(builder, Json));

        CelBuildResult result = builder.Build();

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        Assert.Equal(
            [("x", CelType.ListOf(CelType.Any)), ("y", CelType.MapOf(CelType.String, CelType.Any)), ("z", CelType.Any)],
            Variables(builder));
    }

    // The records of the other known messages are the CEL types they stand for and need no description.
    [Theory]
    [InlineData("google.protobuf.Struct", "map(string, dyn)")]
    [InlineData("google.protobuf.ListValue", "list(dyn)")]
    [InlineData("google.protobuf.Value", "dyn")]
    [InlineData("google.protobuf.Int32Value", "wrapper(int)")]
    [InlineData("google.protobuf.UInt64Value", "wrapper(uint)")]
    [InlineData("google.protobuf.FloatValue", "wrapper(double)")]
    [InlineData("list<google.protobuf.StringValue>", "list(wrapper(string))")]
    public static void RecordOfAKnownMessage_IsTheCelTypeItStandsFor(string record, string type)
    {
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, "{\"variables\": [{\"name\": \"x\", \"type\": \"" + record + "\"}]}"));

        Assert.True(builder.Build().IsSuccess);
        (string name, CelType declared) = Assert.Single(Variables(builder));
        Assert.Equal(("x", type), (name, declared.ToString()));
    }

    // timestamp and duration are the types of time; the names google.protobuf.Timestamp and Duration are no records of them.
    [Theory]
    [InlineData("google.protobuf.Timestamp")]
    [InlineData("google.protobuf.Duration")]
    public static void RecordOfTheFullNameOfATimeType_IsTypeNotDeclared(string record)
    {
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, "{\"variables\": [{\"name\": \"x\", \"type\": \"" + record + "\"}]}"));

        Assert.Equal(CelErrorCode.TypeNotDeclared, BuildError(builder).Code);
    }

    [Theory]
    [InlineData("timestamp", "timestamp")]
    [InlineData("duration", "duration")]
    public static void RecordOfATimeType_BuildsBesideTheDescriptionOfAMessage(string record, string type)
    {
        CelEnvironmentBuilder builder = NewBuilder().AddTypes(new ObjectSamples.SampleProvider("known", [MessageSamples.KnownAny()], []));
        Assert.Empty(Apply(builder, "{\"variables\": [{\"name\": \"x\", \"type\": \"" + record + "\"}, {\"name\": \"y\", \"type\": \"any\"}]}"));

        Assert.True(builder.Build().IsSuccess);
        Assert.Equal(type, Variables(builder)[0].Type.ToString());
    }

    // The set protos has one version, 0; another version is the build error of every set, at the element of the description.
    [Theory]
    [InlineData("1")]
    [InlineData("5")]
    public static void SetProtosOfAnotherVersion_IsUnknownExtensionVersion(string version)
    {
        string json = """{"name": "m", "extensions": [{"name": "protos", "version": "VERSION"}]}""".Replace("VERSION", version, System.StringComparison.Ordinal);
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, json));

        CelError error = BuildError(builder);

        Assert.Equal(CelErrorCode.UnknownExtensionVersion, error.Code);
        Assert.Contains("extension 'protos' has no version " + version + "; available versions: 0", error.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public static void SetProtosOfVersionZero_IsInTheEnvironmentOfADescription()
    {
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, """{"extensions": [{"name": "protos", "version": "0"}]}"""));

        Assert.True(builder.Build().IsSuccess);
    }
}
