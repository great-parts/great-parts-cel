using System;
using System.Collections.Generic;
using System.Globalization;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Properties.Evaluation.CostRows;

namespace Great.Parts.Cel.Properties.Evaluation;

// The memory of the extension sets and of time (docs/design/tests.md, "Cost and memory of the extension sets"): the bytes that an
// evaluation allocates beyond its frame are at most the units it charges times B. The rows are those of that page — strings,
// lists, encoders, network, macros, errors, and the conversions of time to a text — on the sizes of CostRows, through Compile and
// through the planner without type checking.
public sealed class ExtensionCostTests
{
    // The sets of the library and the time zone database; the variables are of type dyn, so that the check of a binding charges
    // nothing.
    private static readonly CelEnvironment SetsEnvironment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Strings(5))
        .AddExtension(CelExtensions.Math(3))
        .AddExtension(CelExtensions.Lists(4))
        .AddExtension(CelExtensions.Sets(0))
        .AddExtension(CelExtensions.Encoders(1))
        .AddExtension(CelExtensions.Bindings(1))
        .AddExtension(CelExtensions.TwoVarComprehensions(0))
        .AddExtension(CelExtensions.Network(1))
        .UseTimeZones(ZoneInfoFile.Database())
        .AddVariable("x", CelType.Dyn)
        .AddVariable("p", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    // The sizes of the rows of the builders that grow an array: those of CostRows and 9 and 129, one above a power of two, where
    // a doubling allocates the most.
    private static readonly int[] GrowingSizes = [0, 1, 9, 10, 100, 129, 1000];

    private static readonly Dictionary<string, CostRow> Rows = CreateRows();

    public static TheoryData<string> RowNames() => Names(Rows);

    [Theory]
    [Trait("Requirement", "R-3")]
    [MemberData(nameof(RowNames))]
    public static void Row_AllocatesWithinCost(string row) => Check(SetsEnvironment, row, Rows[row]);

    private static Dictionary<string, CostRow> CreateRows()
    {
        var rows = new Dictionary<string, CostRow>(StringComparer.Ordinal);

        // Strings.
        Add(rows, "strings, replace, many matches", size => With("x.replace('a', 'bb')", Text(size)));
        Add(rows, "strings, replace, no match", size => With("x.replace('b', 'c')", Text(size)));
        Add(rows, "strings, split, many parts", size => With("x.split('a')", Text(size)));
        Add(rows, "strings, join", size => With("x.join(',')", StringList(size)));
        Add(rows, "strings, quote", size => With("strings.quote(x)", CelValue.FromString(new string('"', size))));
        Add(rows, "strings, substring", size => With($"x.substring(0, {size})", Text(size)));
        Add(rows, "strings, trim", size => With("x.trim()", CelValue.FromString("  " + new string('a', size) + "  ")));
        Add(rows, "strings, reverse", size => With("x.reverse()", Text(size)));
        Add(rows, "strings, lowerAscii", size => With("x.lowerAscii()", CelValue.FromString(new string('A', size))));
        Add(rows, "strings, upperAscii", size => With("x.upperAscii()", Text(size)));
        Add(rows, "strings, charAt", size => With("x.charAt(0)", Text(size)));
        Add(rows, "strings, indexOf, pattern over 256", size => Two("x.indexOf(p)", Text(size), CelValue.FromString(new string('a', 300) + "b")));
        Add(rows, "strings, format, %s of a long string", size => With("'%s'.format([x])", Text(size)));
        Add(rows, "strings, format, %s of a list", size => With("'%s'.format([x])", CostTests.IntList(size)));
        Add(rows, "strings, format, %s of a map", size => With("'%s'.format([x])", CostTests.StringKeyMap(size)));
        Add(rows, "strings, format, %f with a precision of 100", _ => With("'%.100f'.format([x])", CelValue.FromDouble(1e300)));
        Add(rows, "strings, format, %x of a long string", size => With("'%x'.format([x])", Text(size)));

        // Lists.
        Add(rows, "lists, slice", size => With($"x.slice(0, {size})", CostTests.IntList(size)));
        Add(rows, "lists, reverse", size => With("x.reverse()", CostTests.IntList(size)));
        Add(rows, "lists, flatten of lists", size => With("x.flatten()", ListOfLists(size)), sizes: GrowingSizes);
        Add(rows, "lists, flatten of a deep list", size => With($"x.flatten({size + 1})", DeepList(size + 1)), sizes: GrowingSizes);
        Add(rows, "lists, sort of numbers", size => With("x.sort()", DescendingInts(size)));
        Add(rows, "lists, sort of strings", size => With("x.sort()", DescendingStrings(size)));
        Add(rows, "lists, sortBy", size => With("x.sortBy(v, -v)", CostTests.IntList(size)));
        Add(rows, "lists, distinct", size => With("x.distinct()", CostTests.IntList(size)));
        Add(rows, "lists, range", size => With($"lists.range({size})", CelValue.FromInt64(0)));

        // Encoders and network.
        Add(rows, "encoders, base64.encode", size => With("base64.encode(x)", CelValue.FromBytes(new byte[size])));
        Add(rows, "encoders, base64.decode", size => With("base64.decode(x)", CelValue.FromString(new string('A', 4 * size))));
        Add(rows, "encoders, json.encode of a nested value", size => With("json.encode(x)", ListOfLists(size)));
        Add(rows, "network, string(ip)", _ => With("string(ip(x))", CelValue.FromString("2001:db8:0:0:0:0:0:1")));
        Add(rows, "network, string(cidr)", _ => With("string(cidr(x))", CelValue.FromString("2001:db8:0:0:0:0:0:1/64")));
        Add(rows, "network, masked", _ => With("cidr(x).masked()", CelValue.FromString("2001:db8:0:0:0:0:0:1/64")));
        Add(rows, "network, ip() of a prefix", _ => With("cidr(x).ip()", CelValue.FromString("2001:db8:0:0:0:0:0:1/64")));
        Add(rows, "network, isIP of a text of the size", size => With("isIP(x)", Text(size)));
        Add(rows, "network, isCIDR of a text of the size", size => With("isCIDR(x)", Text(size)));

        // Macros.
        Add(rows, "macros, transformList", size => With("x.transformList(i, v, v + 1)", CostTests.IntList(size)));
        Add(rows, "macros, transformList with a filter", size => With("x.transformList(i, v, v >= 0, v + 1)", CostTests.IntList(size)));
        Add(rows, "macros, transformMap", size => With("x.transformMap(i, v, v)", CostTests.IntList(size)), sizes: GrowingSizes);
        Add(rows, "macros, transformMap with a filter", size => With("x.transformMap(i, v, v >= 0, v)", CostTests.IntList(size)));
        Add(rows, "macros, transformMapEntry", size => With("x.transformMapEntry(i, v, {v: i})", CostTests.IntList(size)), sizes: GrowingSizes);
        Add(rows, "macros, transformMapEntry with a filter", size => With("x.transformMapEntry(i, v, v >= 0, {v: i})", CostTests.IntList(size)), sizes: GrowingSizes);
        Add(rows, "macros, cel.bind", size => With("cel.bind(a, x, a + a)", CostTests.IntList(size)));
        Add(rows, "macros, math.greatest of three arguments", _ => With("math.greatest(1, 2, 3)", CelValue.FromInt64(0)));

        // Errors: every row is an error on every size.
        Add(rows, "error, format with an unknown letter", size => With("x.format([1])", CelValue.FromString(new string('a', size) + "%z")), isError: true);
        Add(rows, "error, format with a precision of 101", size => With("x.format([1.5])", CelValue.FromString(new string('a', size) + "%.101f")), isError: true);
        Add(rows, "error, ip(x)", size => With("ip(x)", Text(size)), isError: true);
        Add(rows, "error, cidr(x)", size => With("cidr(x)", Text(size)), isError: true);
        Add(rows, "error, base64.decode(x)", size => With("base64.decode(x)", CelValue.FromString(new string('!', size + 1))), isError: true);
        Add(rows, "error, charAt past the end", size => With($"x.charAt({size + 1})", Text(size)), isError: true);
        Add(rows, "error, lists.range(-1)", _ => With("lists.range(-1)", CelValue.FromInt64(0)), isError: true);
        Add(rows, "error, a duplicate key in transformMapEntry", size => With("x.transformMapEntry(i, v, {1: i})", CostTests.IntList(Math.Max(size, 2))), isError: true);

        // Time: the longest texts, and two errors of one and two units.
        Add(rows, "time, string(timestamp)", _ => With("string(x)", CelValue.FromTimestamp(new CelTimestamp(253402300799, 999_999_999))));
        Add(rows, "time, string(duration)", _ => With("string(x)", CelValue.FromDuration(new CelDuration(long.MinValue))));
        Add(rows, "error, timestamp(i) outside the range", _ => With("timestamp(x)", CelValue.FromInt64(long.MaxValue)), isError: true);
        Add(rows, "error, getHours with the zone +1:5", _ => Two("x.getHours(p)", CelValue.FromTimestamp(new CelTimestamp(0, 0)), CelValue.FromString("+1:5")), isError: true);

        return rows;
    }

    private static CelValue StringList(int size)
    {
        var items = new CelValue[size];
        for (int index = 0; index < size; index++)
        {
            items[index] = CelValue.FromString("ab");
        }

        return CelValue.FromList(CelList.Create(items));
    }

    // The integers size, size - 1, …, 1: a list that the sort has to move.
    private static CelValue DescendingInts(int size)
    {
        var items = new CelValue[size];
        for (int index = 0; index < size; index++)
        {
            items[index] = CelValue.FromInt64(size - index);
        }

        return CelValue.FromList(CelList.Create(items));
    }

    private static CelValue DescendingStrings(int size)
    {
        var items = new CelValue[size];
        for (int index = 0; index < size; index++)
        {
            items[index] = CelValue.FromString((size - index).ToString("D4", CultureInfo.InvariantCulture));
        }

        return CelValue.FromList(CelList.Create(items));
    }

    // A list of size lists of three integers.
    private static CelValue ListOfLists(int size)
    {
        var items = new CelValue[size];
        for (int index = 0; index < size; index++)
        {
            items[index] = CelValue.FromList(CelList.Create([CelValue.FromInt64(1), CelValue.FromInt64(2), CelValue.FromInt64(3)]));
        }

        return CelValue.FromList(CelList.Create(items));
    }

    // The integer 1 in depth lists, one inside the other.
    private static CelValue DeepList(int depth)
    {
        CelValue value = CelValue.FromInt64(1);
        for (int level = 0; level < depth; level++)
        {
            value = CelValue.FromList(CelList.Create([value]));
        }

        return value;
    }
}
