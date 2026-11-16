using System.Linq;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Properties.Evaluation.CostRows;

namespace Great.Parts.Cel.Properties.Evaluation;

// The memory of the sets that the first release measured last: optional, sets, the list forms of math and the macros of two
// variables (docs/design/tests.md, "Cost and memory of the extension sets"). The bytes that an evaluation allocates beyond its frame
// are at most the units it charges times B, on the sizes of CostRows, through Compile and through the planner without type checking.
public sealed class OptionalCostTests
{
    // The sizes of the rows that need an element: the greatest of no number is an error.
    private static readonly int[] NonEmpty = [1, 10, 100, 1000];

    // The sizes of the rows of sets, whose units grow with the square of the size: the comparison of 1 000 by 1 000 elements is
    // a million pairs.
    private static readonly int[] Quadratic = [0, 1, 10, 100, 500];

    private static readonly CelEnvironment OptionalEnvironment = Dynamic(new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Optional(2))
        .AddExtension(CelExtensions.Lists(4))
        .AddExtension(CelExtensions.Sets(0))
        .AddExtension(CelExtensions.Math(3))
        .AddExtension(CelExtensions.TwoVarComprehensions(0)));

    private static readonly CostTable Table = new CostTable(OptionalEnvironment)
        // Optional: the calls, each on a value of the size; x is a list of the size where a row does not say otherwise.
        .Add("optional, none", _ => With("optional.none()", CelValue.Null))
        .AddOverList(
            ("optional, of", "optional.of(x)"),
            ("optional, hasValue", "optional.of(x).hasValue()"),
            ("optional, value", "optional.of(x).value()"),
            ("optional, orValue of an empty optional", "optional.none().orValue(x)"),
            ("optional, or of an empty optional", "optional.none().or(optional.of(x))"),
            ("optional, ofNonZeroValue of a list", "optional.ofNonZeroValue(x)"),
            ("optional, first", "x.first()"),
            ("optional, last", "x.last()"),
            ("optional, optional index of a list", "x[?0]"),
            ("optional, optMap with a value", "optional.of(x).optMap(v, v)"),
            ("optional, optMap of an empty optional", "optional.none().optMap(v, x)"),
            ("optional, optFlatMap with a value", "optional.of(x).optFlatMap(v, optional.of(v))"))
        .Add("optional, unwrap", size => With("optional.unwrap(x)", Optionals(size)))
        .Add("optional, unwrapOpt", size => With("x.unwrapOpt()", Optionals(size)))
        .Add("optional, optional index of a map", size => With("x[?'0']", CostTests.StringKeyMap(size)))
        .Add("optional, optional selection of a map", size => With("x.?a", CostTests.StringKeyMap(size)))

        // Literals with `?`: every position is stored or dropped by the optional that stands in it.
        .Add("optional, list literal with ?", size => With("[" + Repeat("?optional.of(1)", size) + "]", CelValue.Null))
        .Add("optional, list literal with ? of empty optionals", size => With("[" + Repeat("?optional.none()", size) + "]", CelValue.Null))
        .Add("optional, map literal with ?", size => With("{" + string.Join(", ", Enumerable.Range(0, size).Select(index => $"?'k{index}': optional.of(1)")) + "}", CelValue.Null))

        // Sets: the units grow with the square of the size, the bytes do not grow.
        .Add("sets, contains", size => Two("sets.contains(x, p)", CostTests.IntList(size), CostTests.IntList(size)), sizes: Quadratic)
        .Add("sets, intersects", size => Two("sets.intersects(x, p)", CostTests.IntList(size), Ints(size, size)), sizes: Quadratic)
        .Add("sets, equivalent", size => Two("sets.equivalent(x, p)", CostTests.IntList(size), CostTests.IntList(size)), sizes: Quadratic)

        // Math: the list forms, and the macros of two variables.
        .Add("math, greatest of a list", size => With("math.greatest(x)", CostTests.IntList(size)), sizes: NonEmpty)
        .Add("math, least of a list", size => With("math.least(x)", CostTests.IntList(size)), sizes: NonEmpty)
        .AddOverList(
            ("two variables, all", "x.all(i, v, v >= 0)"),
            ("two variables, exists", "x.exists(i, v, v < 0)"),
            ("two variables, existsOne", "x.existsOne(i, v, v < 0)"))
        .Add("two variables, all over a map", size => With("x.all(k, v, v >= 0)", CostTests.StringKeyMap(size)));

    public static TheoryData<string> RowNames() => Table.Names();

    [Theory]
    [Trait("Requirement", "R-3")]
    [MemberData(nameof(RowNames))]
    public static void Row_AllocatesWithinCost(string row) => Table.Check(row);

    // A list of size optionals: those at even indexes hold the index, the others are empty.
    private static CelValue Optionals(int size)
    {
        var items = new CelValue[size];
        for (int index = 0; index < size; index++)
        {
            items[index] = CelValue.FromOptional(index % 2 == 0 ? new CelOptional<CelValue>(CelValue.FromInt64(index)) : default);
        }

        return CelValue.FromList(CelList.Create(items));
    }

    // The integers first, first + 1, … of the size.
    private static CelValue Ints(int size, int first)
    {
        var items = new CelValue[size];
        for (int index = 0; index < size; index++)
        {
            items[index] = CelValue.FromInt64(first + index);
        }

        return CelValue.FromList(CelList.Create(items));
    }

    private static string Repeat(string element, int count) => string.Join(", ", Enumerable.Repeat(element, count));
}
