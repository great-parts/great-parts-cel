using System;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// CelEnvironmentBuilder.AddAbbreviation (the rules of common/containers of cel-go, Abbrevs and aliasAs): the last part of a qualified
// name is a short name; a name of an expression whose first part is the short name resolves only to the qualified name, instead of the
// candidates of the container, for identifiers, selections, functions and message literals; a leading dot cancels it. The errors are
// found by Build, in any order of the calls: not a qualified name, a leading dot, a repeated short name even with the same name, a
// container that equals the short name or begins with it and a dot.
[Trait("Requirement", "F-31")]
[Trait("Requirement", "F-8")]
public sealed class AbbreviationTests
{
    private const string TypeName = "cel.expr.conformance.proto3.TestAllTypes";

    private static CelEnvironmentBuilder Builder() => new CelEnvironmentBuilder(CelStandardLibrary.V1).AddTypes(ProtobufExample.Types3);

    private static CelEnvironment EnvPb { get; } = Builder().AddAbbreviation(TypeName).Build().GetEnvironmentOrThrow();

    private static long Evaluate(CelEnvironment environment, string expression) =>
        environment.Evaluate<long>(expression, new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow();

    private static CelError SingleBuildError(CelEnvironmentBuilder builder)
    {
        CelBuildResult result = builder.Build();
        Assert.False(result.IsSuccess);
        return Assert.Single(result.Errors);
    }

    [Fact]
    public void ShortNameOfAMessage_CreatesTheMessageAndReadsItsField()
    {
        Assert.Equal(1L, Evaluate(EnvPb, "TestAllTypes{single_int64: 1}.single_int64"));
    }

    [Fact]
    public void ShortNameInASelectionChain_ExpandsItsFirstPart()
    {
        Assert.Equal(Evaluate(EnvPb, TypeName + ".NestedEnum.BAR"), Evaluate(EnvPb, "TestAllTypes.NestedEnum.BAR"));
        Assert.Equal(1L, Evaluate(EnvPb, "TestAllTypes.NestedEnum.BAR"));
    }

    [Fact]
    public void NameWithALeadingDot_IsNotAbbreviated()
    {
        CelCompileResult<CelValue> result = EnvPb.Compile<CelValue>(".TestAllTypes{}");

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.UndeclaredReference, error.Code);
        Assert.Equal(TypeName, EnvPb.Compile<CelValue>("." + TypeName + "{}").GetProgramOrThrow().ResultType.Name);
    }

    [Fact]
    public void Abbreviation_WinsOverTheCandidatesOfTheContainer()
    {
        CelEnvironment environment = Builder().UseContainer("google.protobuf").AddAbbreviation(TypeName).Build().GetEnvironmentOrThrow();

        Assert.Equal(TypeName, environment.Compile<CelValue>("TestAllTypes{}").GetProgramOrThrow().ResultType.Name);
    }

    [Fact]
    public void WithoutAnAbbreviation_TheNameIsUndeclared()
    {
        CelEnvironment environment = Builder().Build().GetEnvironmentOrThrow();

        Assert.Equal(CelErrorCode.UndeclaredReference, Assert.Single(environment.Compile<CelValue>("TestAllTypes{}").Errors).Code);
    }

    [Fact]
    public void Abbreviation_AppliesToVariablesAndFunctions()
    {
        CelEnvironment environment = new CelEnvironmentBuilder()
            .AddVariable<long>("acme.limits.max")
            .AddFunction<long, long>("acme.math.twice", value => value * 2)
            .AddAbbreviation("acme.limits.max")
            .AddAbbreviation("acme.math.twice")
            .Build()
            .GetEnvironmentOrThrow();

        long result = environment.Evaluate<long>("twice(max)", new CelBindings().Add("acme.limits.max", 21L), cancellationToken: CancellationToken.None).GetValueOrThrow();

        Assert.Equal(42L, result);
    }

    [Fact]
    public void EnvironmentBuiltFromAnother_KeepsItsAbbreviations()
    {
        CelEnvironment extended = EnvPb.ToBuilder().AddVariable<long>("x").Build().GetEnvironmentOrThrow();

        Assert.Equal(1L, Evaluate(extended, "TestAllTypes{single_int64: 1}.single_int64"));
    }

    [Theory]
    [InlineData("TestAllTypes")]
    [InlineData(".a.b")]
    [InlineData("a.")]
    [InlineData("a..b")]
    [InlineData("a.b.")]
    [InlineData("a b.T")]
    [InlineData("")]
    public static void NameThatIsNotAQualifiedName_IsInvalidAbbreviationAtBuild(string name)
    {
        CelError error = SingleBuildError(new CelEnvironmentBuilder().AddAbbreviation(name));

        Assert.Equal((CelErrorCode.InvalidAbbreviation, CelErrorStage.Build, name), (error.Code, error.Stage, error.Subject));
    }

    [Fact]
    public void LeadingDot_HasTheMessageOfCelGo()
    {
        Assert.Equal(
            "qualified name must not begin with a leading '.': .a.b",
            SingleBuildError(new CelEnvironmentBuilder().AddAbbreviation(".a.b")).Message);
        Assert.Equal(
            "invalid qualified name: TestAllTypes, wanted name of the form 'qualified.name'",
            SingleBuildError(new CelEnvironmentBuilder().AddAbbreviation("TestAllTypes")).Message);
    }

    [Fact]
    public void TwoNamesWithOneShortName_AreInvalidAbbreviation_InAnyOrder()
    {
        CelError forward = SingleBuildError(new CelEnvironmentBuilder().AddAbbreviation("a.b.T").AddAbbreviation("c.d.T"));
        CelError backward = SingleBuildError(new CelEnvironmentBuilder().AddAbbreviation("c.d.T").AddAbbreviation("a.b.T"));

        Assert.Equal(CelErrorCode.InvalidAbbreviation, forward.Code);
        Assert.Equal(forward.Message, backward.Message);
        Assert.Equal(forward.Subject, backward.Subject);
        Assert.Equal("c.d.T", forward.Subject);
    }

    [Fact]
    public void SameNameTwice_IsInvalidAbbreviation()
    {
        CelError error = SingleBuildError(new CelEnvironmentBuilder().AddAbbreviation("a.b.T").AddAbbreviation("a.b.T"));

        Assert.Equal((CelErrorCode.InvalidAbbreviation, "a.b.T"), (error.Code, error.Subject));
    }

    // Abbrevs of cel-go drops the surrounding white space before any check: a name of a folded block of YAML has the indentation of
    // the block.
    [Theory]
    [InlineData(" a.b.T ")]
    [InlineData("a.b.T\n")]
    [InlineData("      a.b.T\r\n")]
    public static void NameWithSurroundingWhiteSpace_IsValid_AsInCelGo(string name)
    {
        CelEnvironment environment = Builder().AddAbbreviation(name).Build().GetEnvironmentOrThrow();

        Assert.NotNull(environment);
    }

    [Fact]
    public void NameWithSurroundingWhiteSpace_IsReportedWithoutIt_AndCollidesWithTheSameNameWithout()
    {
        CelError leadingDot = SingleBuildError(new CelEnvironmentBuilder().AddAbbreviation("   .a.b\n"));
        CelError repeated = SingleBuildError(new CelEnvironmentBuilder().AddAbbreviation(" a.b.T ").AddAbbreviation("a.b.T"));

        Assert.Equal((CelErrorCode.InvalidAbbreviation, ".a.b", "qualified name must not begin with a leading '.': .a.b"), (leadingDot.Code, leadingDot.Subject, leadingDot.Message));
        Assert.Equal((CelErrorCode.InvalidAbbreviation, "a.b.T"), (repeated.Code, repeated.Subject));
        Assert.Equal("abbreviation collides with existing reference: name=a.b.T, abbreviation=T, existing=a.b.T", repeated.Message);
    }

    [Fact]
    public void ContainerThatIsNotTheShortNameOrBeginsWithIt_IsValid()
    {
        Assert.True(new CelEnvironmentBuilder().UseContainer("x.T").AddAbbreviation("a.b.T").Build().IsSuccess);
        Assert.True(new CelEnvironmentBuilder().UseContainer("Tx").AddAbbreviation("a.b.T").Build().IsSuccess);
    }

    [Theory]
    [InlineData("T.x")]
    [InlineData("T")]
    public static void ContainerThatEqualsTheShortNameOrBeginsWithIt_IsInvalidAbbreviation_InEitherOrderOfTheCalls(string container)
    {
        CelError before = SingleBuildError(new CelEnvironmentBuilder().UseContainer(container).AddAbbreviation("a.b.T"));
        CelError after = SingleBuildError(new CelEnvironmentBuilder().AddAbbreviation("a.b.T").UseContainer(container));

        Assert.Equal((CelErrorCode.InvalidAbbreviation, "a.b.T"), (before.Code, before.Subject));
        Assert.Equal(before.Message, after.Message);
        Assert.Contains("container=" + container, before.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AbbreviationErrors_JoinTheOtherBuildErrors()
    {
        CelBuildResult result = new CelEnvironmentBuilder()
            .AddAbbreviation("T")
            .AddVariable<long>("v")
            .AddVariable<string>("v")
            .Build();

        CelErrorCode[] expected = [CelErrorCode.DuplicateVariable, CelErrorCode.InvalidAbbreviation];
        Assert.Equal(expected, result.Errors.Select(error => error.Code).ToArray());
    }

    [Fact]
    public void Abbreviation_ThatIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CelEnvironmentBuilder().AddAbbreviation(null!));
    }
}
