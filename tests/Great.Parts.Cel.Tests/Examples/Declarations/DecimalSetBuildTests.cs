#if NET11_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Declarations;

// The set decimal in the builder (the page of the extension set decimal, "The set", "The literal and the flag"; the page of the
// declarations, "decimal without the set and the literal option"). The set has the name decimal and the one version 1 in this
// release; CelExtensions.Decimal(version) does not check the version, and a version that the set does not have is
// UnknownExtensionVersion (4004) at Build, "extension 'decimal' has no version <n>; available versions: 1" — version 0, which no
// release has, and version 2, which the set will have (round and quantize), among them. The set cannot be named in the description
// in JSON, so these two are reached from code only (EnvironmentJsonDecimalTests). The set declares the type decimal, so a variable
// of that type, at any depth, needs the set whichever call comes first, and without it is TypeNotDeclared (4008) with the hint that
// names the set. EnableDecimalLiterals() needs the set: without it Build gives MissingExtensionDependency (4005), "the decimal
// literal option requires extension 'decimal', which is not added to the environment", once however many times the option is
// called, whichever call comes first; with a set whose version does not exist the errors are two, 4004 and 4005, each once. A
// typed collection of decimals — IReadOnlyList<Decimal128>, IReadOnlyDictionary<string, decimal> — is outside the closed list of
// the elements, as a typed collection of time is: TypeNotDeclared for AddVariable<T> and ArgumentException for CelBindings.Add<T>;
// the elements of such a collection reach an expression as CelList and CelMap of the values (DecimalInputRowTests). A build error
// has the stage Build and no place in an expression.
[Trait("Requirement", "F-3")]
[Trait("Requirement", "F-5")]
[Trait("Requirement", "S-2")]
public sealed class DecimalSetBuildTests
{
    private const string Undeclared = "which no type source or extension of the environment declares; add the extension set 'decimal'";

    [Fact]
    public void Set_HasItsNameAndItsVersion()
    {
        CelExtension extension = CelExtensions.Decimal(1);

        Assert.Equal(("decimal", 1u), (extension.Name, extension.Version));
        Assert.True(Builder().AddExtension(extension).Build().IsSuccess);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(2u)]
    [InlineData(3u)]
    [InlineData(uint.MaxValue)]
    public static void VersionThatTheSetDoesNotHave_IsUnknownExtensionVersion(uint asked)
    {
        CelExtension extension = CelExtensions.Decimal(asked);
        Assert.Equal(("decimal", asked), (extension.Name, extension.Version));

        CelError error = SingleError(Builder().AddExtension(extension));

        AssertError(
            CelErrorCode.UnknownExtensionVersion,
            "extension 'decimal' has no version " + asked.ToString(CultureInfo.InvariantCulture) + "; available versions: 1",
            error);
    }

    [Fact]
    public void SameSetAddedAgain_IsOneSet()
    {
        Assert.True(Builder().AddExtension(CelExtensions.Decimal(1)).AddExtension(CelExtensions.Decimal(1)).Build().IsSuccess);
    }

    // The set brings no dependency: it builds alone and with every other set of the catalog.
    [Fact]
    public void Set_BuildsAloneAndWithTheOtherSets()
    {
        Assert.True(Builder().AddExtension(CelExtensions.Decimal(1)).Build().IsSuccess);
        Assert.True(Builder()
            .AddExtension(CelExtensions.Strings(5))
            .AddExtension(CelExtensions.Math(3))
            .AddExtension(CelExtensions.Lists(4))
            .AddExtension(CelExtensions.Sets(0))
            .AddExtension(CelExtensions.Encoders(1))
            .AddExtension(CelExtensions.Bindings(1))
            .AddExtension(CelExtensions.TwoVarComprehensions(0))
            .AddExtension(CelExtensions.Network(1))
            .AddExtension(CelExtensions.Optional(2))
            .AddExtension(CelExtensions.Protos(0))
            .AddExtension(CelExtensions.Decimal(1))
            .Build()
            .IsSuccess);
    }

    // ToBuilder returns a builder with the same set and the same option: the environment it builds reads the literal and calls
    // the functions of the set.
    [Fact]
    public void ToBuilder_KeepsTheSetAndTheLiteralOption()
    {
        CelEnvironment first = Builder().AddExtension(CelExtensions.Decimal(1)).EnableDecimalLiterals().Build().GetEnvironmentOrThrow();
        CelEnvironment second = first.ToBuilder().Build().GetEnvironmentOrThrow();

        Assert.True(first.Compile<CelValue>("12.34m + decimal('1')").IsSuccess);
        Assert.True(second.Compile<CelValue>("12.34m + decimal('1')").IsSuccess);
    }

    // A variable of the type decimal, of the .NET types that map to it and of the CEL type, builds with the set in either order of
    // the calls; without it the error is TypeNotDeclared at the variable, which names the set the application lacks.
    [Fact]
    public void VariableOfTheTypeDecimal_NeedsTheSet_InEitherOrderOfTheCalls()
    {
        Assert.True(Builder().AddExtension(CelExtensions.Decimal(1)).AddVariable<Decimal128>("a").AddVariable<decimal>("b").AddVariable("c", CelType.Decimal).Build().IsSuccess);
        Assert.True(Builder().AddVariable<Decimal128>("a").AddVariable<decimal>("b").AddVariable("c", CelType.Decimal).AddExtension(CelExtensions.Decimal(1)).Build().IsSuccess);
        Assert.True(Builder().AddExtension(CelExtensions.Decimal(1)).AddVariable<Decimal128?>("n").AddVariable<decimal?>("m").Build().IsSuccess);
    }

    [Theory]
    [InlineData("a", "decimal")]
    [InlineData("b", "decimal")]
    [InlineData("c", "decimal")]
    [InlineData("d", "list(decimal)")]
    [InlineData("e", "map(string, decimal)")]
    [InlineData("f", "optional_type(decimal)")]
    public static void VariableOfTheTypeDecimalWithoutTheSet_IsTypeNotDeclaredWithTheHint(string name, string type)
    {
        CelEnvironmentBuilder builder = Builder()
            .AddVariable<Decimal128>("a")
            .AddVariable<decimal>("b")
            .AddVariable("c", CelType.Decimal)
            .AddVariable("d", CelType.ListOf(CelType.Decimal))
            .AddVariable("e", CelType.MapOf(CelType.String, CelType.Decimal));
        CelBuildResult result = builder.AddExtension(CelExtensions.Optional(2)).AddVariable("f", CelType.OptionalOf(CelType.Decimal)).Build();

        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors, candidate => candidate.Message.StartsWith("variable '" + name + "'", StringComparison.Ordinal));
        Assert.Equal((CelErrorCode.TypeNotDeclared, CelErrorStage.Build), (error.Code, error.Stage));
        Assert.Equal("variable '" + name + "' has type '" + type + "', " + Undeclared, error.Message);
        Assert.Equal(default(CelSourceLocation), error.Location);
    }

    // The option needs the set, in either order of the calls and however often it is called.
    [Fact]
    public void LiteralOption_NeedsTheSet_InEitherOrderOfTheCalls()
    {
        Assert.True(Builder().AddExtension(CelExtensions.Decimal(1)).EnableDecimalLiterals().Build().IsSuccess);
        Assert.True(Builder().EnableDecimalLiterals().AddExtension(CelExtensions.Decimal(1)).Build().IsSuccess);
        Assert.True(Builder().EnableDecimalLiterals().EnableDecimalLiterals().AddExtension(CelExtensions.Decimal(1)).Build().IsSuccess);
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "F-8")]
    public void LiteralOptionWithoutTheSet_IsMissingExtensionDependency_Once()
    {
        const string Message = "the decimal literal option requires extension 'decimal', which is not added to the environment";

        AssertError(CelErrorCode.MissingExtensionDependency, Message, SingleError(Builder().EnableDecimalLiterals()));
        AssertError(CelErrorCode.MissingExtensionDependency, Message, SingleError(Builder().EnableDecimalLiterals().EnableDecimalLiterals()));
        AssertError(CelErrorCode.MissingExtensionDependency, Message, SingleError(Builder().EnableDecimalLiterals().AddExtension(CelExtensions.Optional(2))));
    }

    // The set whose version does not exist is no set, so the option has none: two errors, one of each code, once each.
    [Theory]
    [InlineData(0u)]
    [InlineData(2u)]
    public static void LiteralOptionWithAVersionThatDoesNotExist_IsTwoErrors(uint asked)
    {
        foreach (CelEnvironmentBuilder builder in new[]
        {
            Builder().AddExtension(CelExtensions.Decimal(asked)).EnableDecimalLiterals(),
            Builder().EnableDecimalLiterals().AddExtension(CelExtensions.Decimal(asked)),
        })
        {
            CelBuildResult result = builder.Build();

            Assert.False(result.IsSuccess);
            Assert.Equal(
                new[] { CelErrorCode.UnknownExtensionVersion, CelErrorCode.MissingExtensionDependency },
                result.Errors.Select(error => error.Code).Order());
            Assert.All(result.Errors, error => Assert.Equal(CelErrorStage.Build, error.Stage));
        }
    }

    // A typed collection of decimals is outside the closed list of the elements, with the set as without it.
    [Fact]
    public void TypedCollectionOfDecimals_IsTypeNotDeclaredAndArgumentException()
    {
        AssertOutsideTheRows<IReadOnlyList<Decimal128>>(
            [DecimalExample.Make(15, -1)],
            "System.Collections.Generic.IReadOnlyList`1[System.Numerics.Decimal128]");
        AssertOutsideTheRows<IReadOnlyList<decimal>>(
            [1.5m],
            "System.Collections.Generic.IReadOnlyList`1[System.Decimal]");
        AssertOutsideTheRows<IReadOnlyDictionary<string, decimal>>(
            new Dictionary<string, decimal> { ["a"] = 1.5m },
            "System.Collections.Generic.IReadOnlyDictionary`2[System.String,System.Decimal]");
    }

    private static void AssertOutsideTheRows<T>(T value, string typeName)
    {
        foreach (bool withTheSet in new[] { true, false })
        {
            CelEnvironmentBuilder builder = Builder();
            if (withTheSet)
            {
                builder.AddExtension(CelExtensions.Decimal(1));
            }

            CelError error = SingleError(builder.AddVariable<T>("v"));

            Assert.Equal(
                (CelErrorCode.TypeNotDeclared, CelErrorStage.Build, "variable 'v' has type '" + typeName + "', which no type source or extension of the environment declares"),
                (error.Code, error.Stage, error.Message));
        }

        Assert.Throws<ArgumentException>(() => new CelBindings().Add("v", value));
    }

    private static CelEnvironmentBuilder Builder() => new(CelStandardLibrary.V1);

    private static CelError SingleError(CelEnvironmentBuilder builder)
    {
        CelBuildResult result = builder.Build();

        Assert.False(result.IsSuccess);
        return Assert.Single(result.Errors);
    }

    private static void AssertError(CelErrorCode code, string message, CelError error)
    {
        Assert.Equal((code, CelErrorStage.Build, message), (error.Code, error.Stage, error.Message));
        Assert.Equal(default(CelSourceLocation), error.Location);
        Assert.Null(error.SourceLocation);
    }
}
#endif
