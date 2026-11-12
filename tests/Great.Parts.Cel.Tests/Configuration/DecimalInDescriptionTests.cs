using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Configuration;
using Xunit;
#if NET11_0_OR_GREATER
using Great.Parts.Cel.Values;
#endif

namespace Great.Parts.Cel.Tests.Configuration;

// The type decimal in the parameters and the result of a function of an environment description, where the examples of the description
// have the type of a variable: the name is a type name like any other, and an environment without the set that declares the type
// refuses the overload as TypeNotDeclared, whole type as written, with the hint of the target — the set to add on .NET 11, and on
// .NET 10 that the set exists only on .NET 11. The description has no way to add the set, so the set comes from code.
public sealed class DecimalInDescriptionTests
{
#if NET11_0_OR_GREATER
    private const string Hint = "; add the extension set 'decimal'";
#else
    private const string Hint = "; the extension set 'decimal' exists only on .NET 11";
#endif

    [Theory]
    [InlineData("\"args\": [\"decimal\"], \"return\": \"int\"", "decimal")]
    [InlineData("\"args\": [\"int\"], \"return\": \"decimal\"", "decimal")]
    [InlineData("\"args\": [\"int\", \"list<decimal>\"], \"return\": \"int\"", "list(decimal)")]
    [InlineData("\"args\": [\"int\"], \"return\": \"map<string, decimal>\"", "map(string, decimal)")]
    public static void OverloadOfADescriptionWithTheTypeDecimal_WithoutTheSet_IsTypeNotDeclared(string signature, string type)
    {
        string json = "{\"functions\": [{\"name\": \"price\", \"overloads\": [{\"id\": \"price_of\", " + signature + "}]}]}";
        CelEnvironmentBuilder builder = JsonExample.NewBuilder();

        Assert.Empty(JsonExample.Apply(builder, json));

        JsonExample.AssertBuildError(
            JsonExample.BuildError(builder),
            CelErrorCode.TypeNotDeclared,
            "overload 'price_of' has type '" + type + "', which no type source or extension of the environment declares" + Hint);
    }

#if NET11_0_OR_GREATER
    // The same description with the set added in code builds: the overload is declared with the type, and its implementation is the one
    // that the code registers by the identifier.
    [Fact]
    public static void OverloadOfADescriptionWithTheTypeDecimal_WithTheSet_Builds()
    {
        const string Json = """{"functions": [{"name": "price", "overloads": [{"id": "price_of", "args": ["int"], "return": "decimal"}]}]}""";
        CelEnvironmentBuilder builder = JsonExample.NewBuilder().AddExtension(CelExtensions.Decimal(1));

        Assert.Empty(JsonExample.Apply(builder, Json));

        Assert.True(builder.AddFunctionImplementation("price_of", static _ => CelValue.Null).Build().IsSuccess);
    }
#endif
}
