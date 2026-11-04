using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Checking;

// Name resolution in a container, by the language definition, "Name Resolution": in the container a.b the
// name x is tried as a.b.x, a.x, then x; a leading "." takes the name as written; the variables of a
// comprehension shadow every declaration; the longest declared prefix of a selection chain wins. Each
// variable holds its own name, so the value tells which declaration an expression read. Every value is
// the one cel-go v0.32.0 gives in the same environment (a probe with the environment flags of the oracle).
public sealed class NameResolutionTests
{
    private static readonly CelFunctionImplementation ReturnsF = _ => CelValue.FromString("f");

    private static readonly CelFunctionImplementation ReturnsAbf = _ => CelValue.FromString("a.b.f");

    private readonly CelEnvironment inContainer = WithNames(new CelEnvironmentBuilder(CelStandardLibrary.V1).UseContainer("a.b"))
        .AddVariable<string>("a.b.x")
        .AddVariable<string>("a.x")
        .AddVariable<string>("x")
        .AddFunction(Function("f", "f_int"))
        .AddFunctionImplementation("f_int", ReturnsF)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelEnvironment withoutContainer = WithNames(new CelEnvironmentBuilder(CelStandardLibrary.V1))
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("a.b.x", "a.b.x")
        .Add("a.x", "a.x")
        .Add("x", "x")
        .Add("a.b.c", "a.b.c")
        .Add("a", CelValue.FromMap(CelMap.Create([new KeyValuePair<CelValue, CelValue>(
            CelValue.FromString("b"),
            CelValue.FromMap(CelMap.Create([new KeyValuePair<CelValue, CelValue>(CelValue.FromString("c"), CelValue.FromString("map"))])))])));

    [Theory]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "F-3")]
    [InlineData("x", "a.b.x")]
    [InlineData(".x", "x")]
    [InlineData("a.x", "a.x")]
    [InlineData(".a.b.x", "a.b.x")]
    [InlineData("a.b.c", "a.b.c")]
    [InlineData(".a.b.c", "a.b.c")]
    [InlineData("a['b'].c", "map")]
    [InlineData("f(1)", "a.b.f")]
    [InlineData(".f(1)", "f")]
    [InlineData("a.b.f(1)", "a.b.f")]
    [InlineData("['q'].map(x, x)[0]", "q")]
    [InlineData("['q'].map(x, .x + x)[0]", "xq")]
    [InlineData("[{'b': {'c': 'local'}}].map(a, a.b.c)[0]", "local")]
    public void NameInTheContainer_ResolvesToTheFirstDeclaredCandidate(string expression, string expected)
    {
        Assert.Equal(expected, Evaluate(inContainer, expression));
    }

    // Without a container a name is tried as written; a selection chain is a qualified name when its whole
    // text is declared, and a target that is a declared function name makes a global call.
    [Theory]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "F-3")]
    [InlineData("a.b.c", "a.b.c")]
    [InlineData("a.b['c']", "map")]
    [InlineData("a.b.f(1)", "a.b.f")]
    public void NameWithoutAContainer_IsTriedAsWritten(string expression, string expected)
    {
        Assert.Equal(expected, Evaluate(withoutContainer, expression));
    }

    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("a.b", "z", "undeclared reference to 'z' (in container 'a.b')")]
    [InlineData("", "z", "undeclared reference to 'z' (in container '')")]
    public void UndeclaredName_NamesTheContainer(string container, string expression, string message)
    {
        CelEnvironment environment = WithNames(new CelEnvironmentBuilder(CelStandardLibrary.V1).UseContainer(container)).Build().GetEnvironmentOrThrow();
        Assert.Equal("a.b.c", Evaluate(environment, "a.b.c"));

        CelError error = Assert.Single(environment.Compile<CelValue>(expression).Errors);

        Assert.Equal((CelErrorCode.UndeclaredReference, CelErrorStage.Check, 0), (error.Code, error.Stage, error.Location.Offset));
        Assert.Equal(message, error.Message);
    }

    // The declarations common to both environments: a variable a.b.c, a map variable a and a function a.b.f.
    private static CelEnvironmentBuilder WithNames(CelEnvironmentBuilder builder) => builder
        .AddVariable<string>("a.b.c")
        .AddVariable("a", CelType.MapOf(CelType.String, CelType.Dyn))
        .AddFunction(Function("a.b.f", "abf_int"))
        .AddFunctionImplementation("abf_int", ReturnsAbf);

    private static CelFunctionDecl Function(string name, string overloadId) =>
        new(name, CelOverloadDecl.Global(overloadId, [CelType.Int], CelType.String));

    private string Evaluate(CelEnvironment environment, string expression) =>
        environment.Compile<string>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();
}
