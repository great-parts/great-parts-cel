using System.Linq;
using Great.Parts.Cel.Configuration;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Configuration;

public sealed class TypeRecordResolverTests
{
    public static TheoryData<string, CelType> Records => new()
    {
        { "int", CelType.Int },
        { " \t\r\nuint\n", CelType.UInt },
        { "double", CelType.Double },
        { "bool", CelType.Bool },
        { "string", CelType.String },
        { "bytes", CelType.Bytes },
        { "dyn", CelType.Dyn },
        { "null_type", CelType.Null },
        { "null", CelType.Null },
        { "timestamp", CelType.Timestamp },
        { "duration", CelType.Duration },
        { "list<timestamp>", CelType.ListOf(CelType.Timestamp) },
        { "map<string, duration>", CelType.MapOf(CelType.String, CelType.Duration) },
        { "type<timestamp>", CelType.TypeOf(CelType.Timestamp) },
        { "google.protobuf.Timestamp", CelType.ObjectNamed("google.protobuf.Timestamp") },
        { ".google.protobuf.Duration", CelType.ObjectNamed("google.protobuf.Duration") },
        { "int_wrapper", CelType.WrapperOf(CelType.Int) },
        { "uint_wrapper", CelType.WrapperOf(CelType.UInt) },
        { "double_wrapper", CelType.WrapperOf(CelType.Double) },
        { "bool_wrapper", CelType.WrapperOf(CelType.Bool) },
        { "string_wrapper", CelType.WrapperOf(CelType.String) },
        { "bytes_wrapper", CelType.WrapperOf(CelType.Bytes) },
        { "list<int>", CelType.ListOf(CelType.Int) },
        { "map<string, dyn>", CelType.MapOf(CelType.String, CelType.Dyn) },
        { "map < string , list< map<int, bool> > >", CelType.MapOf(CelType.String, CelType.ListOf(CelType.MapOf(CelType.Int, CelType.Bool))) },
        { "type", CelType.TypeOf(CelType.Dyn) },
        { "type<int>", CelType.TypeOf(CelType.Int) },
        { "type<list<string>>", CelType.TypeOf(CelType.ListOf(CelType.String)) },
        { "any", CelType.Any },
        { "google.protobuf.Any", CelType.Any },
        { "acme.sales.Order", CelType.ObjectNamed("acme.sales.Order") },
        { ".acme.sales.Order", CelType.ObjectNamed("acme.sales.Order") },
        { " . acme . Order ", CelType.ObjectNamed("acme.Order") },
        { "list<acme.Order>", CelType.ListOf(CelType.ObjectNamed("acme.Order")) },
        { "list<list<list<int>>>", CelType.ListOf(CelType.ListOf(CelType.ListOf(CelType.Int))) },
    };

    [Theory]
    [MemberData(nameof(Records))]
    public static void Record_IsItsType(string text, CelType expected)
    {
        CelType? type = TypeRecordResolver.Resolve(text, allowTypeParameters: false, "a variable", out string detail);

        Assert.Equal(expected, type);
        Assert.Equal(string.Empty, detail);
    }

    public static TheoryData<string, CelType> TypesWithParameters => new()
    {
        { "~T", CelType.TypeParameter("T") },
        { " ~ z ", CelType.TypeParameter("z") },
        { "list<~A>", CelType.ListOf(CelType.TypeParameter("A")) },
        { "map<~k, list<~V>>", CelType.MapOf(CelType.TypeParameter("k"), CelType.ListOf(CelType.TypeParameter("V"))) },
        { "type<~A>", CelType.TypeOf(CelType.TypeParameter("A")) },
    };

    [Theory]
    [MemberData(nameof(TypesWithParameters))]
    public static void TypeParameter_IsRightWhereAnOverloadTakesIt(string text, CelType expected) =>
        Assert.Equal(expected, TypeRecordResolver.Resolve(text, allowTypeParameters: true, "an overload", out _));

    [Theory]
    [InlineData("~T", "a type parameter cannot be used in a variable: '~T'")]
    [InlineData("list<~T>", "a type parameter cannot be used in a variable: 'list<~T>'")]
    [InlineData("map<string, type<~T>>", "a type parameter cannot be used in a variable: 'map<string, type<~T>>'")]
    public static void TypeParameter_IsAnErrorOutsideAnOverload(string text, string expected)
    {
        Assert.Null(TypeRecordResolver.Resolve(text, allowTypeParameters: false, "a variable", out string detail));

        Assert.Equal(expected, detail);
    }

    [Theory]
    [InlineData("list<", "'list<' is not a valid type record")]
    [InlineData("list<>", "'list<>' is not a valid type record")]
    [InlineData("1int", "'1int' is not a valid type record")]
    [InlineData("int int", "'int int' is not a valid type record")]
    [InlineData("map<string int>", "'map<string int>' is not a valid type record")]
    [InlineData("~", "'~' is not a valid type record")]
    [InlineData("", "'' is not a valid type record")]
    public static void RecordThatBreaksTheGrammar_IsAnError(string text, string expected)
    {
        Assert.Null(TypeRecordResolver.Resolve(text, allowTypeParameters: true, "an overload", out string detail));

        Assert.Equal(expected, detail);
    }

    [Theory]
    [InlineData("list", "the type 'list' takes 1 type parameter: 'list'")]
    [InlineData("map", "the type 'map' takes 2 type parameters: 'map'")]
    [InlineData("map<int>", "the type 'map' takes 2 type parameters, but 'map<int>' gives 1")]
    [InlineData("map<int, int, int>", "the type 'map' takes 2 type parameters, but 'map<int, int, int>' gives 3")]
    [InlineData("list<int, int>", "the type 'list' takes 1 type parameter, but 'list<int, int>' gives 2")]
    [InlineData("type<int, int>", "the type 'type' takes 1 type parameter, but 'type<int, int>' gives 2")]
    [InlineData("int<int>", "the type 'int' has no type parameters: 'int<int>'")]
    [InlineData("dyn<int>", "the type 'dyn' has no type parameters: 'dyn<int>'")]
    [InlineData("int_wrapper<int>", "the type 'int_wrapper' has no type parameters: 'int_wrapper<int>'")]
    [InlineData("timestamp<int>", "the type 'timestamp' has no type parameters: 'timestamp<int>'")]
    [InlineData("duration<int>", "the type 'duration' has no type parameters: 'duration<int>'")]
    [InlineData("acme.Order<int>", "the type 'acme.Order' has no type parameters: 'acme.Order<int>'")]
    [InlineData("list<map<int>>", "the type 'map' takes 2 type parameters, but 'list<map<int>>' gives 1")]
    public static void WrongNumberOfParameters_IsAnError(string text, string expected)
    {
        Assert.Null(TypeRecordResolver.Resolve(text, allowTypeParameters: false, "a variable", out string detail));

        Assert.Equal(expected, detail);
    }

    // The optional type has exactly one parameter, at any depth.
    [Theory]
    [InlineData("optional_type", "the type 'optional_type' takes 1 type parameter: 'optional_type'")]
    [InlineData("optional_type<int, string>", "the type 'optional_type' takes 1 type parameter, but 'optional_type<int, string>' gives 2")]
    [InlineData("list<optional_type<int, int>>", "the type 'optional_type' takes 1 type parameter, but 'list<optional_type<int, int>>' gives 2")]
    public static void OptionalTypeWithAnotherNumberOfParameters_IsAnError(string text, string expected)
    {
        Assert.Null(TypeRecordResolver.Resolve(text, allowTypeParameters: false, "a variable", out string detail));

        Assert.Equal(expected, detail);
    }

    // The nesting has no limit but the length of the text: the record is resolved without recursion.
    [Fact]
    public static void DeeplyNestedRecord_IsResolved()
    {
        const int Depth = 100_000;
        string text = string.Concat(Enumerable.Repeat("list<", Depth)) + "int" + new string('>', Depth);

        CelType? type = TypeRecordResolver.Resolve(text, allowTypeParameters: false, "a variable", out _);

        Assert.NotNull(type);
        Assert.Equal(CelTypeKind.List, type.Kind);
    }
}
