using System;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Declarations;

// Functions of the application in one step: AddMethod is a function on a receiver whose first delegate parameter is the
// receiver; CelFunctionOptions give the cost, a stable overload id and a description, which the introspection of the
// environment shows. A repeated overload id is DuplicateOverloadId.
[Trait("Requirement", "F-3")]
public sealed class FunctionOptionsTests
{
    [Fact]
    public void AddMethod_IsAFunctionOnAReceiver()
    {
        CelEnvironment environment = new CelEnvironmentBuilder()
            .AddVariable<string>("kpp")
            .AddMethod<string, bool>("isKpp", s => s.Length == 9 && s.All(char.IsAsciiDigit))
            .AddMethod<string, long, string>("left", (s, n) => s[..(int)n])
            .Build()
            .GetEnvironmentOrThrow();

        Assert.True(Evaluate<bool>(environment, "kpp.isKpp()", new CelBindings().Add("kpp", "770101001")));
        Assert.Equal("77", Evaluate<string>(environment, "kpp.left(2)", new CelBindings().Add("kpp", "770101001")));
        Assert.Equal(CelErrorCode.NoMatchingOverload, Assert.Single(environment.Compile<bool>("isKpp(kpp)").Errors).Code);

        CelOverloadDecl overload = Assert.Single(environment.Functions.Single(function => function.Name == "isKpp").Overloads);
        Assert.True(overload.IsReceiverStyle);
    }

    [Fact]
    public void Options_GiveTheIdTheDescriptionAndTheCost()
    {
        CelEnvironment environment = new CelEnvironmentBuilder()
            .AddMethod<string, bool>("isKpp", s => s.Length == 9, new CelFunctionOptions
            {
                OverloadId = "string_is_kpp",
                Description = "Tells whether the string is a Russian KPP of nine digits.",
                Cost = _ => 50,
            })
            .AddFunction<long, long>("twice", n => n * 2, new CelFunctionOptions { OverloadId = "twice_int" })
            .Build()
            .GetEnvironmentOrThrow();

        CelOverloadDecl isKpp = Assert.Single(environment.Functions.Single(function => function.Name == "isKpp").Overloads);
        Assert.Equal("string_is_kpp", isKpp.OverloadId);
        Assert.Equal("Tells whether the string is a Russian KPP of nine digits.", isKpp.Description);
        Assert.Equal("twice_int", Assert.Single(environment.Functions.Single(function => function.Name == "twice").Overloads).OverloadId);

        CelEvalResult<bool> result = environment.Compile<bool>("'770101001'.isKpp()").GetProgramOrThrow()
            .Evaluate(new CelBindings(), new CelEvalOptions { CostLimit = 1_000 }, CancellationToken.None);
        // A call of a function of the application costs 1 + c, c from the cost function.
        Assert.Equal(51UL, result.Cost);
    }

    [Fact]
    public void WithoutOptions_TheIdIsDerivedAndThereIsNoDescription()
    {
        CelEnvironment environment = new CelEnvironmentBuilder().AddFunction<long, long>("twice", n => n * 2).Build().GetEnvironmentOrThrow();

        CelOverloadDecl overload = Assert.Single(environment.Functions.Single(function => function.Name == "twice").Overloads);
        Assert.False(string.IsNullOrEmpty(overload.OverloadId));
        Assert.Null(overload.Description);
    }

    [Fact]
    public void RepeatedOverloadId_IsDuplicateOverloadId()
    {
        var options = new CelFunctionOptions { OverloadId = "my_id" };

        CelError error = Assert.Single(new CelEnvironmentBuilder()
            .AddFunction<long, long>("a", n => n, options)
            .AddFunction<string, string>("b", s => s, options)
            .Build().Errors);

        Assert.Equal(CelErrorCode.DuplicateOverloadId, error.Code);
        Assert.Equal("my_id", error.Subject);
    }

    [Fact]
    public void WithDescription_OnAHandWrittenOverload()
    {
        CelOverloadDecl overload = CelOverloadDecl.Global("half_double", [CelType.Double], CelType.Double)
            .WithDescription("Halves a number.");

        Assert.Equal("Halves a number.", overload.Description);
    }

    // Each function of the standard library has a description, one English sentence.
    [Theory]
    [InlineData("size")]
    [InlineData("matches")]
    [InlineData("startsWith")]
    [InlineData("timestamp")]
    public static void StandardLibrary_FunctionsHaveDescriptions(string name)
    {
        CelEnvironment environment = new CelEnvironmentBuilder().Build().GetEnvironmentOrThrow();

        Assert.All(environment.Functions.Single(function => function.Name == name).Overloads, overload =>
        {
            Assert.False(string.IsNullOrWhiteSpace(overload.Description));
            Assert.EndsWith(".", overload.Description, StringComparison.Ordinal);
        });
    }

    private static T Evaluate<T>(CelEnvironment environment, string text, CelBindings bindings) =>
        environment.Compile<T>(text).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();
}
