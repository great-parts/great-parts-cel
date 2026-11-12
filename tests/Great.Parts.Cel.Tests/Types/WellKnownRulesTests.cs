using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Types;

// WellKnownRules: the thirteen full names of protobuf that stand for a CEL type other than an object type, and CelType.ObjectNamed,
// which gives that type for them and the object type of the name for every other name.
public sealed class WellKnownRulesTests
{
    public static TheoryData<string, CelType, string> Table => new()
    {
        { "google.protobuf.Any", CelType.Any, "any" },
        { "google.protobuf.Value", CelType.Dyn, "dyn" },
        { "google.protobuf.Struct", CelType.MapOf(CelType.String, CelType.Dyn), "map(string, dyn)" },
        { "google.protobuf.ListValue", CelType.ListOf(CelType.Dyn), "list(dyn)" },
        { "google.protobuf.BoolValue", CelType.WrapperOf(CelType.Bool), "wrapper(bool)" },
        { "google.protobuf.BytesValue", CelType.WrapperOf(CelType.Bytes), "wrapper(bytes)" },
        { "google.protobuf.DoubleValue", CelType.WrapperOf(CelType.Double), "wrapper(double)" },
        { "google.protobuf.FloatValue", CelType.WrapperOf(CelType.Double), "wrapper(double)" },
        { "google.protobuf.Int32Value", CelType.WrapperOf(CelType.Int), "wrapper(int)" },
        { "google.protobuf.Int64Value", CelType.WrapperOf(CelType.Int), "wrapper(int)" },
        { "google.protobuf.UInt32Value", CelType.WrapperOf(CelType.UInt), "wrapper(uint)" },
        { "google.protobuf.UInt64Value", CelType.WrapperOf(CelType.UInt), "wrapper(uint)" },
        { "google.protobuf.StringValue", CelType.WrapperOf(CelType.String), "wrapper(string)" },
    };

    [Theory]
    [MemberData(nameof(Table))]
    public static void ObjectNamed_OfAWellKnownName_IsTheTypeThatProtobufMapsItTo(string name, CelType expected, string text)
    {
        CelType type = CelType.ObjectNamed(name);

        Assert.Equal(expected, type);
        Assert.Equal(text, type.ToString());
        Assert.Equal(expected, WellKnownRules.TypeOf(name));
    }

    [Theory]
    [InlineData("google.protobuf.Timestamp")]
    [InlineData("google.protobuf.Duration")]
    [InlineData("google.protobuf.Empty")]
    [InlineData("google.protobuf.FieldMask")]
    [InlineData("google.protobuf.int32value")]
    [InlineData("google.protobuf.Int32Value.Extra")]
    [InlineData("acme.Value")]
    [InlineData("Value")]
    [InlineData("google.protobuf")]
    public static void ObjectNamed_OfAnyOtherName_IsTheObjectTypeOfThatName(string name)
    {
        CelType type = CelType.ObjectNamed(name);

        Assert.Null(WellKnownRules.TypeOf(name));
        Assert.Equal((CelTypeKind.Object, name), (type.Kind, type.Name));
        Assert.Empty(type.Parameters);
    }

    // The two names are type identifiers of the standard library: the types timestamp and duration come from their properties.
    [Fact]
    public static void TimestampAndDuration_AreNotAmongTheNamesOfTheTable()
    {
        Assert.NotEqual(CelTypeKind.Timestamp, CelType.ObjectNamed("google.protobuf.Timestamp").Kind);
        Assert.NotEqual(CelTypeKind.Duration, CelType.ObjectNamed("google.protobuf.Duration").Kind);
        Assert.NotEqual(CelType.Timestamp, CelType.ObjectNamed(CelType.Timestamp.Name));
        Assert.NotEqual(CelType.Duration, CelType.ObjectNamed(CelType.Duration.Name));
    }

    // Names that map to one type give equal types, so a description of one name and a declaration by another agree; the wrappers
    // of different primitive types differ.
    [Fact]
    public static void ObjectNamed_OfTheSameWellKnownNameTwice_GivesEqualTypes()
    {
        Assert.Equal(CelType.ObjectNamed("google.protobuf.Struct"), CelType.ObjectNamed("google.protobuf.Struct"));
        Assert.Equal(CelType.ObjectNamed("google.protobuf.Int32Value"), CelType.ObjectNamed("google.protobuf.Int64Value"));
        Assert.NotEqual(CelType.ObjectNamed("google.protobuf.Int32Value"), CelType.ObjectNamed("google.protobuf.UInt32Value"));
    }
}
