using System;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Tests.Syntax;
using Xunit;

namespace Great.Parts.Cel.Tests.Library;

// The table of the standard library, version 1, of the walking skeleton.
public sealed class StandardLibraryV1Tests
{
    // The cost of a call of each overload in units, as the cost table of the documentation gives it: a
    // logical operator costs nothing beyond the operands it evaluates; an int arithmetic call takes
    // constant time without allocation and costs 1 unit. No overload has a part that grows with the data.
    private const string Costs = """
        logical_or → 0
        add_int64 → 1
        divide_int64 → 1
        negate_int64 → 1
        """;

    public static TheoryData<string, string> CostCases => CaseTable.Parse(Costs);

    [Fact]
    public static void Table_HasTheOverloadsOfTheWalkingSkeletonInTheOrderOfCelGo()
    {
        Assert.Equal(
            [
                ("_||_", "logical_or", "(bool, bool) -> bool"),
                ("_+_", "add_int64", "(int, int) -> int"),
                ("_/_", "divide_int64", "(int, int) -> int"),
                ("-_", "negate_int64", "(int) -> int"),
            ],
            StandardLibraryV1.Overloads.Select(overload => (overload.Function, overload.Declaration.OverloadId, Signature(overload.Declaration))));
        Assert.All(StandardLibraryV1.Overloads, overload => Assert.False(overload.Declaration.IsReceiverStyle));
        Assert.All(StandardLibraryV1.Overloads, overload => Assert.Equal("standard library v1", overload.Source));
    }

    [Theory]
    [MemberData(nameof(CostCases))]
    public static void Cost_IsTheUnitsOfTheCostTable(string overloadId, string units)
    {
        OverloadBinding overload = Assert.Single(StandardLibraryV1.Overloads, overload => overload.Declaration.OverloadId == overloadId);

        Assert.Equal(ulong.Parse(units, CultureInfo.InvariantCulture), overload.FixedCost);
        Assert.Null(overload.ArgumentCost);
    }

    [Fact]
    public static void EveryOverload_HasARowInTheCostTable()
    {
        Assert.Equal(
            CostCases.Select(row => row.Data.Item1).Order(StringComparer.Ordinal),
            StandardLibraryV1.Overloads.Select(overload => overload.Declaration.OverloadId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public static void LogicalOr_HasNoImplementationAndTheArithmeticHasOne()
    {
        Assert.Equal(
            [("logical_or", false), ("add_int64", true), ("divide_int64", true), ("negate_int64", true)],
            StandardLibraryV1.Overloads.Select(overload => (overload.Declaration.OverloadId, overload.Implementation is not null)));
    }

    [Fact]
    public static void ByFunction_GroupsTheOverloadsOfEachFunction()
    {
        Assert.Equal(["-_", "_+_", "_/_", "_||_"], StandardLibraryV1.ByFunction.Keys.Order(StringComparer.Ordinal));
        Assert.All(
            StandardLibraryV1.ByFunction,
            entry => Assert.Equal(StandardLibraryV1.Overloads.Where(overload => overload.Function == entry.Key), entry.Value));
    }

    [Fact]
    public static void VersionOne_IsAHandleToTheTable()
    {
        Assert.Equal(StandardLibraryV1.Overloads, CelStandardLibrary.V1.Overloads);
    }

    private static string Signature(CelOverloadDecl declaration) =>
        "(" + string.Join(", ", declaration.ParameterTypes.Select(type => type.ToString())) + ") -> " + declaration.ResultType;
}
