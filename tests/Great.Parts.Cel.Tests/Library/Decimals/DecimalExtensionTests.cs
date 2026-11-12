#if NET11_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Library.Decimals;
using Great.Parts.Cel.Library.Extensions;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Library.Decimals;

// The definition of the set decimal against its page: the 18 overloads in their order with their identifiers, signatures and fixed
// units, the one instance of the one version, the sets that the set is not (it is no row of the catalog and has no name in a
// description), and the conflicts with a set of the application of the same name, the type name that a provider may not take, and a
// function of the application that takes and gives a decimal.
public sealed class DecimalExtensionTests
{
    private static readonly (string Function, string Id, string Parameters, string Result, ulong Cost)[] Table =
    [
        ("_+_", "add_decimal", "decimal,decimal", "decimal", 1),
        ("_-_", "subtract_decimal", "decimal,decimal", "decimal", 1),
        ("_*_", "multiply_decimal", "decimal,decimal", "decimal", 1),
        ("_/_", "divide_decimal", "decimal,decimal", "decimal", 1),
        ("-_", "negate_decimal", "decimal", "decimal", 1),
        ("_<_", "less_decimal", "decimal,decimal", "bool", 1),
        ("_<=_", "less_equals_decimal", "decimal,decimal", "bool", 1),
        ("_>_", "greater_decimal", "decimal,decimal", "bool", 1),
        ("_>=_", "greater_equals_decimal", "decimal,decimal", "bool", 1),
        ("decimal", "int64_to_decimal", "int", "decimal", 1),
        ("decimal", "uint64_to_decimal", "uint", "decimal", 1),
        ("decimal", "double_to_decimal", "double", "decimal", 4),
        ("decimal", "string_to_decimal", "string", "decimal", 1),
        ("decimal", "decimal_to_decimal", "decimal", "decimal", 1),
        ("int", "decimal_to_int64", "decimal", "int", 1),
        ("uint", "decimal_to_uint64", "decimal", "uint", 1),
        ("double", "decimal_to_double", "decimal", "double", 1),
        ("string", "decimal_to_string", "decimal", "string", 1),
    ];

    [Fact]
    public static void Get_OfVersionOne_IsOneInstanceThatTheMemberOfTheCatalogClassGives()
    {
        CelExtension set = DecimalExtension.Get(1);

        Assert.Same(set, DecimalExtension.Get(1));
        Assert.Same(set, CelExtensions.Decimal(1));
        Assert.Equal(("decimal", 1u), (set.Name, set.Version));
        Assert.Equal(1u, Assert.Single(set.AvailableVersions));
        Assert.False(set.IsApplication);
        Assert.NotNull(set.Definition);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(2u)]
    [InlineData(uint.MaxValue)]
    public static void Get_OfAnotherVersion_IsASetWithoutADefinitionThatListsTheVersionOfTheSet(uint version)
    {
        CelExtension set = DecimalExtension.Get(version);

        Assert.Equal(("decimal", version), (set.Name, set.Version));
        Assert.Null(set.Definition);
        Assert.Equal(1u, Assert.Single(set.AvailableVersions));
        Assert.False(set.IsSameSetAs(DecimalExtension.Get(1)));
        Assert.True(set.IsSameSetAs(DecimalExtension.Get(version)));
    }

    [Fact]
    public static void Definition_IsTheTypeAndThe18OverloadsAndNothingElse()
    {
        ExtensionDefinition definition = DecimalExtension.Get(1).Definition!;

        Assert.IsType<DecimalDefinition>(definition);
        Assert.Equal(("decimal", 1u, "extension decimal v1"), (definition.Name, definition.Version, definition.Source));
        AbstractTypeDeclaration type = Assert.Single(definition.AbstractTypes);
        Assert.Equal("decimal", type.Name);
        Assert.Same(CelType.Decimal, type.Type);
        Assert.Empty(definition.Variables);
        Assert.Empty(definition.Dependencies);
        Assert.Empty(definition.MacroForms);
        Assert.Equal(18, definition.Overloads.Length);
        Assert.All(definition.Overloads, overload => Assert.Equal("extension decimal v1", overload.Source));
        Assert.All(definition.Overloads, overload => Assert.False(overload.IsApplication));
    }

    [Fact]
    public static void Overloads_AreThoseOfThePage_InItsOrder()
    {
        ImmutableArray<OverloadBinding> overloads = DecimalExtension.Get(1).Definition!.Overloads;

        (string, string, string, string, ulong)[] actual = [.. overloads.Select(static overload => (
            overload.Function,
            overload.Declaration.OverloadId,
            string.Join(',', overload.Declaration.ParameterTypes.Select(static type => type.Name)),
            overload.Declaration.ResultType.Name,
            overload.FixedCost))];

        Assert.Equal(Table, actual);
        Assert.All(overloads, overload => Assert.False(overload.Declaration.IsReceiverStyle));
    }

    // The part of a cost that grows with the data is the cost function of the one overload that reads a string; decimal_to_string
    // charges the length of its text inside the call, so it has no cost function.
    [Fact]
    public static void CostFunction_IsOnlyThatOfTheOverloadThatReadsAString()
    {
        IEnumerable<string> withCostFunction = DecimalExtension.Get(1).Definition!.Overloads
            .Where(static overload => overload.ArgumentCost is not null)
            .Select(static overload => overload.Declaration.OverloadId);

        Assert.Equal("string_to_decimal", Assert.Single(withCostFunction));
    }

    [Fact]
    public static void Name_IsNotAmongTheNamesOfTheCatalog()
    {
        Assert.DoesNotContain("decimal", (IEnumerable<string>)ExtensionCatalog.Default.Names);
        Assert.DoesNotContain("decimal", (IEnumerable<string>)CelExtensions.Names);
    }

    [Fact]
    public static void AnotherVersionAddedWithVersionOne_IsOneErrorOfTheVersionThatDoesNotExist()
    {
        CelBuildResult result = Builder().AddExtension(CelExtensions.Decimal(1)).AddExtension(CelExtensions.Decimal(2)).Build();

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(
            (CelErrorCode.UnknownExtensionVersion, "extension 'decimal' has no version 2; available versions: 1"),
            (error.Code, error.Message));
    }

    [Fact]
    public static void SetOfTheApplicationInAnotherVersion_IsExtensionVersionConflict()
    {
        const string Message = "extension 'decimal' is added in versions 1 and 2";

        Assert.Equal((CelErrorCode.ExtensionVersionConflict, Message), SingleError(Builder().AddExtension(CelExtensions.Decimal(1)).AddExtension(Own(2))));
        Assert.Equal((CelErrorCode.ExtensionVersionConflict, Message), SingleError(Builder().AddExtension(Own(2)).AddExtension(CelExtensions.Decimal(1))));
    }

    [Fact]
    public static void SetOfTheApplicationOfTheSameVersion_IsDuplicateExtension()
    {
        const string Message = "extension 'decimal' version 1 is added as two different sets: application and extension decimal v1";

        Assert.Equal((CelErrorCode.DuplicateExtension, Message), SingleError(Builder().AddExtension(CelExtensions.Decimal(1)).AddExtension(Own(1))));
        Assert.Equal((CelErrorCode.DuplicateExtension, Message), SingleError(Builder().AddExtension(Own(1)).AddExtension(CelExtensions.Decimal(1))));
    }

    // The set declares the type identifier decimal, so a type of a provider of the same name is a second declaration of it.
    [Fact]
    public static void TypeOfAProviderNamedDecimal_IsDuplicateType()
    {
        CelTypeProvider provider = CelTypeProvider.Create("types", CelObjectTypeBuilder.ForDictionary("decimal").Build());

        CelBuildResult result = Builder().AddExtension(CelExtensions.Decimal(1)).AddTypes(provider).Build();

        Assert.Contains(result.Errors, static error => error.Code == CelErrorCode.DuplicateType);
    }

    // A function of the application declares a parameter and a result of the type decimal, which the set gives the environment.
    [Fact]
    public static void FunctionOfTheApplication_TakesAndGivesADecimal()
    {
        CelExtension twice = CelExtension.Create(
            "twice",
            1,
            [new CelFunctionDecl("twice", CelOverloadDecl.Global("twice_decimal", [CelType.Decimal], CelType.Decimal))],
            new Dictionary<string, CelFunctionImplementation> { ["twice_decimal"] = Twice });
        CelEnvironment environment = Builder().AddExtension(CelExtensions.Decimal(1)).AddExtension(twice).Build().GetEnvironmentOrThrow();

        CelValue result = environment.Compile<CelValue>("string(twice(decimal('1.50')))").GetProgramOrThrow()
            .Evaluate(new CelBindings(), new CelEvalOptions(), System.Threading.CancellationToken.None).GetValueOrThrow();

        Assert.True(result.TryGetString(out string? text));
        Assert.Equal("3.00", text);
    }

    private static CelValue Twice(ReadOnlySpan<CelValue> arguments) =>
        arguments[0].TryGetDecimal(out Decimal128 value) ? CelValue.FromDecimal(value + value) : CelValue.Null;

    private static CelEnvironmentBuilder Builder() => new(CelStandardLibrary.V1);

    // A set of the application with one function, decimalProbe(string) -> string.
    private static CelExtension Own(uint version) =>
        CelExtension.Create(
            "decimal",
            version,
            [new CelFunctionDecl("decimalProbe", CelOverloadDecl.Global("decimal_probe_string", [CelType.String], CelType.String))],
            new Dictionary<string, CelFunctionImplementation> { ["decimal_probe_string"] = static arguments => arguments[0] });

    private static (CelErrorCode Code, string Message) SingleError(CelEnvironmentBuilder builder)
    {
        CelBuildResult result = builder.Build();

        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors);
        return (error.Code, error.Message);
    }
}
#endif
