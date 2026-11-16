using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Properties.Evaluation.CostRows;

namespace Great.Parts.Cel.Properties.Evaluation;

// The memory of the rows of the standard library and of time that the first release measured only through the cases of the generator:
// equality and order, `in`, the index of a list and of a map, the search of a short pattern, `matches` in its steady state, and the
// conversions and receivers of time that take a text (docs/design/tests.md, "Cost and memory of the extension sets"). The bytes that an
// evaluation allocates beyond its frame are at most the units it charges times B, on the sizes of CostRows, through Compile and
// through the planner without type checking.
public sealed class StandardCostTests
{
    // The sizes of the rows that read an element: a list or a map of no element has none to read.
    private static readonly int[] NonEmpty = [1, 10, 100, 1000];

    // The time zone database, so that the receivers of time with a zone name are measured.
    private static readonly CelEnvironment StandardEnvironment = Dynamic(new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .UseTimeZones(ZoneInfoFile.Database()));

    private static readonly CostTable Table = new CostTable(StandardEnvironment)
        // Equality and order: the two operands are two objects with the same content, so the comparison goes to the end.
        .Add("equality, two lists", size => Two("x == p", CostTests.IntList(size), CostTests.IntList(size)))
        .Add("equality, two maps with string keys", size => Two("x == p", CostTests.StringKeyMap(size), CostTests.StringKeyMap(size)))
        .Add("equality, two strings", size => Two("x == p", Text(size), Text(size)))
        .Add("equality, two bytes", size => Two("x == p", CelValue.FromBytes(new byte[size]), CelValue.FromBytes(new byte[size])))
        .Add("ordering, two strings", size => Two("x < p", Text(size), Text(size)))
        .Add("ordering, two bytes", size => Two("x < p", CelValue.FromBytes(new byte[size]), CelValue.FromBytes(new byte[size])))

        // `in` and the index: the element that is looked for is not in the list, so `in` goes through all of it.
        .Add("in, a list", size => With($"{size} in x", CostTests.IntList(size)))
        .Add("in, a map with string keys", size => With("'0' in x", CostTests.StringKeyMap(size)))
        .Add("index, a list", size => With("x[0]", CostTests.IntList(size)), sizes: NonEmpty)
        .Add("index, a map with string keys", size => With("x['0']", CostTests.StringKeyMap(size)), sizes: NonEmpty)
        .Add("size, a string", size => With("size(x)", Text(size)))
        .Add("size, a list", size => With("size(x)", CostTests.IntList(size)))

        // Text: the search of a pattern of one unit, and `matches` with a literal pattern and with one from a variable, whose
        // evaluation after the first finds its program in the cache.
        .Add("contains, a short pattern", size => With("x.contains('b')", Text(size)))
        .Add("startsWith", size => With("x.startsWith('b')", Text(size)))
        .Add("endsWith", size => With("x.endsWith('b')", Text(size)))
        .Add("matches, a literal pattern", size => With("x.matches('a+b')", Text(size)))
        .Add("matches, a pattern from a variable", size => Two("x.matches(p)", Text(size), CelValue.FromString("a+b")))

        // Time: the conversions of a text and the receivers with a zone.
        .Add("time, timestamp(string)", _ => With("timestamp(x)", CelValue.FromString("2009-02-13T23:31:30Z")))
        .Add("time, duration(string)", _ => With("duration(x)", CelValue.FromString("72h3m0.5s")))
        .Add("time, getHours with a zone name", _ => Two("x.getHours(p)", CelValue.FromTimestamp(new CelTimestamp(1_234_567_890, 0)), CelValue.FromString("Europe/Paris")))
        .Add("time, getHours with an offset", _ => Two("x.getHours(p)", CelValue.FromTimestamp(new CelTimestamp(1_234_567_890, 0)), CelValue.FromString("+01:30")))
        .Add("time, getDayOfWeek without a zone", _ => With("x.getDayOfWeek()", CelValue.FromTimestamp(new CelTimestamp(1_234_567_890, 0))))
        .Add("time, timestamp plus duration", _ => Two("x + p", CelValue.FromTimestamp(new CelTimestamp(1_234_567_890, 0)), CelValue.FromDuration(new CelDuration(3_600_000_000_000))));

    public static TheoryData<string> RowNames() => Table.Names();

    [Theory]
    [Trait("Requirement", "R-3")]
    [MemberData(nameof(RowNames))]
    public static void Row_AllocatesWithinCost(string row) => Table.Check(row);
}
