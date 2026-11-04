using System;
using System.Text;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Library;

// The implementations of !, equality and ordering, called directly with a call site.
public sealed class ComparisonTests
{
    private static readonly long[] Ints = [long.MinValue, -2, -1, 0, 1, 2, long.MaxValue];

    private static readonly string[] Texts = ["", "a", "ab", "b", "￿", "\U0001F600", "", "Z"];

    private static readonly LineTable Lines = new("x");

    [Fact]
    public static void Relations_OfInts_AreTheOperators()
    {
        foreach (long left in Ints)
        {
            foreach (long right in Ints)
            {
                CelValue[] arguments = [CelValue.FromInt64(left), CelValue.FromInt64(right)];
                Assert.Equal(left < right, Bool(Relation(Comparisons.Relation.Less, CelValueKind.Int, CelValueKind.Int, arguments)));
                Assert.Equal(left <= right, Bool(Relation(Comparisons.Relation.LessEquals, CelValueKind.Int, CelValueKind.Int, arguments)));
                Assert.Equal(left > right, Bool(Relation(Comparisons.Relation.Greater, CelValueKind.Int, CelValueKind.Int, arguments)));
                Assert.Equal(left >= right, Bool(Relation(Comparisons.Relation.GreaterEquals, CelValueKind.Int, CelValueKind.Int, arguments)));
            }
        }
    }

    // Strings order by code points: the oracle compares the sequences of scalar values.
    [Fact]
    public static void Relations_OfStrings_AreTheOrderOfCodePoints()
    {
        foreach (string left in Texts)
        {
            foreach (string right in Texts)
            {
                int expected = CompareCodePoints(left, right);
                CelValue[] arguments = [CelValue.FromString(left), CelValue.FromString(right)];
                Assert.Equal(expected < 0, Bool(Relation(Comparisons.Relation.Less, CelValueKind.String, CelValueKind.String, arguments)));
                Assert.Equal(expected >= 0, Bool(Relation(Comparisons.Relation.GreaterEquals, CelValueKind.String, CelValueKind.String, arguments)));
            }
        }
    }

    [Fact]
    public static void Relations_WithANaN_AreFalse()
    {
        foreach (Comparisons.Relation relation in Enum.GetValues<Comparisons.Relation>())
        {
            Assert.False(Bool(Relation(relation, CelValueKind.Double, CelValueKind.Double, [CelValue.FromDouble(double.NaN), CelValue.FromDouble(double.NaN)])));
            Assert.False(Bool(Relation(relation, CelValueKind.Int, CelValueKind.Double, [CelValue.FromInt64(1), CelValue.FromDouble(double.NaN)])));
            Assert.False(Bool(Relation(relation, CelValueKind.Double, CelValueKind.UInt, [CelValue.FromDouble(double.NaN), CelValue.FromUInt64(1)])));
        }
    }

    [Fact]
    public static void Relations_AcrossNumericKinds_CompareOnOneNumberLine()
    {
        Assert.True(Bool(Relation(Comparisons.Relation.Less, CelValueKind.Int, CelValueKind.UInt, [CelValue.FromInt64(-1), CelValue.FromUInt64(0)])));
        Assert.True(Bool(Relation(Comparisons.Relation.Greater, CelValueKind.UInt, CelValueKind.Int, [CelValue.FromUInt64(ulong.MaxValue), CelValue.FromInt64(long.MaxValue)])));
        Assert.True(Bool(Relation(Comparisons.Relation.LessEquals, CelValueKind.Double, CelValueKind.Int, [CelValue.FromDouble(-0.5), CelValue.FromInt64(0)])));
        Assert.True(Bool(Relation(Comparisons.Relation.GreaterEquals, CelValueKind.UInt, CelValueKind.Double, [CelValue.FromUInt64(3), CelValue.FromDouble(3.0)])));
        Assert.False(Bool(Relation(Comparisons.Relation.Greater, CelValueKind.Int, CelValueKind.Double, [CelValue.FromInt64(long.MaxValue), CelValue.FromDouble(1e19)])));
    }

    [Fact]
    public static void Relations_OfBoolAndBytes()
    {
        Assert.True(Bool(Relation(Comparisons.Relation.Less, CelValueKind.Bool, CelValueKind.Bool, [CelValue.FromBoolean(false), CelValue.FromBoolean(true)])));
        Assert.True(Bool(Relation(Comparisons.Relation.Greater, CelValueKind.Bytes, CelValueKind.Bytes, [CelValue.FromBytes(new byte[] { 0xFF }), CelValue.FromBytes(new byte[] { 0x01, 0x02 })])));
        Assert.True(Bool(Relation(Comparisons.Relation.Less, CelValueKind.Bytes, CelValueKind.Bytes, [CelValue.FromBytes(new byte[] { 0x01 }), CelValue.FromBytes(new byte[] { 0x01, 0x00 })])));
    }

    // An overload takes only the kinds of its parameters, also when the other kind is ordered with it.
    [Fact]
    public static void Relation_OfOtherKinds_IsEvalNoMatchingOverload()
    {
        CelValue result = Relation(Comparisons.Relation.Less, CelValueKind.Int, CelValueKind.Int, [CelValue.FromInt64(1), CelValue.FromDouble(2)]);

        AssertError(CelErrorCode.EvalNoMatchingOverload, "no such overload: _<_(int, double)", result);
        AssertError(
            CelErrorCode.EvalNoMatchingOverload,
            "no such overload: _<_(string, int)",
            Relation(Comparisons.Relation.Less, CelValueKind.Int, CelValueKind.Int, [CelValue.FromString("a"), CelValue.FromInt64(2)]));
    }

    [Fact]
    public static void LogicalNot_NegatesABool()
    {
        var site = new CallSite("!_", 0, Lines);

        Assert.False(Bool(Comparisons.LogicalNot([CelValue.FromBoolean(true)], site)));
        Assert.True(Bool(Comparisons.LogicalNot([CelValue.FromBoolean(false)], site)));
        AssertError(CelErrorCode.EvalNoMatchingOverload, "no such overload: !_(int)", Comparisons.LogicalNot([CelValue.FromInt64(0)], site));
    }

    [Fact]
    public static void Equality_IsCelEqualityAndItsNegation()
    {
        var site = new CallSite("_==_", 0, Lines);
        CelValue one = CelValue.FromInt64(1);
        CelValue oneDouble = CelValue.FromDouble(1.0);
        CelValue text = CelValue.FromString("1");

        Assert.True(Bool(Comparisons.AreEqual([one, oneDouble], site)));
        Assert.False(Bool(Comparisons.AreNotEqual([one, oneDouble], site)));
        Assert.False(Bool(Comparisons.AreEqual([one, text], site)));
        Assert.True(Bool(Comparisons.AreNotEqual([one, text], site)));
        Assert.False(Bool(Comparisons.AreEqual([CelValue.FromDouble(double.NaN), CelValue.FromDouble(double.NaN)], site)));
        Assert.True(Bool(Comparisons.AreNotEqual([CelValue.FromDouble(double.NaN), CelValue.FromDouble(double.NaN)], site)));
    }

    // A list nested beyond the stack of a small thread: the comparison stops with InsufficientStack at the call.
    [Fact]
    public static void Equality_OfValuesNestedTooDeeply_IsInsufficientStackAtTheCall()
    {
        CelValue left = Nested(100_000);
        CelValue right = Nested(100_000);
        CelValue result = default;
        var thread = new System.Threading.Thread(() => result = Comparisons.AreEqual([left, right], new CallSite("_==_", 3, new LineTable("a == b"))), 256 * 1024);
        thread.Start();
        thread.Join();

        CelError error = AssertError(CelErrorCode.InsufficientStack, "insufficient execution stack to continue comparing values", result);
        Assert.Equal(3, error.Location.Offset);
    }

    private static CelValue Nested(int depth)
    {
        CelValue value = CelValue.FromInt64(0);
        for (int i = 0; i < depth; i++)
        {
            value = CelValue.FromList(CelList.Create(value));
        }

        return value;
    }

    private static CelValue Relation(Comparisons.Relation relation, CelValueKind left, CelValueKind right, CelValue[] arguments) =>
        Comparisons.Ordering(relation, left, right)(arguments, new CallSite("_<_", 0, Lines));

    private static int CompareCodePoints(string left, string right)
    {
        StringRuneEnumerator leftRunes = left.EnumerateRunes();
        StringRuneEnumerator rightRunes = right.EnumerateRunes();
        while (true)
        {
            bool hasLeft = leftRunes.MoveNext();
            bool hasRight = rightRunes.MoveNext();
            if (!hasLeft || !hasRight)
            {
                return hasLeft.CompareTo(hasRight);
            }

            int comparison = leftRunes.Current.Value.CompareTo(rightRunes.Current.Value);
            if (comparison != 0)
            {
                return comparison;
            }
        }
    }

    private static bool Bool(CelValue value)
    {
        Assert.True(value.TryGetBoolean(out bool result), value.Kind.ToString());
        return result;
    }

    private static CelError AssertError(CelErrorCode code, string message, CelValue result)
    {
        Assert.True(result.TryGetError(out CelError? error), result.Kind.ToString());
        Assert.Equal((code, message), (error.Code, error.Message));
        return error;
    }
}
