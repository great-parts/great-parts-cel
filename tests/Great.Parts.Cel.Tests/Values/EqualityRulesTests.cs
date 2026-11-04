using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// EqualityRules.AreEqual and CelValue.CelEquals: CEL equality by the language definition, with the cost of each pair
// charged before it is compared.
public sealed class EqualityRulesTests
{
    // "<left> <right> → equal | different"; the samples are named in Sample.
    private const string Pairs = """
        int-7 uint-7 → equal
        int-7 double-7 → equal
        uint-7 double-7 → equal
        int-minus-3 uint-3 → different
        int-2^54+1 double-2^54 → equal
        uint-near-2^64 double-2^64 → equal
        int-near-minus-2^63 double-minus-2^63 → different
        int-7 double-1e19 → different
        double-nan double-nan → different
        double-nan int-0 → different
        double-minus-zero uint-0 → equal
        string-x string-x → equal
        string-decomposed string-composed → different
        string-x bytes-x → different
        null null → equal
        null bool-false → different
        bool-true bool-true → equal
        bool-true bool-false → different
        bytes-x bytes-x-copy → equal
        bytes-x bytes-xy → different
        type-int type-int → equal
        type-list-int type-list-dyn → equal
        type-wrapper-int type-int → different
        type-type-int type-type-string → equal
        list-1-a list-1u-a → equal
        list-1 list-1-2 → different
        list-empty list-empty → equal
        list-nested-1 list-nested-1.0 → equal
        list-1 int-7 → different
        map-1-a-b-2 map-b-2.0-1u-a → equal
        map-1-a map-2-a → different
        map-k-list-1 map-k-list-2 → different
        map-1-a list-1 → different
        """;

    public static TheoryData<string, string> PairCases => CaseTable.Parse(Pairs);

    [Theory]
    [MemberData(nameof(PairCases))]
    public static void AreEqual_FollowsTheLanguageDefinition(string pair, string expected)
    {
        string[] names = KeyRulesTests.Split(pair, " ");
        ValueEquality expectedResult = expected == "equal" ? ValueEquality.Equal : ValueEquality.NotEqual;

        Assert.Equal(expectedResult, EqualityRules.AreEqual(Sample(names[0]), Sample(names[1]), null, out CelValue error));
        Assert.Equal(expectedResult, EqualityRules.AreEqual(Sample(names[1]), Sample(names[0]), new RecordingMeter(), out _));
        Assert.Equal(CelValueKind.Null, error.Kind);
        Assert.True(CelValue.CelEquals(Sample(names[0]), Sample(names[1])).TryGetBoolean(out bool result));
        Assert.Equal(expected == "equal", result);
    }

    [Fact]
    public void ErrorOperand_IsTheResult()
    {
        CelValue first = ErrorValue("first");
        CelValue second = ErrorValue("second");

        Assert.Equal(ValueEquality.Error, EqualityRules.AreEqual(first, second, null, out CelValue error));
        Assert.True(error.TryGetError(out CelError? read));
        Assert.Equal("first", read.Message);
        Assert.Equal(ValueEquality.Error, EqualityRules.AreEqual(CelValue.FromInt64(1), second, null, out error));
        Assert.Equal(second, error);
        Assert.Equal(second, CelValue.CelEquals(CelValue.Null, second));
    }

    // "<left> <right> → <charges in order>".
    private const string Charges = """
        int-7 string-x → 1
        double-nan double-nan → 1
        string-eleven string-x → 3
        bytes-x bytes-xy → 3
        type-int type-list-int → 1
        list-1 list-1-2 → 1
        list-1-abc list-1-abd → 1 1 2
        list-1-2-3 list-1-5-3 → 1 1 1
        map-long-key-1 map-long-key-1 → 1 3 1
        map-1-a map-1-a → 1 1 2
        map-1-a map-2-a → 1 1
        """;

    public static TheoryData<string, string> ChargeCases => CaseTable.Parse(Charges);

    [Theory]
    [MemberData(nameof(ChargeCases))]
    public static void AreEqual_ChargesEachPairBeforeComparingIt(string pair, string expected)
    {
        string[] names = KeyRulesTests.Split(pair, " ");
        var meter = new RecordingMeter();

        _ = EqualityRules.AreEqual(Sample(names[0]), Sample(names[1]), meter, out _);

        Assert.Equal(expected, string.Join(' ', meter.Charges));
    }

    // Values nested deeper than the stack of the thread allows give InsufficientStack instead of a stack overflow.
    [Fact]
    public void DeeplyNestedValues_AreInsufficientStack()
    {
        CelValue left = Nested(100_000);
        CelValue right = Nested(100_000);
        ValueEquality result = ValueEquality.Equal;
        CelValue equals = default;

        var thread = new Thread(
            () =>
            {
                result = EqualityRules.AreEqual(left, right, null, out _);
                equals = CelValue.CelEquals(left, right);
            },
            256 * 1024);
        thread.Start();
        thread.Join();

        Assert.Equal(ValueEquality.InsufficientStack, result);
        Assert.True(equals.TryGetError(out CelError? error));
        Assert.Equal(
            (CelErrorCode.InsufficientStack, CelErrorStage.Evaluate, "insufficient execution stack to continue comparing values"),
            (error.Code, error.Stage, error.Message));
    }

    private static CelValue Nested(int depth)
    {
        CelValue value = CelValue.FromInt64(0);
        for (int level = 0; level < depth; level++)
        {
            value = CelValue.FromList(CelList.Create(value));
        }

        return value;
    }

    private static CelValue ErrorValue(string message) =>
        CelValue.FromError(new CelError(CelErrorCode.InternalError, CelErrorStage.Evaluate, message, default, null, null));

    private static CelValue List(params CelValue[] items) => CelValue.FromList(CelList.Create(items));

    private static CelValue Map(params (CelValue Key, CelValue Value)[] entries)
    {
        var pairs = new List<KeyValuePair<CelValue, CelValue>>();
        foreach ((CelValue key, CelValue value) in entries)
        {
            pairs.Add(new(key, value));
        }

        return CelValue.FromMap(CelMap.Create(pairs));
    }

    private static CelValue Sample(string name) => name switch
    {
        "int-0" => CelValue.FromInt64(0),
        "int-7" => CelValue.FromInt64(7),
        "uint-0" => CelValue.FromUInt64(0),
        "uint-3" => CelValue.FromUInt64(3),
        "uint-7" => CelValue.FromUInt64(7),
        "int-minus-3" => CelValue.FromInt64(-3),
        "double-7" => CelValue.FromDouble(7.0),
        "int-2^54+1" => CelValue.FromInt64(18014398509481985),
        "double-2^54" => CelValue.FromDouble(18014398509481984.0),
        "uint-near-2^64" => CelValue.FromUInt64(18446744073709551000),
        "double-2^64" => CelValue.FromDouble(18446744073709551616.0),
        "int-near-minus-2^63" => CelValue.FromInt64(-9223372036854775000),
        "double-minus-2^63" => CelValue.FromDouble(-9223372036854775808.0),
        "double-1e19" => CelValue.FromDouble(1e19),
        "double-nan" => CelValue.FromDouble(double.NaN),
        "double-minus-zero" => CelValue.FromDouble(-0.0),
        "string-x" => CelValue.FromString("x"),
        "string-eleven" => CelValue.FromString("elevenchars"),
        "string-decomposed" => CelValue.FromString("é"),
        "string-composed" => CelValue.FromString("é"),
        "bytes-x" => CelValue.FromBytes(new byte[] { 0x78, 1, 2, 3, 4 }),
        "bytes-x-copy" => CelValue.FromBytes(new byte[] { 0x78, 1, 2, 3, 4 }),
        "bytes-xy" => CelValue.FromBytes(new byte[] { 0x78, 1, 2, 3, 4, 5 }),
        "null" => CelValue.Null,
        "bool-true" => CelValue.FromBoolean(true),
        "bool-false" => CelValue.FromBoolean(false),
        "type-int" => CelValue.FromType(CelType.Int),
        "type-list-int" => CelValue.FromType(CelType.ListOf(CelType.Int)),
        "type-list-dyn" => CelValue.FromType(CelType.ListOf(CelType.Dyn)),
        "type-wrapper-int" => CelValue.FromType(CelType.WrapperOf(CelType.Int)),
        "type-type-int" => CelValue.FromType(CelType.TypeOf(CelType.Int)),
        "type-type-string" => CelValue.FromType(CelType.TypeOf(CelType.String)),
        "list-1-a" => List(CelValue.FromInt64(1), CelValue.FromString("a")),
        "list-1u-a" => List(CelValue.FromUInt64(1), CelValue.FromString("a")),
        "list-1" => List(CelValue.FromInt64(1)),
        "list-1-2" => List(CelValue.FromInt64(1), CelValue.FromInt64(2)),
        "list-1-2-3" => List(CelValue.FromInt64(1), CelValue.FromInt64(2), CelValue.FromInt64(3)),
        "list-1-5-3" => List(CelValue.FromInt64(1), CelValue.FromInt64(5), CelValue.FromInt64(3)),
        "list-1-abc" => List(CelValue.FromInt64(1), CelValue.FromString("abc")),
        "list-1-abd" => List(CelValue.FromInt64(1), CelValue.FromString("abd")),
        "list-empty" => List(),
        "list-nested-1" => List(List(CelValue.FromInt64(1))),
        "list-nested-1.0" => List(List(CelValue.FromDouble(1.0))),
        "map-1-a" => Map((CelValue.FromInt64(1), CelValue.FromString("a"))),
        "map-2-a" => Map((CelValue.FromInt64(2), CelValue.FromString("a"))),
        "map-1-a-b-2" => Map((CelValue.FromInt64(1), CelValue.FromString("a")), (CelValue.FromString("b"), CelValue.FromInt64(2))),
        "map-b-2.0-1u-a" => Map((CelValue.FromString("b"), CelValue.FromDouble(2.0)), (CelValue.FromUInt64(1), CelValue.FromString("a"))),
        "map-k-list-1" => Map((CelValue.FromString("k"), List(CelValue.FromInt64(1)))),
        "map-k-list-2" => Map((CelValue.FromString("k"), List(CelValue.FromInt64(2)))),
        "map-long-key-1" => Map((CelValue.FromString("twelve-chars"), CelValue.FromInt64(1))),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };
}
