using System;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// The lists of CelValidationResult on the forms the examples leave out: the container, the macros of every kind, the functions
// of the extension sets, the order of the lists, and the arguments of the two methods.
public sealed class ValidateNamesTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder()
        .UseContainer("acme.sales")
        .AddVariable<long>("acme.sales.limit")
        .AddVariable<long>("count")
        .AddVariable<string>("name")
        .AddVariable("items", CelType.ListOf(CelType.Int))
        .AddVariable("m", CelType.MapOf(CelType.String, CelType.Int))
        .AddVariable<long>("Zeta")
        .AddVariable<long>("_beta")
        .AddVariable<long>("alpha")
        .AddExtension(CelExtensions.Math(3))
        .AddExtension(CelExtensions.Strings(5))
        .AddExtension(CelExtensions.Bindings(1))
        .AddExtension(CelExtensions.TwoVarComprehensions(0))
        .AddExtension(CelExtensions.Optional(2))
        .Build()
        .GetEnvironmentOrThrow();

    // The name of a variable is the one declared, after the container: a short name and the full name are the same variable.
    [Theory]
    [InlineData("limit + 1")]
    [InlineData("acme.sales.limit + 1")]
    public static void ContainerName_IsTheDeclaredVariable(string text)
    {
        CelValidationResult result = Environment.Validate<long>(text);

        Assert.True(result.IsValid);
        Assert.Equal(["acme.sales.limit"], result.ReferencedVariables.ToArray());
    }

    // The local variable of a macro is not a variable of the environment, at any depth of nesting.
    [Theory]
    [InlineData("items.all(x, x > count)", new[] { "count", "items" })]
    [InlineData("items.exists(x, x > 0) && items.exists_one(y, y == 1)", new[] { "items" })]
    [InlineData("items.map(x, x * count).filter(y, y > 1)", new[] { "count", "items" })]
    [InlineData("items.all(i, v, v > i)", new[] { "items" })]
    [InlineData("items.transformList(i, v, v + i + count).size() > 0", new[] { "count", "items" })]
    [InlineData("cel.bind(t, count + 1, t * t) > 0", new[] { "count" })]
    [InlineData("cel.bind(a, count, cel.bind(b, a + count, b + a)) > 0", new[] { "count" })]
    [InlineData("has(m.k) && m.k > count", new[] { "count", "m" })]
    public static void MacroLocals_AreNotVariables(string text, string[] variables)
    {
        CelValidationResult result = Environment.Validate(text);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(error => error.ToString())));
        Assert.Equal(variables, result.ReferencedVariables.ToArray());
    }

    [Theory]
    [InlineData("count in items ? count : -count")]
    [InlineData("items[0] + count % 2 - count / 3 == 4 || !(name < 'a') && 1 <= 2")]
    public static void Operators_AreNotFunctions(string text)
    {
        CelValidationResult result = Environment.Validate(text);

        Assert.True(result.IsValid);
        Assert.Empty(result.ReferencedFunctions);
    }

    // A macro is a name that the text calls, so it is listed by the name the text writes — a qualified name for a macro of a
    // namespace — and the functions its expansion calls are listed as for any call.
    [Theory]
    [InlineData("items.all(x, x > count)", new[] { "all" })]
    [InlineData("has(m.k)", new[] { "has" })]
    [InlineData("cel.bind(t, count + 1, t * t) > 0", new[] { "cel.bind" })]
    [InlineData("items.map(x, x * 2).filter(y, y > 1)", new[] { "filter", "map" })]
    [InlineData("items.map(x, x > 0, x * 2).exists_one(y, y == 2)", new[] { "exists_one", "map" })]
    [InlineData("math.greatest(count, 5) > math.least(count, 1)", new[] { "math.greatest", "math.least" })]
    [InlineData("items.all(i, v, v > i) && items.exists(x, x > 0)", new[] { "all", "exists" })]
    public static void Macros_AreListedByTheNameOfTheText(string text, string[] functions)
    {
        CelValidationResult result = Environment.Validate(text);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(error => error.ToString())));
        Assert.Equal(functions, result.ReferencedFunctions.ToArray());
    }

    // The macros that expand to calls of names that no text can write list only the name of the macro: neither the function that
    // math.greatest expands to, nor the map that sortBy builds for its own part.
    [Fact]
    public void InternalNamesOfAnExpansion_AreNotListed()
    {
        CelEnvironment environment = new CelEnvironmentBuilder()
            .AddVariable("items", CelType.ListOf(CelType.Int))
            .AddExtension(CelExtensions.Lists(4))
            .AddExtension(CelExtensions.Optional(2))
            .AddExtension(CelExtensions.Math(3))
            .Build()
            .GetEnvironmentOrThrow();

        CelValidationResult sorted = environment.Validate("items.sortBy(x, -x).size() > 0");
        CelValidationResult optional = environment.Validate("optional.of(1).optMap(x, x + 1).hasValue()");
        CelValidationResult greatest = environment.Validate("math.greatest(1, 2)");

        Assert.True(sorted.IsValid, string.Join("; ", sorted.Errors.Select(error => error.ToString())));
        Assert.Equal(["size", "sortBy"], sorted.ReferencedFunctions.ToArray());
        Assert.True(optional.IsValid, string.Join("; ", optional.Errors.Select(error => error.ToString())));
        Assert.Contains("optMap", optional.ReferencedFunctions);
        Assert.DoesNotContain(optional.ReferencedFunctions, name => name.Contains('@', StringComparison.Ordinal));
        Assert.Equal(["math.greatest"], greatest.ReferencedFunctions.ToArray());
    }

    // A function of an extension set is a function by its name, qualified or not.
    [Fact]
    public void ExtensionFunctions_AreListedByName()
    {
        CelValidationResult result = Environment.Validate("math.abs(count) > 0 && name.upperAscii().startsWith('A') && math.ceil(1.5) < 3.0 && math.abs(-1) == 1");

        Assert.True(result.IsValid);
        Assert.Equal(["math.abs", "math.ceil", "startsWith", "upperAscii"], result.ReferencedFunctions.ToArray());
        Assert.Equal(["count", "name"], result.ReferencedVariables.ToArray());
    }

    [Fact]
    public void FunctionOfAnExpansion_IsListed()
    {
        CelValidationResult result = Environment.Validate("items.map(x, double(x)).exists(y, y > 0.5)");

        Assert.Equal(["double", "exists", "map"], result.ReferencedFunctions.ToArray());
        Assert.Equal(["items"], result.ReferencedVariables.ToArray());
    }

    // The order is that of string.CompareOrdinal: capitals, then the underscore, then lower case letters.
    [Fact]
    public void Lists_AreInOrdinalOrder()
    {
        CelValidationResult result = Environment.Validate<long>("alpha + _beta + Zeta + math.abs(1)");

        Assert.Equal(["Zeta", "_beta", "alpha"], result.ReferencedVariables.ToArray());
    }

    [Fact]
    public void NoNames_GiveEmptyListsThatAreNotDefault()
    {
        CelValidationResult result = Environment.Validate("1 + 2");

        Assert.True(result.IsValid);
        Assert.Equal(CelType.Int, result.ResultType);
        Assert.False(result.ReferencedVariables.IsDefault);
        Assert.False(result.ReferencedFunctions.IsDefault);
        Assert.True(result.ReferencedVariables.IsEmpty);
        Assert.True(result.ReferencedFunctions.IsEmpty);
    }

    // The local variable of a macro stays out of the list also when the check fails inside the macro; the unresolved name does too.
    [Fact]
    public void CheckErrorInsideAMacro_ListsWhatResolved()
    {
        CelValidationResult result = Environment.Validate("items.all(x, x > nothing) && count > 0");

        Assert.False(result.IsValid);
        Assert.Equal(CelErrorCode.UndeclaredReference, Assert.Single(result.Errors).Code);
        Assert.Null(result.ResultType);
        Assert.Equal(["count", "items"], result.ReferencedVariables.ToArray());
        Assert.Equal(["all"], result.ReferencedFunctions.ToArray());
    }

    [Fact]
    public void ResultType_OfAnUntypedVariable_IsDyn()
    {
        CelEnvironment environment = new CelEnvironmentBuilder().AddVariable("anything", CelType.Dyn).Build().GetEnvironmentOrThrow();

        CelValidationResult result = environment.Validate<bool>("anything");

        Assert.True(result.IsValid);
        Assert.Equal(CelType.Dyn, result.ResultType);
    }

    // An expected type that the expression does not have is a check error with the place of the expression, as for Compile.
    [Fact]
    public void ExpectedTypeMismatch_HasAPlace()
    {
        CelValidationResult result = Environment.Validate<bool>("1 + 2");

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.ExpectedTypeMismatch, error.Code);
        Assert.Equal(CelErrorStage.Check, error.Stage);
        Assert.Equal(1, error.Location.Line);
        Assert.Equal(Environment.Compile<bool>("1 + 2").Errors[0].Location, error.Location);
        Assert.Null(result.ResultType);
    }

    [Fact]
    public void ExpectedTypeOfTheOptions_IsTheTypeThatIsChecked()
    {
        CelValidationResult result = Environment.Validate("count", new CelCompileOptions { ExpectedType = CelType.Bool });

        Assert.Equal(CelErrorCode.ExpectedTypeMismatch, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Arguments_AreCheckedAsForCompile()
    {
        Assert.Equal("expression", Assert.Throws<ArgumentNullException>(() => Environment.Validate<long>(null!)).ParamName);
        Assert.Equal("expression", Assert.Throws<ArgumentNullException>(() => Environment.Validate(null!)).ParamName);
        Assert.Throws<ArgumentException>(() => Environment.Validate<short>("count +"));
        Assert.Throws<ArgumentException>(() => Environment.Validate<Guid>("count"));
    }
}
