using System;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Declarations;

// The descriptions of the functions of the library and of the application, and the forms of AddMethod that the examples do not show:
// every function of the standard library version 1 and of every set of the catalog has a description that is one sentence; the
// overloads of a function are in the order of their identifiers; AddMethod with two and three arguments and with options; options
// without any property.
public sealed class FunctionDescriptionTests
{
    [Fact]
    [Trait("Requirement", "F-13")]
    public void StandardLibraryV1_EveryFunctionHasADescription()
    {
        CelEnvironment environment = new CelEnvironmentBuilder().Build().GetEnvironmentOrThrow();

        AssertDescribed(environment);
    }

    [Theory]
    [Trait("Requirement", "F-13")]
    [InlineData("strings", 5u)]
    [InlineData("math", 3u)]
    [InlineData("lists", 4u)]
    [InlineData("sets", 0u)]
    [InlineData("encoders", 1u)]
    [InlineData("bindings", 1u)]
    [InlineData("two-var-comprehensions", 0u)]
    [InlineData("network", 1u)]
    [InlineData("protos", 0u)]
    [InlineData("optional", 2u)]
#if NET11_0_OR_GREATER
    [InlineData("decimal", 1u)]
#endif
    public static void EverySet_EveryFunctionHasADescription(string set, uint version)
    {
        CelEnvironment environment = new CelEnvironmentBuilder().AddExtension(Create(set, version)).Build().GetEnvironmentOrThrow();

        AssertDescribed(environment);
        Assert.True(environment.Functions.Length > 0);
    }

    [Fact]
    [Trait("Requirement", "F-13")]
    public void Functions_OverloadsAreInTheOrderOfTheirIdentifiers()
    {
        CelEnvironment environment = new CelEnvironmentBuilder()
            .AddFunction<string, string>("pick", s => s)
            .AddFunction<long, long>("pick", n => n)
            .AddFunction<double, double>("pick", d => d)
            .Build()
            .GetEnvironmentOrThrow();

        string[] ids = [.. environment.Functions.Single(function => function.Name == "pick").Overloads.Select(overload => overload.OverloadId)];

        Assert.Equal(["pick_double", "pick_int", "pick_string"], ids);
    }

    [Fact]
    [Trait("Requirement", "F-3")]
    public void AddMethod_WithTwoAndThreeArguments_AndWithOptions()
    {
        CelEnvironment environment = new CelEnvironmentBuilder()
            .AddMethod<string, string, long, string>("repeat", (s, separator, count) => string.Join(separator, Enumerable.Repeat(s, (int)count)))
            .AddMethod<long, long, long, long, long>(
                "sum4",
                (a, b, c, d) => a + b + c + d,
                new CelFunctionOptions { OverloadId = "int_sum4", Description = "Adds four numbers." })
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Equal("ab-ab-ab", Evaluate<string>(environment, "'ab'.repeat('-', 3)"));
        Assert.Equal(10L, Evaluate<long>(environment, "(1).sum4(2, 3, 4)"));
        CelOverloadDecl repeat = Assert.Single(environment.Functions.Single(function => function.Name == "repeat").Overloads);
        Assert.Equal("string_repeat_string_int", repeat.OverloadId);
        Assert.Null(repeat.Description);
        CelOverloadDecl sum = Assert.Single(environment.Functions.Single(function => function.Name == "sum4").Overloads);
        Assert.Equal(("int_sum4", "Adds four numbers.", true), (sum.OverloadId, sum.Description, sum.IsReceiverStyle));
    }

    [Fact]
    [Trait("Requirement", "F-3")]
    public void OptionsWithoutProperties_DeclareTheSameOverloadAsTheFormWithoutOptions()
    {
        CelEnvironment plain = new CelEnvironmentBuilder().AddFunction<long, long>("twice", n => n * 2).Build().GetEnvironmentOrThrow();
        CelEnvironment optioned = new CelEnvironmentBuilder().AddFunction<long, long>("twice", n => n * 2, new CelFunctionOptions()).Build().GetEnvironmentOrThrow();

        CelOverloadDecl left = plain.Functions.Single(function => function.Name == "twice").Overloads.Single();
        CelOverloadDecl right = optioned.Functions.Single(function => function.Name == "twice").Overloads.Single();

        Assert.Equal((left.OverloadId, left.Description, left.Cost), (right.OverloadId, right.Description, right.Cost));
        Assert.Equal(6L, Evaluate<long>(optioned, "twice(3)"));
    }

    [Fact]
    [Trait("Requirement", "F-3")]
    public void Options_AreChecked()
    {
        var builder = new CelEnvironmentBuilder();

        Assert.Throws<ArgumentNullException>("options", () => builder.AddFunction<long, long>("f", n => n, null!));
        Assert.Throws<ArgumentException>("options", () => builder.AddMethod<string, bool>("f", s => s.Length == 0, new CelFunctionOptions { OverloadId = string.Empty }));
        Assert.Throws<ArgumentException>("name", () => builder.AddFunction<long>(string.Empty, () => 1, new CelFunctionOptions()));
        Assert.Throws<ArgumentNullException>("implementation", () => builder.AddMethod<string, bool>("f", null!));
    }

    [Fact]
    [Trait("Requirement", "F-13")]
    public void WithDescription_KeepsTheCostAndTheSignature()
    {
        CelOverloadDecl overload = CelOverloadDecl.Global("f_int", [CelType.Int], CelType.Int).WithCost(_ => 7).WithDescription("Does f.");

        Assert.Equal(("f_int", "Does f."), (overload.OverloadId, overload.Description));
        Assert.NotNull(overload.Cost);
        Assert.Null(overload.WithDescription(string.Empty).Description);
        Assert.Throws<ArgumentNullException>("description", () => overload.WithDescription(null!));
    }

    private static void AssertDescribed(CelEnvironment environment)
    {
        foreach (CelFunctionDecl function in environment.Functions)
        {
            foreach (CelOverloadDecl overload in function.Overloads)
            {
                Assert.False(string.IsNullOrWhiteSpace(overload.Description), function.Name + " / " + overload.OverloadId);
                Assert.EndsWith(".", overload.Description, StringComparison.Ordinal);
            }
        }
    }

    private static CelExtension Create(string set, uint version) => set switch
    {
        "strings" => CelExtensions.Strings(version),
        "math" => CelExtensions.Math(version),
        "lists" => CelExtensions.Lists(version),
        "sets" => CelExtensions.Sets(version),
        "encoders" => CelExtensions.Encoders(version),
        "bindings" => CelExtensions.Bindings(version),
        "two-var-comprehensions" => CelExtensions.TwoVarComprehensions(version),
        "network" => CelExtensions.Network(version),
        "protos" => CelExtensions.Protos(version),
        "optional" => CelExtensions.Optional(version),
#if NET11_0_OR_GREATER
        "decimal" => CelExtensions.Decimal(version),
#endif
        _ => throw new ArgumentOutOfRangeException(nameof(set), set, "No such set."),
    };

    private static T Evaluate<T>(CelEnvironment environment, string text) =>
        environment.Compile<T>(text).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow();
}
