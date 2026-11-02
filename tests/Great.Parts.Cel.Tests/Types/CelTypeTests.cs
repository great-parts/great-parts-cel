using System;
using System.Collections.Immutable;
using System.Linq;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Types;

public sealed class CelTypeTests
{
    [Fact]
    public void SimpleTypes_HaveTheirKindsAndCelNames()
    {
        CelType[] types = [CelType.Int, CelType.UInt, CelType.Double, CelType.Bool, CelType.String, CelType.Bytes, CelType.Dyn];

        Assert.Equal(
            [
                (CelTypeKind.Int, "int"),
                (CelTypeKind.UInt, "uint"),
                (CelTypeKind.Double, "double"),
                (CelTypeKind.Bool, "bool"),
                (CelTypeKind.String, "string"),
                (CelTypeKind.Bytes, "bytes"),
                (CelTypeKind.Dyn, "dyn"),
            ],
            types.Select(type => (type.Kind, type.Name)));
        Assert.All(types, type => Assert.Empty(type.Parameters));
    }

    [Fact]
    public void SimpleType_IsASharedInstance()
    {
        Assert.Same(CelType.Int, CelType.Int);
        Assert.Same(CelType.Dyn, CelType.Dyn);
    }

    [Fact]
    public void TypesWithEqualKindNameAndParameters_AreEqual()
    {
        CelType left = new(CelTypeKind.Map, "map", [CelType.String, new CelType(CelTypeKind.List, "list", [CelType.Int])]);
        CelType right = new(CelTypeKind.Map, "map", [new CelType(CelTypeKind.String, "string", []), new CelType(CelTypeKind.List, "list", [CelType.Int])]);

        Assert.True(left.Equals(right));
        Assert.True(left.Equals((object)right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void SimpleType_EqualsItsSeparatelyCreatedCopy()
    {
        CelType copy = new(CelTypeKind.Int, "int", []);

        Assert.True(CelType.Int.Equals(copy));
        Assert.Equal(CelType.Int.GetHashCode(), copy.GetHashCode());
    }

    [Fact]
    public void TypesThatDifferInKind_AreNotEqual()
    {
        Assert.False(new CelType(CelTypeKind.Object, "int", []).Equals(CelType.Int));
    }

    [Fact]
    public void TypesThatDifferInName_AreNotEqual()
    {
        Assert.False(new CelType(CelTypeKind.Object, "acme.Order", []).Equals(new CelType(CelTypeKind.Object, "acme.order", [])));
    }

    [Fact]
    public void TypesThatDifferInAParameter_AreNotEqual()
    {
        CelType listOfInt = new(CelTypeKind.List, "list", [CelType.Int]);

        Assert.False(listOfInt.Equals(new CelType(CelTypeKind.List, "list", [CelType.UInt])));
    }

    [Fact]
    public void TypesThatDifferInTheNumberOfParameters_AreNotEqual()
    {
        CelType listOfInt = new(CelTypeKind.List, "list", [CelType.Int]);

        Assert.False(listOfInt.Equals(new CelType(CelTypeKind.List, "list", ImmutableArray<CelType>.Empty)));
        Assert.False(listOfInt.Equals(new CelType(CelTypeKind.List, "list", [CelType.Int, CelType.Int])));
    }

    [Fact]
    public void DifferentSimpleTypes_AreNotEqual()
    {
        Assert.False(CelType.Int.Equals(CelType.UInt));
        Assert.False(CelType.String.Equals((object)CelType.Bytes));
    }

    [Fact]
    public void Equals_NullOrOtherObject_IsFalse()
    {
        Assert.False(CelType.Int.Equals(null));
        Assert.False(CelType.Int.Equals((object?)null));
        Assert.False(CelType.Int.Equals("int"));
    }

    [Fact]
    public void Null_IsASharedInstanceOfNullType()
    {
        Assert.Same(CelType.Null, CelType.Null);
        Assert.Equal((CelTypeKind.Null, "null_type"), (CelType.Null.Kind, CelType.Null.Name));
        Assert.Empty(CelType.Null.Parameters);
    }

    [Fact]
    public void ListOf_HasTheElementTypeAsItsParameter()
    {
        CelType list = CelType.ListOf(CelType.String);

        Assert.Equal((CelTypeKind.List, "list"), (list.Kind, list.Name));
        Assert.Equal([CelType.String], list.Parameters);
    }

    [Fact]
    public void MapOf_HasTheKeyAndValueTypesAsItsParametersInThisOrder()
    {
        CelType map = CelType.MapOf(CelType.Int, CelType.Bool);

        Assert.Equal((CelTypeKind.Map, "map"), (map.Kind, map.Name));
        Assert.Equal([CelType.Int, CelType.Bool], map.Parameters);
    }

    [Fact]
    public void ListOfAndMapOf_OfEqualParameters_AreEqual()
    {
        Assert.Equal(CelType.ListOf(CelType.ListOf(CelType.Int)), CelType.ListOf(CelType.ListOf(CelType.Int)));
        Assert.Equal(CelType.MapOf(CelType.String, CelType.Dyn), CelType.MapOf(CelType.String, CelType.Dyn));
        Assert.NotEqual(CelType.MapOf(CelType.String, CelType.Int), CelType.MapOf(CelType.Int, CelType.String));
    }

    [Fact]
    public void ListOf_NullElementType_Throws()
    {
        Assert.Equal("elementType", Assert.Throws<ArgumentNullException>(() => CelType.ListOf(null!)).ParamName);
    }

    [Fact]
    public void MapOf_NullKeyOrValueType_Throws()
    {
        Assert.Equal("keyType", Assert.Throws<ArgumentNullException>(() => CelType.MapOf(null!, CelType.Int)).ParamName);
        Assert.Equal("valueType", Assert.Throws<ArgumentNullException>(() => CelType.MapOf(CelType.Int, null!)).ParamName);
    }

    [Fact]
    public void ToString_WritesTheTypeInCelNotationWithItsParameters()
    {
        CelType[] types =
        [
            CelType.Int,
            CelType.Null,
            CelType.Dyn,
            CelType.ListOf(CelType.Int),
            CelType.MapOf(CelType.String, CelType.Dyn),
            CelType.ListOf(CelType.MapOf(CelType.UInt, CelType.ListOf(CelType.Bytes))),
        ];

        Assert.Equal(
            ["int", "null_type", "dyn", "list(int)", "map(string, dyn)", "list(map(uint, list(bytes)))"],
            types.Select(type => type.ToString()));
    }
}
