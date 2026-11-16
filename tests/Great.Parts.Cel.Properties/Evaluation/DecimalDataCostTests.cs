#if NET11_0_OR_GREATER
using System;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Properties.Evaluation.CostRows;

namespace Great.Parts.Cel.Properties.Evaluation;

// The memory of the rows of the set decimal (docs/design/tests.md, "Cost and memory of the extension sets"): the conversions of a text
// and of a number, the text of a value and the arithmetic. A value of the type decimal takes no memory beyond its 16 bytes, so the
// rows measure what the conversions of a text and of a double allocate. The bytes that an evaluation allocates beyond its frame are at
// most the units it charges times B, on the sizes of CostRows, through Compile and through the planner without type checking.
public sealed class DecimalDataCostTests
{
    // The longest text of a decimal that the type holds: 34 digits and a point.
    private const string LongestText = "-1.234567890123456789012345678901234";

    private static readonly CostTable Table = new CostTable(Dynamic(new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Decimal(1))))
        // The conversions of a text: a number of the size in digits, at most 34, and a text of the size that does not parse.
        .Add("decimal, decimal(string)", size => With("decimal(x)", CelValue.FromString(new string('1', Math.Clamp(size, 1, 34)))))
        .Add("decimal, decimal(string) of a text that does not parse", size => With("decimal(x)", Text(size)), isError: true)
        .Add("decimal, string(decimal)", _ => With("string(decimal(x))", CelValue.FromString(LongestText)))

        // The conversions of numbers.
        .Add("decimal, decimal(double), a third", _ => With("decimal(x)", CelValue.FromDouble(1.0 / 3.0)))
        .Add("decimal, decimal(double), a large number", _ => With("decimal(x)", CelValue.FromDouble(1e300)))
        .Add("decimal, decimal(int)", _ => With("decimal(x)", CelValue.FromInt64(long.MinValue)))
        .Add("decimal, int(decimal)", _ => With("int(decimal(x))", CelValue.FromString("1234")))
        .Add("decimal, double(decimal)", _ => With("double(decimal(x))", CelValue.FromString(LongestText)))

        // The arithmetic and the comparison of two values.
        .Add("decimal, sum", _ => Two("decimal(x) + decimal(p)", CelValue.FromString(LongestText), CelValue.FromString("2.25")))
        .Add("decimal, difference", _ => Two("decimal(x) - decimal(p)", CelValue.FromString(LongestText), CelValue.FromString("2.25")))
        .Add("decimal, product", _ => Two("decimal(x) * decimal(p)", CelValue.FromString(LongestText), CelValue.FromString("2.25")))
        .Add("decimal, quotient", _ => Two("decimal(x) / decimal(p)", CelValue.FromString("1"), CelValue.FromString("3")))
        .Add("decimal, negation", _ => With("-decimal(x)", CelValue.FromString(LongestText)))
        .Add("decimal, comparison", _ => Two("decimal(x) < decimal(p)", CelValue.FromString(LongestText), CelValue.FromString("2.25")))
        .Add("decimal, equality", _ => Two("decimal(x) == decimal(p)", CelValue.FromString("1.5"), CelValue.FromString("1.50")));

    public static TheoryData<string> RowNames() => Table.Names();

    [Theory]
    [Trait("Requirement", "R-3")]
    [MemberData(nameof(RowNames))]
    public static void Row_AllocatesWithinCost(string row) => Table.Check(row);
}
#endif
