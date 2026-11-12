using Great.Parts.Cel.Configuration;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Configuration;

// optional_type<E> in the type records of a description with a type parameter inside it, which the examples of the records do not
// take.
public sealed class OptionalTypeRecordTests
{
    public static TheoryData<string, CelType> WithTypeParameters => new()
    {
        { "optional_type<~A>", CelType.OptionalOf(CelType.TypeParameter("A")) },
        { "list<optional_type<~A>>", CelType.ListOf(CelType.OptionalOf(CelType.TypeParameter("A"))) },
        { "map<string, optional_type<~A>>", CelType.MapOf(CelType.String, CelType.OptionalOf(CelType.TypeParameter("A"))) },
        { "optional_type<optional_type<~B>>", CelType.OptionalOf(CelType.OptionalOf(CelType.TypeParameter("B"))) },
    };

    [Theory]
    [MemberData(nameof(WithTypeParameters))]
    public static void OptionalType_WithATypeParameterInside_IsResolvedWhereTypeParametersAreAllowed(string text, CelType expected)
    {
        CelType? type = TypeRecordResolver.Resolve(text, allowTypeParameters: true, "an argument", out _);

        Assert.Equal(expected, type);
    }

    [Fact]
    public static void OptionalType_WithATypeParameterInside_IsRefusedWhereTheyAreNot()
    {
        Assert.Null(TypeRecordResolver.Resolve("optional_type<~A>", allowTypeParameters: false, "a variable", out string detail));

        Assert.Equal("a type parameter cannot be used in a variable: 'optional_type<~A>'", detail);
    }
}
