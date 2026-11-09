using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// ValueTypeRules: the runtime type of a value; whether a value fits a declared type at any depth, what the check
// charges, and the actual type that the messages of a mismatch name.
public sealed class ValueTypeRulesTests
{
    private static readonly CelType A = CelType.TypeParameter("A");

    [Fact]
    public void TypeOf_ErasesTheParametersOfCollectionsAndTypeValues()
    {
        (CelValue Value, CelType Type)[] cases =
        [
            (CelValue.Null, CelType.Null),
            (CelValue.FromBoolean(false), CelType.Bool),
            (CelValue.FromInt64(-4), CelType.Int),
            (CelValue.FromUInt64(4), CelType.UInt),
            (CelValue.FromDouble(0.25), CelType.Double),
            (CelValue.FromString("s"), CelType.String),
            (CelValue.FromBytes(new byte[] { 9 }), CelType.Bytes),
            (List(CelValue.FromString("s")), CelType.ListOf(CelType.Dyn)),
            (Map((CelValue.FromBoolean(true), CelValue.FromInt64(1))), CelType.MapOf(CelType.Dyn, CelType.Dyn)),
            (CelValue.FromType(CelType.ListOf(CelType.Int)), CelType.TypeOf(CelType.Dyn)),
        ];

        Assert.All(cases, item => Assert.Equal(item.Type, ValueTypeRules.TypeOf(item.Value)));
    }

    public static TheoryData<string, bool> TopLevelCases => new()
    {
        { "int against int", true },
        { "int against uint", false },
        { "int against dyn", true },
        { "int against type parameter", true },
        { "null against null_type", true },
        { "int against null_type", false },
        { "null against wrapper", true },
        { "int against wrapper", true },
        { "string against wrapper", false },
        { "null against string", false },
        { "type value against type of another type", true },
        { "int against type", false },
        { "list against list", true },
        { "map against list", false },
        { "error against dyn", false },
        { "error against int", false },
    };

    [Theory]
    [MemberData(nameof(TopLevelCases))]
    public static void Fits_AtTheTopLevel(string name, bool expected)
    {
        (CelValue value, CelType type) = TopLevel(name);

        Assert.Equal(expected, ValueTypeRules.Fits(value, type, null).IsFit);
        Assert.Equal(expected, ValueTypeRules.MismatchType(value, type) is null);
    }

    [Fact]
    public void Fits_DescendsIntoElementsKeysAndValues()
    {
        CelValue numbers = List(CelValue.FromInt64(4), CelValue.FromInt64(5));
        CelValue mixed = List(CelValue.FromInt64(4), CelValue.FromBoolean(true));
        CelValue nested = Map((CelValue.FromString("k"), List(CelValue.FromUInt64(1), CelValue.FromInt64(2))));

        Assert.True(ValueTypeRules.Fits(numbers, CelType.ListOf(CelType.Int), null).IsFit);
        Assert.False(ValueTypeRules.Fits(mixed, CelType.ListOf(CelType.Int), null).IsFit);
        Assert.True(ValueTypeRules.Fits(mixed, CelType.ListOf(CelType.Dyn), null).IsFit);
        Assert.True(ValueTypeRules.Fits(mixed, CelType.ListOf(A), null).IsFit);
        Assert.True(ValueTypeRules.Fits(nested, CelType.MapOf(CelType.String, CelType.ListOf(CelType.Dyn)), null).IsFit);
        Assert.False(ValueTypeRules.Fits(nested, CelType.MapOf(CelType.String, CelType.ListOf(CelType.UInt)), null).IsFit);
        Assert.False(ValueTypeRules.Fits(nested, CelType.MapOf(CelType.Int, CelType.Dyn), null).IsFit);
        Assert.True(ValueTypeRules.Fits(List(CelValue.FromType(CelType.Int)), CelType.ListOf(CelType.TypeOf(CelType.String)), null).IsFit);
        Assert.True(ValueTypeRules.Fits(List(CelValue.Null, CelValue.FromDouble(1)), CelType.ListOf(CelType.WrapperOf(CelType.Double)), null).IsFit);
    }

    // "<declared type> → <units>": the units charged for the list [[1, 2], [3]], or the map {'a': [1], 'b': []}.
    [Theory]
    [InlineData("list(list(int))", 5UL)]
    [InlineData("list(list(dyn))", 2UL)]
    [InlineData("list(dyn)", 0UL)]
    [InlineData("dyn", 0UL)]
    [InlineData("map(string, list(int))", 3UL)]
    [InlineData("map(string, dyn)", 2UL)]
    [InlineData("map(dyn, dyn)", 0UL)]
    public static void Fits_ChargesTheSizeOfEachCollectionItDescendsInto(string declared, ulong units)
    {
        CelType type = Declared(declared);
        CelValue value = type.Kind == CelTypeKind.Map
            ? Map((CelValue.FromString("a"), List(CelValue.FromInt64(1))), (CelValue.FromString("b"), List()))
            : List(List(CelValue.FromInt64(1), CelValue.FromInt64(2)), List(CelValue.FromInt64(3)));
        var meter = new RecordingMeter();

        Assert.True(ValueTypeRules.Fits(value, type, meter).IsFit);

        Assert.Equal(units, meter.Total);
    }

    [Fact]
    public void Fits_StopsAtTheFirstElementThatDoesNotFit()
    {
        var meter = new RecordingMeter();
        CelValue value = List(List(CelValue.FromString("x")), List(CelValue.FromInt64(1), CelValue.FromInt64(2)));

        Assert.False(ValueTypeRules.Fits(value, CelType.ListOf(CelType.ListOf(CelType.Int)), meter).IsFit);

        Assert.Equal([2UL, 1UL], meter.Charges);
    }

    public static TheoryData<string, string> MismatchCases => new()
    {
        { "string against int", "string" },
        { "list against map", "list" },
        { "null against string", "null_type" },
        { "error against dyn", "error" },
        { "bool in list against list(int)", "list(bool)" },
        { "list in list against list(int)", "list(list)" },
        { "int key against map(string, dyn)", "map(int, dyn)" },
        { "bytes value against map(dyn, string)", "map(dyn, bytes)" },
        { "deep value against map(string, list(int))", "map(string, list(double))" },
    };

    [Theory]
    [MemberData(nameof(MismatchCases))]
    public static void MismatchType_NamesThePathOfTheFirstMismatch(string name, string expected)
    {
        (CelValue value, CelType type) = Mismatch(name);

        Assert.False(ValueTypeRules.Fits(value, type, null).IsFit);
        Assert.Equal(expected, ValueTypeRules.MismatchType(value, type));
    }

    // With the admission of null, a null element of a list or value of a map at the top fits, and the mismatch type does
    // not name it; without it, null fits none of string, bytes, list, map and type, as before.
    [Fact]
    public void NullElements_FitAtTheTopWithTheAdmissionOnly()
    {
        CelType[] declared =
        [
            CelType.ListOf(CelType.String),
            CelType.ListOf(CelType.Bytes),
            CelType.ListOf(CelType.ListOf(CelType.Dyn)),
            CelType.ListOf(CelType.MapOf(CelType.Dyn, CelType.Dyn)),
            CelType.ListOf(CelType.TypeOf(CelType.Dyn)),
        ];

        foreach (CelType type in declared)
        {
            CelValue value = List(CelValue.Null);
            Assert.True(ValueTypeRules.Fits(value, type, null, nullElements: true).IsFit);
            Assert.Null(ValueTypeRules.MismatchType(value, type, nullElements: true));
            Assert.False(ValueTypeRules.Fits(value, type, null).IsFit);
            Assert.Equal("list(null_type)", ValueTypeRules.MismatchType(value, type));
        }

        CelType mapType = CelType.MapOf(CelType.String, CelType.String);
        CelValue map = Map((CelValue.FromString("b"), CelValue.Null));
        Assert.True(ValueTypeRules.Fits(map, mapType, null, nullElements: true).IsFit);
        Assert.Null(ValueTypeRules.MismatchType(map, mapType, nullElements: true));
        Assert.False(ValueTypeRules.Fits(map, mapType, null).IsFit);
        Assert.Equal("map(string, null_type)", ValueTypeRules.MismatchType(map, mapType));
    }

    // The admission is for the top only, and another element still fails: its type is named past the null.
    [Fact]
    public void NullElements_AdmitNothingDeeperAndNothingElse()
    {
        CelType nested = CelType.ListOf(CelType.ListOf(CelType.String));
        CelValue deep = List(List(CelValue.Null));
        CelValue mixed = List(CelValue.Null, CelValue.FromInt64(1));

        Assert.False(ValueTypeRules.Fits(deep, nested, null, nullElements: true).IsFit);
        Assert.Equal("list(list(null_type))", ValueTypeRules.MismatchType(deep, nested, nullElements: true));
        Assert.False(ValueTypeRules.Fits(mixed, CelType.ListOf(CelType.String), null, nullElements: true).IsFit);
        Assert.Equal("list(int)", ValueTypeRules.MismatchType(mixed, CelType.ListOf(CelType.String), nullElements: true));
    }

    private static (CelValue Value, CelType Type) TopLevel(string name) => name switch
    {
        "int against int" => (CelValue.FromInt64(1), CelType.Int),
        "int against uint" => (CelValue.FromInt64(1), CelType.UInt),
        "int against dyn" => (CelValue.FromInt64(1), CelType.Dyn),
        "int against type parameter" => (CelValue.FromInt64(1), A),
        "null against null_type" => (CelValue.Null, CelType.Null),
        "int against null_type" => (CelValue.FromInt64(0), CelType.Null),
        "null against wrapper" => (CelValue.Null, CelType.WrapperOf(CelType.Int)),
        "int against wrapper" => (CelValue.FromInt64(2), CelType.WrapperOf(CelType.Int)),
        "string against wrapper" => (CelValue.FromString("2"), CelType.WrapperOf(CelType.Int)),
        "null against string" => (CelValue.Null, CelType.String),
        "type value against type of another type" => (CelValue.FromType(CelType.Bool), CelType.TypeOf(CelType.Int)),
        "int against type" => (CelValue.FromInt64(1), CelType.TypeOf(CelType.Dyn)),
        "list against list" => (List(), CelType.ListOf(CelType.Int)),
        "map against list" => (Map(), CelType.ListOf(CelType.Int)),
        "error against dyn" => (ErrorValue(), CelType.Dyn),
        _ => (ErrorValue(), CelType.Int),
    };

    private static (CelValue Value, CelType Type) Mismatch(string name) => name switch
    {
        "string against int" => (CelValue.FromString("1"), CelType.Int),
        "list against map" => (List(), CelType.MapOf(CelType.Dyn, CelType.Dyn)),
        "null against string" => (CelValue.Null, CelType.String),
        "error against dyn" => (ErrorValue(), CelType.Dyn),
        "bool in list against list(int)" => (List(CelValue.FromInt64(1), CelValue.FromBoolean(false), CelValue.FromString("z")), CelType.ListOf(CelType.Int)),
        "list in list against list(int)" => (List(List()), CelType.ListOf(CelType.Int)),
        "int key against map(string, dyn)" => (Map((CelValue.FromString("a"), CelValue.Null), (CelValue.FromInt64(3), CelValue.Null)), CelType.MapOf(CelType.String, CelType.Dyn)),
        "bytes value against map(dyn, string)" => (Map((CelValue.FromInt64(3), CelValue.FromBytes(new byte[] { 1 }))), CelType.MapOf(CelType.Dyn, CelType.String)),
        _ => (Map((CelValue.FromString("a"), List(CelValue.FromInt64(1), CelValue.FromDouble(2)))), CelType.MapOf(CelType.String, CelType.ListOf(CelType.Int))),
    };

    private static CelType Declared(string text) => text switch
    {
        "list(list(int))" => CelType.ListOf(CelType.ListOf(CelType.Int)),
        "list(list(dyn))" => CelType.ListOf(CelType.ListOf(CelType.Dyn)),
        "list(dyn)" => CelType.ListOf(CelType.Dyn),
        "dyn" => CelType.Dyn,
        "map(string, list(int))" => CelType.MapOf(CelType.String, CelType.ListOf(CelType.Int)),
        "map(string, dyn)" => CelType.MapOf(CelType.String, CelType.Dyn),
        _ => CelType.MapOf(CelType.Dyn, CelType.Dyn),
    };

    private static CelValue ErrorValue() =>
        CelValue.FromError(new CelError(CelErrorCode.InternalError, CelErrorStage.Evaluate, "failed", default, null, null));

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
}
