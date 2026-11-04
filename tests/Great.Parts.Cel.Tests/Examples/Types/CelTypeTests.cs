using System;
using System.Linq;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Types;

// The public type model: the factories, Kind, Name and Parameters of each type, structural equality, and the
// text form that error messages show — the notation of the language definition, wrappers as wrapper(P).
public sealed class CelTypeTests
{
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "F-4")]
    public void TextForm_IsTheNotationOfTheLanguageDefinition()
    {
        (CelType Type, string Text)[] cases =
        [
            (CelType.Dyn, "dyn"),
            (CelType.Null, "null_type"),
            (CelType.Bool, "bool"),
            (CelType.Int, "int"),
            (CelType.UInt, "uint"),
            (CelType.Double, "double"),
            (CelType.String, "string"),
            (CelType.Bytes, "bytes"),
            (CelType.ListOf(CelType.Int), "list(int)"),
            (CelType.MapOf(CelType.String, CelType.ListOf(CelType.Dyn)), "map(string, list(dyn))"),
            (CelType.WrapperOf(CelType.Int), "wrapper(int)"),
            (CelType.TypeOf(CelType.Int), "type(int)"),
            (CelType.TypeOf(CelType.Dyn), "type(dyn)"),
            (CelType.TypeOf(CelType.TypeOf(CelType.Dyn)), "type(type(dyn))"),
            (CelType.TypeParameter("T"), "T"),
            (CelType.ListOf(CelType.TypeParameter("T")), "list(T)"),
            (CelType.ObjectNamed("acme.sales.Order"), "acme.sales.Order"),
        ];

        Assert.Equal(cases.Select(item => item.Text), cases.Select(item => item.Type.ToString()));
    }

    [Fact]
    [Trait("Requirement", "F-3")]
    public void EachType_HasItsKindNameAndParameters()
    {
        (CelType Type, CelTypeKind Kind, string Name, CelType[] Parameters)[] cases =
        [
            (CelType.Dyn, CelTypeKind.Dyn, "dyn", []),
            (CelType.Null, CelTypeKind.Null, "null_type", []),
            (CelType.Int, CelTypeKind.Int, "int", []),
            (CelType.Bytes, CelTypeKind.Bytes, "bytes", []),
            (CelType.ListOf(CelType.Int), CelTypeKind.List, "list", [CelType.Int]),
            (CelType.MapOf(CelType.String, CelType.Int), CelTypeKind.Map, "map", [CelType.String, CelType.Int]),
            (CelType.WrapperOf(CelType.Bool), CelTypeKind.Wrapper, "google.protobuf.BoolValue", [CelType.Bool]),
            (CelType.WrapperOf(CelType.Bytes), CelTypeKind.Wrapper, "google.protobuf.BytesValue", [CelType.Bytes]),
            (CelType.WrapperOf(CelType.Double), CelTypeKind.Wrapper, "google.protobuf.DoubleValue", [CelType.Double]),
            (CelType.WrapperOf(CelType.Int), CelTypeKind.Wrapper, "google.protobuf.Int64Value", [CelType.Int]),
            (CelType.WrapperOf(CelType.UInt), CelTypeKind.Wrapper, "google.protobuf.UInt64Value", [CelType.UInt]),
            (CelType.WrapperOf(CelType.String), CelTypeKind.Wrapper, "google.protobuf.StringValue", [CelType.String]),
            (CelType.TypeOf(CelType.Int), CelTypeKind.Type, "type", [CelType.Int]),
            (CelType.TypeParameter("T"), CelTypeKind.TypeParameter, "T", []),
            (CelType.ObjectNamed("acme.sales.Order"), CelTypeKind.Object, "acme.sales.Order", []),
        ];

        Assert.All(cases, item =>
        {
            Assert.Equal(item.Kind, item.Type.Kind);
            Assert.Equal(item.Name, item.Type.Name);
            Assert.Equal(item.Parameters, item.Type.Parameters);
        });
    }

    // Two types are equal when they have the same kind, name and parameters, however they were created.
    [Fact]
    [Trait("Requirement", "F-3")]
    public void TypesWithTheSameStructure_AreEqual()
    {
        CelType first = CelType.MapOf(CelType.String, CelType.ListOf(CelType.WrapperOf(CelType.Int)));
        CelType second = CelType.MapOf(CelType.String, CelType.ListOf(CelType.WrapperOf(CelType.Int)));

        Assert.Equal(first, second);
        Assert.True(first.Equals((object)second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal(CelType.TypeParameter("T"), CelType.TypeParameter("T"));
        Assert.NotEqual(CelType.ListOf(CelType.Int), CelType.ListOf(CelType.UInt));
        Assert.NotEqual(CelType.TypeParameter("T"), CelType.TypeParameter("U"));
        Assert.NotEqual(CelType.WrapperOf(CelType.Int), CelType.Int);
        Assert.NotEqual(CelType.TypeOf(CelType.Int), CelType.Int);
    }

    // A wrapper exists only for the six primitive types that protobuf wraps.
    [Fact]
    [Trait("Requirement", "F-3")]
    public void WrapperOfATypeThatProtobufDoesNotWrap_IsArgumentException()
    {
        CelType[] notWrapped =
        [
            CelType.Dyn,
            CelType.Null,
            CelType.ListOf(CelType.Int),
            CelType.MapOf(CelType.String, CelType.Int),
            CelType.TypeOf(CelType.Int),
            CelType.WrapperOf(CelType.Int),
            CelType.TypeParameter("T"),
        ];

        Assert.All(notWrapped, type => Assert.Throws<ArgumentException>(() => CelType.WrapperOf(type)));
    }
}
