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
}
