using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Configuration;

// The record of a type in the environment description in JSON (the page of the description, "Type records"):
// the grammar of cel-go, a name optionally with parameters in angle brackets, or ~ and one letter, with white
// space between the lexemes. A record names its type exactly: a name that is none of the listed names is an
// object type of a source, resolved at Build. A type parameter stands only in a record of an overload. In the
// marked documents each '|' is the place of one expected error (see JsonExample); a record that is wrong is
// reported at its value.
[Trait("Requirement", "F-7")]
[Trait("Requirement", "F-2")]
public sealed class TypeRecordTests
{
    // Every row of the table of records.
    [Fact]
    public void EveryRecord_IsItsType()
    {
        var cases = new (string Record, CelType Type)[]
        {
            ("int", CelType.Int),
            ("uint", CelType.UInt),
            ("double", CelType.Double),
            ("bool", CelType.Bool),
            ("string", CelType.String),
            ("bytes", CelType.Bytes),
            ("dyn", CelType.Dyn),
            ("null_type", CelType.Null),
            ("null", CelType.Null),
            ("int_wrapper", CelType.WrapperOf(CelType.Int)),
            ("uint_wrapper", CelType.WrapperOf(CelType.UInt)),
            ("double_wrapper", CelType.WrapperOf(CelType.Double)),
            ("bool_wrapper", CelType.WrapperOf(CelType.Bool)),
            ("string_wrapper", CelType.WrapperOf(CelType.String)),
            ("bytes_wrapper", CelType.WrapperOf(CelType.Bytes)),
            ("list<int>", CelType.ListOf(CelType.Int)),
            ("list<list<string>>", CelType.ListOf(CelType.ListOf(CelType.String))),
            ("map<string, int>", CelType.MapOf(CelType.String, CelType.Int)),
            ("map<int, map<string, dyn>>", CelType.MapOf(CelType.Int, CelType.MapOf(CelType.String, CelType.Dyn))),
            ("type", CelType.TypeOf(CelType.Dyn)),
            ("type<int>", CelType.TypeOf(CelType.Int)),
            ("type<list<int>>", CelType.TypeOf(CelType.ListOf(CelType.Int))),
            ("acme.crm.Deal", CelType.ObjectNamed("acme.crm.Deal")),
            (".acme.crm.Deal", CelType.ObjectNamed("acme.crm.Deal")),
            ("Deal", CelType.ObjectNamed("Deal")),
            ("list<acme.crm.Deal>", CelType.ListOf(CelType.ObjectNamed("acme.crm.Deal"))),
        };

        foreach ((string record, CelType type) in cases)
        {
            var builder = JsonExample.NewBuilder();

            Assert.Empty(JsonExample.Apply(builder, "{\"variables\": [{\"name\": \"x\", \"type\": \"" + record + "\"}]}"));

            Assert.Equal(new[] { ("x", type) }, JsonExample.Variables(builder));
        }
    }

    // White space between the lexemes is a space, a tab, a line feed and a carriage return, anywhere between two
    // lexemes and at both ends of the record.
    [Fact]
    public void WhiteSpaceBetweenLexemes_IsAllowed()
    {
        var builder = JsonExample.NewBuilder();

        Assert.Empty(JsonExample.Apply(
            builder,
            "{\"variables\": [{\"name\": \"x\", \"type\": \" map <\\tstring\\n,\\r list < int >\\t> \"}]}"));

        Assert.Equal(
            new[] { ("x", CelType.MapOf(CelType.String, CelType.ListOf(CelType.Int))) },
            JsonExample.Variables(builder));
    }

    // In an overload a type parameter is a letter after ~, capital or small, with any depth of nesting.
    [Fact]
    public void TypeParameter_IsALetterAfterATilde_InAnOverload()
    {
        var builder = JsonExample.NewBuilder();

        Assert.Empty(JsonExample.Apply(
            builder,
            """
            {"functions": [{"name": "f", "overloads": [
              {"id": "f_a", "args": ["~A", "list<~B>", "map<~a, list<~z>>"], "return": "~Z"}
            ]}]}
            """));

        CelEnvironmentBuilder inCode = JsonExample.NewBuilder().AddFunction(new CelFunctionDecl(
            "f",
            CelOverloadDecl.Global(
                "f_a",
                [
                    CelType.TypeParameter("A"),
                    CelType.ListOf(CelType.TypeParameter("B")),
                    CelType.MapOf(CelType.TypeParameter("a"), CelType.ListOf(CelType.TypeParameter("z"))),
                ],
                CelType.TypeParameter("Z"))));
        Assert.Equal(JsonExample.Functions(inCode), JsonExample.Functions(builder));
    }

    // A record that is wrong, whatever the way: an empty record, an unclosed or unopened bracket, a missing or
    // an extra parameter, a trailing comma, characters after the type, a name that is not a name.
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("list<")]
    [InlineData("list<int")]
    [InlineData("list<int>>")]
    [InlineData("list<>")]
    [InlineData("list<int,>")]
    [InlineData("list<,int>")]
    [InlineData("map<string,>")]
    [InlineData("int int")]
    [InlineData("list<int>x")]
    [InlineData("<int>")]
    [InlineData("a..b")]
    [InlineData("a.")]
    [InlineData("1int")]
    [InlineData("a-b")]
    [InlineData("a b")]
    [InlineData("~")]
    [InlineData("~AB")]
    [InlineData("~1")]
    [InlineData("~_")]
    [InlineData("int,")]
    [InlineData("list<int>,")]
    public void RecordThatTheGrammarDoesNotAccept_IsAnErrorAtTheValue(string record) =>
        JsonExample.AssertError("{\"variables\": [{\"name\": \"x\", \"type\": |\"" + record + "\"}]}");

    // The number of parameters is exact for list and map, 0 or 1 for type; every other name
    // takes none, wrapper types and the simple types included.
    [Theory]
    [InlineData("list")]
    [InlineData("list<int, int>")]
    [InlineData("map")]
    [InlineData("map<string>")]
    [InlineData("map<string, int, int>")]
    [InlineData("type<int, int>")]
    [InlineData("int<string>")]
    [InlineData("string<int>")]
    [InlineData("dyn<int>")]
    [InlineData("null_type<int>")]
    [InlineData("int_wrapper<int>")]
    [InlineData("acme.crm.Deal<int>")]
    [InlineData("any<int>")]
    public void WrongNumberOfParameters_IsAnErrorAtTheValue(string record) =>
        JsonExample.AssertError("{\"variables\": [{\"name\": \"x\", \"type\": |\"" + record + "\"}]}");

    // The same records are wrong in the record of an overload and of a field, at the value.
    [Theory]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "args": [|"list<int, int>"], "return": "int"}]}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "target": |"map<string>", "return": "int"}]}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": |"~AB"}]}]}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": "x", "type": |"list"}]}]}}""")]
    public void WrongRecord_IsAnErrorAtTheValueWhereverARecordStands(string marked) =>
        JsonExample.AssertError(marked);

    // A type parameter is an error as the type of a variable or of a field, alone or inside a record.
    [Theory]
    [InlineData("""{"variables": [{"name": "x", "type": |"~T"}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type": |"list<~T>"}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type": |"map<string, ~V>"}]}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": "x", "type": |"list<~T>"}]}]}}""")]
    public void TypeParameterOutsideAnOverload_IsAnErrorAtTheValue(string marked) =>
        JsonExample.AssertError(marked);
}
