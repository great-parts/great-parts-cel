using System;
using Great.Parts.Cel.Diagnostics;
using Xunit;
#if NET11_0_OR_GREATER
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Values;
#endif

namespace Great.Parts.Cel.Tests.Examples.Configuration;

// The set decimal and the type decimal in the environment description in JSON (the page of the description, "Extension sets",
// "Type records"; the page of the extension set decimal, "The set"). The set cannot be named in a description: decimal is no name of
// the sets of the catalog, so {"name": "decimal", "version": "<any>"} is InvalidConfiguration (4009) at the value of name, with
// the text of every name that the catalog does not take, "'decimal' is not the name of an extension set of the library", in both
// targets and for every version; the builder is left as it was. A version of the set that does not exist is therefore reached from
// code only (DecimalSetBuildTests). The description has no key for the literal option either: the option is a method of the
// builder, and a key for it is an unknown key. The type decimal is a type name like any other: "type": "decimal", "list<decimal>"
// and "map<string, decimal>" are read, and Build declares them when the code added the set and refuses them otherwise —
// TypeNotDeclared (4008) at the "{" of the variable, with the hint that names the set, or in net10.0 the hint that says the set
// exists only on .NET 11. In the marked documents each '|' is the place of one expected error (see JsonExample).
[Trait("Requirement", "F-7")]
[Trait("Requirement", "F-5")]
public sealed class EnvironmentJsonDecimalTests
{
    private const string NotAName = "'decimal' is not the name of an extension set of the library";

    [Theory]
    [InlineData("""{"extensions": [{"name": |"decimal", "version": "1"}]}""")]
    [InlineData("""{"extensions": [{"name": |"decimal", "version": "0"}]}""")]
    [InlineData("""{"extensions": [{"name": |"decimal", "version": "2"}]}""")]
    [InlineData("""{"extensions": [{"name": |"decimal", "version": "4294967295"}]}""")]
    [InlineData("""{"extensions": [{"name": "strings", "version": "5"}, {"name": |"decimal", "version": "1"}]}""")]
    [InlineData("""{"extensions": [{"name": |"decimal", "version": "1"}, {"name": "strings", "version": "5"}]}""")]
    public static void NameDecimal_IsAnErrorAtTheValue(string marked) => JsonExample.AssertError(marked, mentions: NotAName);

    [Fact]
    public static void NameDecimalAndAVersionThatIsWrong_AreTwoErrors_TheNameFirst() =>
        JsonExample.AssertErrors("""{"extensions": [{"name": |"decimal", "version": |"latest"}]}""");

    [Theory]
    [InlineData("""{"extensions": [{"name": |"Decimal", "version": "1"}]}""", "Decimal")]
    [InlineData("""{"extensions": [{"name": |"decimal ", "version": "1"}]}""", "decimal ")]
    [InlineData("""{"extensions": [{"name": |"cel.lib.ext.decimal", "version": "1"}]}""", "cel.lib.ext.decimal")]
    public static void NamesNearDecimal_AreUnknownNamesToo(string marked, string name) => JsonExample.AssertError(marked, mentions: name);

    [Theory]
    [InlineData("""{|"decimalLiterals": true}""")]
    [InlineData("""{|"decimal_literals": true}""")]
    [InlineData("""{|"enableDecimalLiterals": true}""")]
    [InlineData("""{|"decimal": {"version": "1"}}""")]
    public static void KeyForTheLiteralOption_IsAnUnknownKey(string marked) => JsonExample.AssertError(marked);

    // The type decimal in a record: at Build, in an environment whose builder has no set, the variable is TypeNotDeclared at its element.
    [Theory]
    [InlineData("decimal", "decimal")]
    [InlineData("list<decimal>", "list(decimal)")]
    [InlineData("map<string, decimal>", "map(string, decimal)")]
    public static void VariableOfTheTypeDecimalWithoutTheSet_IsTypeNotDeclaredAtTheVariable(string record, string type)
    {
        string json = "{\"name\": \"m\", \"variables\": [{\"name\": \"price\", \"type\": \"" + record + "\"}]}";
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, json));
#if NET11_0_OR_GREATER
        const string Hint = "; add the extension set 'decimal'";
#else
        const string Hint = "; the extension set 'decimal' exists only on .NET 11";
#endif

        JsonExample.AssertBuildErrorAt(
            JsonExample.BuildError(builder),
            CelErrorCode.TypeNotDeclared,
            "variable 'price' has type '" + type + "', which no type source or extension of the environment declares" + Hint,
            "environment JSON 'm'",
            json.IndexOf("{\"name\": \"price\"", StringComparison.Ordinal));
    }

#if NET11_0_OR_GREATER
    // With the set added in code — before or after the description — the record is the type decimal, and the variable works as one.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void VariableOfTheTypeDecimalWithTheSet_IsTheTypeDecimal(bool setAfterTheDescription)
    {
        const string Json = """{"variables": [{"name": "price", "type": "decimal"}, {"name": "prices", "type": "list<decimal>"}, {"name": "rates", "type": "map<string, decimal>"}]}""";
        var builder = JsonExample.NewBuilder();
        if (!setAfterTheDescription)
        {
            builder.AddExtension(CelExtensions.Decimal(1)).EnableDecimalLiterals();
        }

        Assert.Empty(JsonExample.Apply(builder, Json));
        if (setAfterTheDescription)
        {
            builder.AddExtension(CelExtensions.Decimal(1)).EnableDecimalLiterals();
        }

        CelEnvironment environment = builder.Build().GetEnvironmentOrThrow();
        CelBindings bindings = new CelBindings()
            .Add("price", CelValue.FromDecimal(DecimalExample.Make(25, -1)))
            .Add("prices", CelValue.FromList(CelList.Create(CelValue.FromDecimal(DecimalExample.Make(15, -1)))))
            .Add("rates", CelValue.FromMap(CelMap.Create([new KeyValuePair<CelValue, CelValue>(CelValue.FromString("a"), CelValue.FromDecimal(DecimalExample.Make(250, -2)))])));

        CelValue value = environment.Compile<CelValue>("price > 1m && prices[0] < price && rates['a'] == price && type(price) == decimal").GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();

        Assert.True(value.TryGetBoolean(out bool truth) && truth);
    }
#endif
}
