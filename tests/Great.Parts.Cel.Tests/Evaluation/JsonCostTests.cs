using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// The cost of the operations on a JSON object and of the index of a JSON array, by the rows of the cost table for application
// data. The documents have n properties whose names have L code units, 0, 1 and 10 of them, with L of 1, 10 and 11, and the
// keys that the programs look up have K code units. A literal costs 0 and a read of a variable 1, so the sums below are the
// read of the variable, the node or the constant unit of the call, and the row:
//   lookup (selection, has, index)  n · (1 + ⌈K/10⌉)
//   size                            Σ (1 + ⌈Lᵢ/10⌉), before each property is processed
//   enumeration                     1 + Σ (1 + ⌈Lᵢ/10⌉); the equality of two objects has it for each of the two
//   in                              the row above + ⌈K/10⌉
//   equality of two objects         1, the two rows above, and for each key of the left object its lookup in the right one
//                                   (1 + n · (1 + ⌈L/10⌉)) and the equality of two numbers (1)
//   index of an array of n          n
//   a string read                   1 + ⌈L/10⌉ before it is created, L the length of the raw token without its quotes
public sealed class JsonCostTests
{
    private const ulong Limit = 1_000_000;

    public static TheoryData<int, int> Documents => new()
    {
        { 0, 1 }, { 1, 1 }, { 10, 1 },
        { 0, 10 }, { 1, 10 }, { 10, 10 },
        { 0, 11 }, { 1, 11 }, { 10, 11 },
    };

    public static TheoryData<int, int, int> DocumentsAndKeys
    {
        get
        {
            var data = new TheoryData<int, int, int>();
            foreach (int keyLength in new[] { 1, 10, 11 })
            {
                foreach (int n in new[] { 0, 1, 10 })
                {
                    foreach (int nameLength in new[] { 1, 10, 11 })
                    {
                        data.Add(n, nameLength, keyLength);
                    }
                }
            }

            return data;
        }
    }

    // The read of j, the node, the lookup.
    [Theory]
    [MemberData(nameof(DocumentsAndKeys))]
    public static void SelectionOfAKey_IsChargedOnePlusOnePlusTheLookup(int n, int nameLength, int keyLength)
    {
        string key = new('z', keyLength);

        Assert.Equal(2 + (ulong)n * (1 + Tenths(keyLength)), Units("j." + key, Object(n, nameLength)));
    }

    [Theory]
    [MemberData(nameof(DocumentsAndKeys))]
    public static void PresenceOfAKey_IsChargedLikeTheSelection(int n, int nameLength, int keyLength)
    {
        string key = new('z', keyLength);

        Assert.Equal(2 + (ulong)n * (1 + Tenths(keyLength)), Units("has(j." + key + ")", Object(n, nameLength)));
    }

    // The read of j, the constant unit of index_map, the lookup.
    [Theory]
    [MemberData(nameof(DocumentsAndKeys))]
    public static void IndexByAKey_IsChargedTheLookup(int n, int nameLength, int keyLength)
    {
        string key = new('z', keyLength);

        Assert.Equal(2 + (ulong)n * (1 + Tenths(keyLength)), Units("j['" + key + "']", Object(n, nameLength)));
    }

    // A key that is found costs the same as one that is not: the lookup walks every property.
    [Theory]
    [MemberData(nameof(Documents))]
    public static void LookupOfAFoundKey_IsChargedLikeAMissingOne(int n, int nameLength)
    {
        if (n == 0)
        {
            return;
        }

        string name = Name(0, nameLength);
        ulong expected = 2 + ((ulong)n * (1 + Tenths(nameLength)));

        Assert.Equal(expected, Units("j." + name, Object(n, nameLength)));
        Assert.Equal(expected, Units("has(j." + name + ")", Object(n, nameLength)));
        Assert.Equal(expected, Units("j['" + name + "']", Object(n, nameLength)));
    }

    // The read of j, the constant unit of size_map or map_size, then the term of each property.
    [Theory]
    [MemberData(nameof(Documents))]
    public static void SizeOfAnObject_IsChargedOnePlusTheTermOfEachProperty(int n, int nameLength)
    {
        ulong expected = 2 + Names(n, nameLength);

        Assert.Equal(expected, Units("size(j)", Object(n, nameLength)));
        Assert.Equal(expected, Units("j.size()", Object(n, nameLength)));
    }

    // The read of j, the constant unit of in_map, ⌈K/10⌉ for the key, then the term of each property.
    [Theory]
    [MemberData(nameof(DocumentsAndKeys))]
    public static void InOnAnObject_IsChargedTheSizeRowAndTheKey(int n, int nameLength, int keyLength)
    {
        string key = new('z', keyLength);

        Assert.Equal(2 + Tenths(keyLength) + Names(n, nameLength), Units("'" + key + "' in j", Object(n, nameLength)));
    }

    // A comprehension over an object and over an array of the same count differ by the leading 1 and the terms of the properties.
    [Theory]
    [MemberData(nameof(Documents))]
    public static void EnumerationOfAnObject_IsChargedTheTermOfEachProperty(int n, int nameLength)
    {
        ulong array = Units("j.all(k, true)", Array(n));
        ulong obj = Units("j.all(k, true)", Object(n, nameLength));

        Assert.Equal(1 + Names(n, nameLength), obj - array);
    }

    // The read of a and of b, the unit of the pair, the leading 1 and the terms of both objects, then for each key of a the lookup in
    // b and the equality of two numbers.
    [Theory]
    [MemberData(nameof(Documents))]
    public static void EqualityOfObjects_IsChargedTheTermsOfBothAndEachLookupInTheSecond(int n, int nameLength)
    {
        string json = Object(n, nameLength);
        ulong perKey = 1 + ((ulong)n * (1 + Tenths(nameLength))) + 1;
        ulong expected = 2 + 1 + (2 * (1 + Names(n, nameLength))) + ((ulong)n * perKey);

        Assert.Equal(expected, Units("a == b", ("a", json), ("b", json)));
    }

    // The terms are charged before each property: a limit between two terms stops the check of the repeats there.
    [Fact]
    public static void SizeOfAnObjectAboveTheLimit_IsCostLimitExceeded()
    {
        string json = Object(10, 10);
        ulong needed = 2 + Names(10, 10);

        Assert.Equal(CelValueKind.Int, Run("size(j)", needed, ("j", json)).Kind);
        CelError error = Plans.Error(Run("size(j)", needed - 1, ("j", json)));
        Assert.Equal(CelErrorCode.CostLimitExceeded, error.Code);
    }

    // The read of j, the constant unit of index_list, n.
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    public static void IndexOfAnArray_IsChargedOnePlusN(int n)
    {
        Assert.Equal(2 + (ulong)n, Units("j[0]", Array(n)));
    }

    // An array of 3 elements: ja[1] is 1 + (1 + 3).
    [Fact]
    public static void IndexOfAnArrayOfThree_IsFive()
    {
        Assert.Equal(5UL, Units("j[1]", "[1, 2, 3]"));
    }

    // The read of j, the node, the lookup of the key in an object of two properties, and the read of the string of the value.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(11)]
    public static void SelectionOfAString_IsChargedTheLookupAndTheStringBeforeItIsCreated(int length)
    {
        string json = "{\"s\": \"" + new string('x', length) + "\", \"t\": 1}";

        Assert.Equal(2 + (2 * 2) + 1 + Tenths(length), Units("j.s", json));
        Assert.Equal(2 + (2 * 2) + 1 + Tenths(length), Units("j['s']", json));
    }

    // The length is that of the raw token: nine escape sequences of two characters are 18, not the 9 of the string.
    [Fact]
    public static void StringWithEscapes_IsChargedByItsRawLength()
    {
        string json = "{\"s\": \"" + string.Concat(Enumerable.Repeat("\\n", 9)) + "\", \"t\": 1}";

        Assert.Equal(9UL, Units("j.s", json));
    }

    // has() does not read the value, so it creates no string and charges none.
    [Fact]
    public static void PresenceOfAString_ChargesNoString()
    {
        Assert.Equal(6UL, Units("has(j.s)", "{\"s\": \"abcdefghijk\", \"t\": 1}"));
        Assert.Equal(2 + Tenths(1) + Names(2, 1), Units("'s' in j", "{\"s\": \"abcdefghijk\", \"t\": 1}"));
    }

    // The read of j, the constant unit of index_list, n, and the string of the element.
    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 2)]
    public static void IndexOfAnArrayOfStrings_IsChargedTheStringOfTheElement(int index, int stringUnits)
    {
        Assert.Equal(2 + 2 + (ulong)stringUnits, Units("j[" + index.ToString(CultureInfo.InvariantCulture) + "]", "[\"abcdefghijk\", \"x\"]"));
    }

    // A comprehension over an array of strings reads each element: the read of j, a step for each element, and the strings.
    [Fact]
    public static void RangeOfAnArrayOfStrings_IsChargedTheStringOfEachElement()
    {
        Assert.Equal(8UL, Units("j.all(x, true)", "[\"abcdefghijk\", \"x\"]"));
    }

    // The range of a comprehension over an object reads the names only: a string value is not created, so not charged.
    [Fact]
    public static void RangeOfAnObject_ChargesNoStringOfItsValues()
    {
        Assert.Equal(
            Units("j.all(k, true)", "{\"s\": 1, \"t\": 1}"),
            Units("j.all(k, true)", "{\"s\": \"abcdefghijk\", \"t\": 1}"));
    }

    // Each read of an identifier bound to a string charges its units: the read of s and the string of 11.
    [Fact]
    public static void IdentifierBoundToAString_IsChargedTheStringAtEachRead()
    {
        Assert.Equal(4UL, Units("s", "s", "\"abcdefghijk\""));
        Assert.Equal(1UL, Units("s", "s", "1"));
    }

    // The string is charged before it is created: a limit one unit below the cost stops the read with CostLimitExceeded.
    [Fact]
    public static void StringAboveTheLimit_IsCostLimitExceeded()
    {
        const string Json = "[\"abcdefghijk\"]";
        ulong needed = Units("j[0]", Json);

        Assert.Equal(CelValueKind.String, Run("j[0]", needed, ("j", Json)).Kind);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Plans.Error(Run("j[0]", needed - 1, ("j", Json))).Code);
    }

    private static ulong Tenths(int length) => ((ulong)length + 9) / 10;

    // The sum of the terms of n properties of names of L code units.
    private static ulong Names(int n, int nameLength) => (ulong)n * (1 + Tenths(nameLength));

    // A name of nameLength code units that is a valid identifier; the names of one object are distinct for up to 10 of length 1.
    private static string Name(int index, int nameLength) =>
        nameLength == 1
            ? ((char)('a' + index)).ToString()
            : "a" + index.ToString(CultureInfo.InvariantCulture).PadLeft(nameLength - 1, '0');

    private static string Object(int n, int nameLength)
    {
        var json = new StringBuilder("{");
        for (int i = 0; i < n; i++)
        {
            json.Append(i == 0 ? string.Empty : ", ").Append('"').Append(Name(i, nameLength)).Append("\": ").Append(1);
        }

        return json.Append('}').ToString();
    }

    private static string Array(int n)
    {
        var json = new StringBuilder("[");
        for (int i = 0; i < n; i++)
        {
            json.Append(i == 0 ? string.Empty : ", ").Append(1);
        }

        return json.Append(']').ToString();
    }

    private static ulong Units(string expression, string json) => Units(expression, ("j", json));

    private static ulong Units(string expression, string name, string json) => Units(expression, (name, json));

    private static ulong Units(string expression, params (string Name, string Json)[] documents)
    {
        _ = Run(expression, Limit, out ulong units, documents);
        return units;
    }

    private static CelValue Run(string expression, ulong limit, params (string Name, string Json)[] documents) =>
        Run(expression, limit, out _, documents);

    private static CelValue Run(string expression, ulong limit, out ulong units, params (string Name, string Json)[] documents)
    {
        var builder = new CelEnvironmentBuilder(CelStandardLibrary.V1);
        var parsed = new List<JsonDocument>();
        var bindings = new Dictionary<string, CelValue>();
        try
        {
            foreach ((string name, string json) in documents)
            {
                builder.AddVariable(name, CelType.Dyn);
                if (json.Length == 0)
                {
                    continue;
                }

                JsonDocument document = JsonDocument.Parse(json);
                parsed.Add(document);
                bindings[name] = JsonRules.FromElement(document.RootElement);
            }

            Plan plan = builder.Build().GetEnvironmentOrThrow().Compile<CelValue>(expression).GetProgramOrThrow().Plan;
            Frame frame = plan.CreateFrame(bindings, limit, CancellationToken.None);
            CelValue value = plan.Run(frame);
            units = frame.ChargedUnits;
            return value;
        }
        finally
        {
            foreach (JsonDocument document in parsed)
            {
                document.Dispose();
            }
        }
    }
}
