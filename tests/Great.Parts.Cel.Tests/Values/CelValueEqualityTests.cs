using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// .NET equality of CelValue: the same kind and the same content; lists, maps and errors by reference.
public sealed class CelValueEqualityTests
{
    private static readonly CelList SharedList = CelList.Create(CelValue.FromInt64(1));
    private static readonly CelMap SharedMap = CelMap.Create([new(CelValue.FromInt64(1), CelValue.FromInt64(2))]);
    private static readonly CelError SharedError = Error();
    private static readonly ArrayMemoryManager Manager = new([9, 1, 2]);

    // "<left> <right> → equal | different". Every sample is a new value, so equal content in two samples is not one instance.
    private const string Pairs = """
        null default → equal
        null false → different
        false false → equal
        false true → different
        int-1 int-1 → equal
        int-1 uint-1 → different
        int-1 double-1 → different
        uint-1 uint-1 → equal
        double-1 double-1 → equal
        double-zero double-negative-zero → equal
        double-nan double-nan → equal
        double-nan double-other-nan → equal
        double-nan double-1 → different
        string-a string-a → equal
        string-a string-upper-a → different
        string-composed string-decomposed → different
        string-empty bytes-empty → different
        bytes-array bytes-other-array → equal
        bytes-array bytes-manager → equal
        bytes-array bytes-other-content → different
        bytes-empty bytes-default → equal
        list-shared list-shared → equal
        list-new list-new → different
        map-shared map-shared → equal
        map-new map-new → different
        error-shared error-shared → equal
        error-new error-new → different
        list-new map-new → different
        """;

    public static TheoryData<string, string> PairCases => CaseTable.Parse(Pairs);

    [Theory]
    [MemberData(nameof(PairCases))]
    public static void Pair_IsEqualByKindAndContent(string pair, string expected)
    {
        string[] names = KeyRulesTests.Split(pair, " ");
        CelValue left = Sample(names[0]);
        CelValue right = Sample(names[1]);
        bool equal = expected == "equal";

        Assert.Equal(equal, left.Equals(right));
        Assert.Equal(equal, right.Equals(left));
        Assert.Equal(equal, left.Equals((object)right));
        Assert.Equal(equal, left == right);
        Assert.Equal(!equal, left != right);
        if (equal)
        {
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }
    }

    [Fact]
    public void EqualsObject_OtherThanAValue_IsFalse()
    {
        CelValue one = CelValue.FromInt64(1);

        Assert.False(one.Equals(null));
        Assert.False(one.Equals(1L));
    }

    private static CelValue Sample(string name) => name switch
    {
        "null" => CelValue.Null,
        "default" => default,
        "false" => CelValue.FromBoolean(false),
        "true" => CelValue.FromBoolean(true),
        "int-1" => CelValue.FromInt64(1),
        "uint-1" => CelValue.FromUInt64(1),
        "double-1" => CelValue.FromDouble(1.0),
        "double-zero" => CelValue.FromDouble(0.0),
        "double-negative-zero" => CelValue.FromDouble(-0.0),
        "double-nan" => CelValue.FromDouble(double.NaN),
        "double-other-nan" => CelValue.FromDouble(BitConverter.UInt64BitsToDouble(0x7FF8000000000001)),
        "string-a" => CelValue.FromString(new string('a', 1)),
        "string-upper-a" => CelValue.FromString("A"),
        "string-composed" => CelValue.FromString("\u00e9"),
        "string-decomposed" => CelValue.FromString("e\u0301"),
        "string-empty" => CelValue.FromString(""),
        "bytes-array" => CelValue.FromBytes(new byte[] { 1, 2 }),
        "bytes-other-array" => CelValue.FromBytes(new byte[] { 0, 1, 2, 3 }.AsMemory(1, 2)),
        "bytes-manager" => CelValue.FromBytes(Manager.Memory[1..]),
        "bytes-other-content" => CelValue.FromBytes(new byte[] { 1, 3 }),
        "bytes-empty" => CelValue.FromBytes(Array.Empty<byte>()),
        "bytes-default" => CelValue.FromBytes(default),
        "list-shared" => CelValue.FromList(SharedList),
        "list-new" => CelValue.FromList(CelList.Create(CelValue.FromInt64(1))),
        "map-shared" => CelValue.FromMap(SharedMap),
        "map-new" => CelValue.FromMap(CelMap.Create([new(CelValue.FromInt64(1), CelValue.FromInt64(2))])),
        "error-shared" => CelValue.FromError(SharedError),
        "error-new" => CelValue.FromError(Error()),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    private static CelError Error() =>
        new(CelErrorCode.InternalError, CelErrorStage.Evaluate, "failed", default, null, null);
}
