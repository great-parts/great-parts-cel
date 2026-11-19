using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Orders;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// Validation of an expression without a program: the same compilation as Compile<T>, all errors, the result type and the
// names of the variables of the environment and of the functions called by name, distinct and in ordinal order. A macro
// that the text calls by name (all, exists) is in the list of the functions, as a function is; local variables of macros
// and operators are not names of the environment; the fields of the input are variables.
[Trait("Requirement", "F-10")]
public sealed class ValidateTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder()
        .AddTypes(ShopContext.Default)
        .UseInput<Order>()
        .AddVariable<long>("limit")
        .AddVariable("tags", CelType.ListOf(CelType.String))
        .AddFunction<string, bool>("isKpp", s => s.Length == 9)
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void ValidExpression_GivesTheResultTypeAndTheNames()
    {
        CelValidationResult result = Environment.Validate<bool>(
            "total > double(limit) && lines.all(l, l.qty > 0) && tags.exists(t, isKpp(t)) && size(tags) > 0 && status.startsWith('a')");

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(CelType.Bool, result.ResultType);
        Assert.Equal(["limit", "lines", "status", "tags", "total"], result.ReferencedVariables.ToArray());
        Assert.Equal(["all", "double", "exists", "isKpp", "size", "startsWith"], result.ReferencedFunctions.ToArray());
    }

    [Fact]
    public void NamesAreDistinct()
    {
        CelValidationResult result = Environment.Validate<long>("limit + limit * size(tags) + size(lines)");

        Assert.Equal(["limit", "lines", "tags"], result.ReferencedVariables.ToArray());
        Assert.Equal(["size"], result.ReferencedFunctions.ToArray());
    }

    [Fact]
    public void ParseError_IsInvalidWithAllErrorsAndNoType()
    {
        CelValidationResult result = Environment.Validate<bool>("total > && )");

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, error => Assert.Equal(CelErrorStage.Parse, error.Stage));
        Assert.Null(result.ResultType);
        Assert.Empty(result.ReferencedVariables);
        Assert.Empty(result.ReferencedFunctions);
    }

    // The lists hold what the check resolved before it failed; the unresolved name is not in them.
    [Fact]
    public void CheckError_ListsWhatResolved()
    {
        CelValidationResult result = Environment.Validate<bool>("total > 1.0 && totl > 1.0 && size(tags) > 0");

        Assert.False(result.IsValid);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.UndeclaredReference, error.Code);
        Assert.Equal("totl", error.Subject);
        Assert.Null(result.ResultType);
        Assert.Equal(["tags", "total"], result.ReferencedVariables.ToArray());
        Assert.Equal(["size"], result.ReferencedFunctions.ToArray());
    }

    // The expected type comes from T, as for Compile<T>; Validate without T takes any result type.
    [Fact]
    public void ExpectedType_ComesFromT()
    {
        CelValidationResult typed = Environment.Validate<bool>("total");
        CelValidationResult untyped = Environment.Validate("total");

        Assert.Equal(CelErrorCode.ExpectedTypeMismatch, Assert.Single(typed.Errors).Code);
        Assert.True(untyped.IsValid);
        Assert.Equal(CelType.Double, untyped.ResultType);
    }

    [Fact]
    public void Validate_GivesTheErrorsOfCompile()
    {
        const string Text = "limit + 'a' + totl";

        CelValidationResult validated = Environment.Validate<long>(Text);
        CelCompileResult<long> compiled = Environment.Compile<long>(Text);

        Assert.Equal(compiled.Errors.Select(error => error.ToString()), validated.Errors.Select(error => error.ToString()));
    }
}
