using System.Linq;
using Great.Parts.Cel.Configuration;
using Xunit;

namespace Great.Parts.Cel.Tests.Configuration;

public sealed class TypeRecordParserTests
{
    // The outermost type of a well-formed record: its name without white space, and whether it is a type parameter or
    // has type parameters.
    [Theory]
    [InlineData("int", "int", false, false)]
    [InlineData(" \t\r\nint\n", "int", false, false)]
    [InlineData("_x1", "_x1", false, false)]
    [InlineData("acme.sales.Order", "acme.sales.Order", false, false)]
    [InlineData(".acme.Order", ".acme.Order", false, false)]
    [InlineData(" . acme . Order ", ".acme.Order", false, false)]
    [InlineData("list<int>", "list", false, true)]
    [InlineData("map < string , dyn >", "map", false, true)]
    [InlineData("map<string, list<map<int, ~V>>>", "map", false, true)]
    [InlineData("type<list<~A>, ~B, acme.Order>", "type", false, true)]
    [InlineData("~T", "T", true, false)]
    [InlineData(" ~ z ", "z", true, false)]
    public static void WellFormedRecord_GivesItsOutermostType(string text, string name, bool isTypeParameter, bool hasParameters)
    {
        TypeRecord? record = TypeRecordParser.Parse(text);

        Assert.Equal(new TypeRecord(name, isTypeParameter, hasParameters), record);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1int")]
    [InlineData("in-t")]
    [InlineData("int int")]
    [InlineData("int\u00a0")]
    [InlineData("\u00e9")]
    [InlineData(".")]
    [InlineData("a.")]
    [InlineData("a..b")]
    [InlineData("<int>")]
    [InlineData("list<")]
    [InlineData("list<>")]
    [InlineData("list<int")]
    [InlineData("list<int,>")]
    [InlineData("list<,int>")]
    [InlineData("list<int>>")]
    [InlineData("list<int>,int")]
    [InlineData("map<string int>")]
    [InlineData("list<int>x")]
    [InlineData("int>")]
    [InlineData("~")]
    [InlineData("~ ")]
    [InlineData("~1")]
    [InlineData("~_")]
    [InlineData("~TT")]
    [InlineData("~T<int>")]
    [InlineData("list<~TU>")]
    public static void TextOutsideTheGrammar_IsNotARecord(string text)
    {
        Assert.Null(TypeRecordParser.Parse(text));
    }

    [Fact]
    public static void DeepNesting_IsParsedWithoutRecursion()
    {
        const int depth = 100_000;
        string text = string.Concat(Enumerable.Repeat("list<", depth)) + "int" + new string('>', depth);

        Assert.Equal(new TypeRecord("list", false, true), TypeRecordParser.Parse(text));
        Assert.Null(TypeRecordParser.Parse(text[..^1]));
    }

    [Theory]
    [InlineData("int", true)]
    [InlineData("_1", true)]
    [InlineData("acme.sales.Order", true)]
    [InlineData(".acme.Order", true)]
    [InlineData("", false)]
    [InlineData(" int", false)]
    [InlineData("int ", false)]
    [InlineData("a .b", false)]
    [InlineData("a\t.b", false)]
    [InlineData("a.\nb", false)]
    [InlineData("a.\rb", false)]
    [InlineData("a.", false)]
    [InlineData("1a", false)]
    [InlineData("list<int>", false)]
    [InlineData("~T", false)]
    public static void TypeName_IsANameWithoutWhiteSpace(string text, bool isTypeName)
    {
        Assert.Equal(isTypeName, TypeRecordParser.IsTypeName(text));
    }
}
