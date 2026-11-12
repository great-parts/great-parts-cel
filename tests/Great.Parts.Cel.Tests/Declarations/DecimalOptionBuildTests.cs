#if NET11_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Declarations;

// The option of the decimal literals in Build, which the examples of the set leave to the checks themselves: what Build puts into the
// syntax of the environment, and the one rule the option has about the sets — a set named decimal, in any version, whichever set it
// is. A set of the application takes the part of the set here, so these tests need no set of the library.
public sealed class DecimalOptionBuildTests
{
    private static CelEnvironmentBuilder Builder() => new(CelStandardLibrary.V1);

    // A set of the application with the name decimal: no function, no type, no dependency.
    private static CelExtension SetNamedDecimal(uint version = 1) =>
        CelExtension.Create("decimal", version, [], new Dictionary<string, CelFunctionImplementation>());

    // The option reaches the syntax of the environment and with it every parse of the environment; without it the literal is refused.
    [Fact]
    public static void Option_PutsTheFlagInTheSyntaxOfTheEnvironment()
    {
        CelEnvironment with = Builder().AddExtension(SetNamedDecimal()).EnableDecimalLiterals().Build().GetEnvironmentOrThrow();
        CelEnvironment without = Builder().AddExtension(SetNamedDecimal()).Build().GetEnvironmentOrThrow();

        Assert.True(with.Syntax.DecimalLiterals);
        Assert.False(without.Syntax.DecimalLiterals);
        Assert.False(Builder().Build().GetEnvironmentOrThrow().Syntax.DecimalLiterals);

        ParseResult parsed = with.Parse("-12.50m");
        Assert.True(parsed.IsSuccess);
        Assert.Equal(CelValueKind.Decimal, Assert.IsType<ConstantNode>(parsed.Expression.Root).Value.Kind);
        Assert.Equal(CelErrorCode.SyntaxNotEnabled, Assert.Single(without.Parse("-12.50m").Errors).Code);
    }

    // The sets that bring syntax or macros of their own keep them: the option is one more flag of the same options, set after the
    // sets have given theirs.
    [Fact]
    public static void Option_KeepsTheSyntaxAndTheMacrosOfTheSets()
    {
        CelEnvironment environment = Builder()
            .AddExtension(CelExtensions.Optional(2))
            .AddExtension(CelExtensions.Math(3))
            .AddExtension(SetNamedDecimal())
            .EnableDecimalLiterals()
            .Build()
            .GetEnvironmentOrThrow();

        Assert.True(environment.Syntax.OptionalSyntax);
        Assert.True(environment.Syntax.DecimalLiterals);
        Assert.True(environment.Parse("a.?b.orValue(1.5m)").IsSuccess);
        Assert.True(environment.Parse("math.greatest(a, b) == 2m").IsSuccess);
        Assert.True(environment.Parse("[1.5m].exists(x, x == 1.5m)").IsSuccess);
    }

    // The rule has the name of the set only: any version of a set named decimal meets it, whoever wrote the set.
    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(7u)]
    public static void Option_IsMetByASetNamedDecimalOfAnyVersion(uint version)
    {
        Assert.True(Builder().AddExtension(SetNamedDecimal(version)).EnableDecimalLiterals().Build().IsSuccess);
    }

    [Fact]
    public static void Option_IsNotMetBySetsOfOtherNames()
    {
        CelBuildResult result = Builder()
            .AddExtension(CelExtension.Create("decimals", 1, [], new Dictionary<string, CelFunctionImplementation>()))
            .AddExtension(CelExtensions.Strings(5))
            .EnableDecimalLiterals()
            .Build();

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(
            (CelErrorCode.MissingExtensionDependency, CelErrorStage.Build, "the decimal literal option requires extension 'decimal', which is not added to the environment"),
            (error.Code, error.Stage, error.Message));
    }

    // The option has no element of a description, so its error has no place; the dependency of a set on another is a second error of
    // the same code, and the two are told apart by their text.
    [Fact]
    public static void OptionWithoutTheSet_HasNoPlaceAndIsAnErrorOfItsOwnBesideTheErrorsOfTheSets()
    {
        CelBuildResult result = Builder()
            .AddExtension(CelExtension.Create("needs", 1, [], new Dictionary<string, CelFunctionImplementation>(), "absent"))
            .EnableDecimalLiterals()
            .Build();

        Assert.Equal(2, result.Errors.Count(error => error.Code == CelErrorCode.MissingExtensionDependency));
        Assert.Equal(2, result.Errors.Length);
        CelError option = Assert.Single(result.Errors, error => error.Message.StartsWith("the decimal literal option", StringComparison.Ordinal));
        Assert.Null(option.SourceLocation);
        Assert.Equal(default(CelSourceLocation), option.Location);
    }
}
#endif
