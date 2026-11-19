using System;
using System.Linq;
using System.Text;
using System.Threading;
using Great.Parts.Cel.Configuration;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Orders;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Diagnostics;

// The parts of the errors of the codes that the examples of ErrorPartsTests do not reach, by the section "Parts of an error" of docs/error-codes.md, one
// test for each code or group of codes that an expression, a builder or a description in JSON can give without an application type
// from a generator or a protobuf message: the fragment of the error, the subject and the two types; a part the column does not name
// is null.
[Trait("Requirement", "F-11")]
public sealed class ErrorPartsByCodeTests
{
    private static readonly string[] Strings = ["a"];
    private static readonly CelType DynList = CelType.ListOf(CelType.Dyn);

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddTypes(ShopContext.Default)
        .AddExtension(CelExtensions.Strings(5))
        .AddExtension(CelExtensions.Optional(2))
        .AddVariable<long>("x")
        .AddVariable<string>("s")
        .AddVariable("l", CelType.ListOf(CelType.Int))
        .AddVariable("m", CelType.MapOf(CelType.String, CelType.Int))
        .AddVariable("d", CelType.Dyn)
        .AddFunction<long, long>("boom", _ => throw new InvalidOperationException("failed"))
        .AddFunction<int, long>("narrow", value => value)
        .Build()
        .GetEnvironmentOrThrow();

    // A .NET type that no type source describes.
    private sealed class Opaque
    {
        public int Member { get; } = 1;
    }

    [Fact]
    public void InvalidLiteral_HasTheLiteralAsItsFragment_AndNoParts()
    {
        CelError error = Single("9223372036854775808");

        AssertParts(error, CelErrorCode.InvalidLiteral, null, null, null);
        Assert.Equal((0, 19), (error.Span.Start.Offset, error.Span.Length));
    }

    [Fact]
    public void ExpressionTooLong_HasTheCharacterWhereTheLimitIsPassed()
    {
        CelError error = Single(new string('1', 70_000));

        AssertParts(error, CelErrorCode.ExpressionTooLong, null, null, null);
        Assert.Equal(1, error.Span.Length);
        Assert.Equal(error.Location, error.Span.Start);
    }

    [Fact]
    public void NestingTooDeep_HasTheTokenThatOpenedTheOperand()
    {
        CelError error = Environment.Compile<CelValue>(new string('[', 300)).Errors.First(candidate => candidate.Code == CelErrorCode.NestingTooDeep);

        AssertParts(error, CelErrorCode.NestingTooDeep, null, null, null);
        Assert.Equal(1, error.Span.Length);
        Assert.Equal(error.Location, error.Span.Start);
    }

    [Fact]
    public void TooManyErrors_IsTheLastError_AtTheCharacterWhereParsingStopped()
    {
        CelError error = Environment.Compile<CelValue>("[" + string.Concat(Enumerable.Repeat("1 2, ", 150)) + "]").Errors[^1];

        AssertParts(error, CelErrorCode.TooManyErrors, null, null, null);
        Assert.Equal(1, error.Span.Length);
        Assert.Equal(error.Location, error.Span.Start);
    }

    [Fact]
    public void SyntaxNotEnabled_HasTheTokenOfTheSyntax()
    {
        CelError error = Single("d.?x", new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable("d", CelType.Dyn).Build().GetEnvironmentOrThrow());

        AssertParts(error, CelErrorCode.SyntaxNotEnabled, null, null, null);
        Assert.Equal((2, 1), (error.Span.Start.Offset, error.Span.Length));
    }

    // A parse error at a token has the token as its fragment: the reserved word, the macro argument that is not an identifier.
    [Fact]
    public void ReservedIdentifier_AndInvalidMacroCall_HaveTheTokenAndTheNodeAsTheirFragments()
    {
        CelError reserved = Single("x + package");
        Assert.Equal((4, 7, "package"), (reserved.Span.Start.Offset, reserved.Span.Length, reserved.Subject));

        CelError macro = Single("l.all(1, true)");
        AssertParts(macro, CelErrorCode.InvalidMacroCall, "all", null, null);
        Assert.Equal((5, 9), (macro.Span.Start.Offset, macro.Span.Length));
    }

    [Fact]
    public void NotAType_HasTheName()
    {
        CelError error = Single("x{a: 1}");

        AssertParts(error, CelErrorCode.NotAType, "x", null, null);
        Assert.Equal(error.Location, error.Span.Start);
        Assert.True(error.Span.Length > 0);
    }

    [Fact]
    public void TypeNotConstructible_HasTheTypeOfTheLiteral()
    {
        CelError error = Single("shop.Order{}");

        AssertParts(error, CelErrorCode.TypeNotConstructible, "shop.Order", null, null);
        Assert.Equal(error.Location, error.Span.Start);
    }

    [Fact]
    public void InvalidRegex_HasTheLiteralAsItsFragment_AndNoParts()
    {
        CelError error = Single("s.matches('(')");

        AssertParts(error, CelErrorCode.InvalidRegex, null, null, null);
        Assert.Equal((10, 3), (error.Span.Start.Offset, error.Span.Length));
    }

    [Fact]
    public void InvalidFormatString_HasTheFunction()
    {
        CelError error = Single("'%z'.format([1])");

        AssertParts(error, CelErrorCode.InvalidFormatString, "format", null, null);
        Assert.Equal(error.Location, error.Span.Start);
    }

    [Fact]
    public void EvalNoSuchField_OfAnObject_HasTheFieldAndTheTypeOfTheValue()
    {
        CelError error = Evaluate("d.zzz", Bindings("d", new Order()));

        AssertParts(error, CelErrorCode.EvalNoSuchField, "zzz", null, CelType.ObjectNamed("shop.Order"));
    }

    [Fact]
    public void ModulusByZero_DuplicateMapKey_AndOptionalHasNoValue_HaveNoParts()
    {
        AssertParts(Evaluate("x % 0", Bindings("x", 1L)), CelErrorCode.ModulusByZero, null, null, null);
        AssertParts(Evaluate("{'a': 1, 'a': 2}", Bindings("x", 1L)), CelErrorCode.DuplicateMapKey, null, null, null);
        AssertParts(Evaluate("optional.none().value()", Bindings("x", 1L)), CelErrorCode.OptionalHasNoValue, null, null, null);
    }

    [Fact]
    public void TypeNotAccessible_HasTheFullNameOfTheNetType()
    {
        CelError error = Evaluate("d.member", Bindings("d", new Opaque()));

        AssertParts(error, CelErrorCode.TypeNotAccessible, typeof(Opaque).ToString(), null, null);
        Assert.Equal(error.Location, error.Span.Start);
    }

    [Fact]
    public void ApplicationException_HasTheOverloadOfTheFunction_AndTheException()
    {
        CelError error = Evaluate("boom(1)", Bindings("x", 1L));

        Assert.Equal(CelErrorCode.ApplicationException, error.Code);
        Assert.Contains("boom", error.Subject, StringComparison.Ordinal);
        Assert.Null(error.ExpectedType);
        Assert.Null(error.ActualType);
        Assert.IsType<InvalidOperationException>(error.Exception);
    }

    [Fact]
    public void EvalInvalidRegex_HasNoParts()
    {
        AssertParts(Evaluate("s.matches(s)", Bindings("s", "(")), CelErrorCode.EvalInvalidRegex, null, null, null);
    }

    [Fact]
    public void ArgumentConversion_HasTheOverloadAndTheTypeOfTheValue()
    {
        CelError error = Evaluate("narrow(5000000000)", Bindings("x", 1L));

        Assert.Equal(CelErrorCode.ArgumentConversion, error.Code);
        Assert.Contains("narrow", error.Subject, StringComparison.Ordinal);
        Assert.Null(error.ExpectedType);
        Assert.Equal(CelType.Int, error.ActualType);
    }

    [Fact]
    public void FieldSelectionNotSupported_HasTheFieldAndTheTypeOfTheValue()
    {
        AssertParts(Evaluate("d.member", Bindings("d", 1L)), CelErrorCode.FieldSelectionNotSupported, "member", null, CelType.Int);
    }

    [Fact]
    public void EvalInvalidComprehensionRange_AndUnsupportedMapKeyType_HaveTheTypeOfTheValue()
    {
        AssertParts(Evaluate("d.all(i, true)", Bindings("d", 1L)), CelErrorCode.EvalInvalidComprehensionRange, null, null, CelType.Int);
        AssertParts(Evaluate("{d: 1}", Bindings("d", 1.5)), CelErrorCode.UnsupportedMapKeyType, null, null, CelType.Double);
    }

    // A list value in the place of a map: the type of the value at the top level is list(dyn) as the function type gives it, and a
    // list that does not fit the declared type deeper names the type of its first element that does not, as the message does.
    [Fact]
    public void BindingTypeMismatch_OfAListAndOfAnElement_NamesTheTypeTheMessageNames()
    {
        CelError top = Evaluate("m.size()", Bindings("m", new long[] { 1 }));
        AssertParts(top, CelErrorCode.BindingTypeMismatch, "m", CelType.MapOf(CelType.String, CelType.Int), DynList);

        CelError deep = Evaluate("size(l)", Bindings("l", Strings));
        AssertParts(deep, CelErrorCode.BindingTypeMismatch, "l", CelType.ListOf(CelType.Int), CelType.ListOf(CelType.String));
        Assert.Contains("'list(string)'", deep.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownExtensionVersion_HasTheSet_AndNoFragment()
    {
        CelError error = BuildError(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddExtension(CelExtensions.Math(2)));

        AssertParts(error, CelErrorCode.UnknownExtensionVersion, "math", null, null);
        Assert.Equal(default, error.Span);
    }

    [Fact]
    public void MissingExtensionDependency_HasTheSetThatIsMissing()
    {
        AssertParts(BuildError(new CelEnvironmentBuilder(CelStandardLibrary.V1).EnableDecimalLiterals()), CelErrorCode.MissingExtensionDependency, "decimal", null, null);
    }

    [Fact]
    public void InvalidFunctionName_AndInvalidVariableName_HaveTheName()
    {
        AssertParts(
            BuildError(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddFunction<long, long>("_+_", value => value)),
            CelErrorCode.InvalidFunctionName,
            "_+_",
            null,
            null);
        AssertParts(BuildError(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<long>("a b")), CelErrorCode.InvalidVariableName, "a b", null, null);
    }

    [Fact]
    public void ContainerConflict_HasNoSubject()
    {
        AssertParts(BuildError(new CelEnvironmentBuilder(CelStandardLibrary.V1).UseContainer("a").UseContainer("b")), CelErrorCode.ContainerConflict, null, null, null);
    }

    [Fact]
    public void InvalidConfiguration_NamesTheKey_AndHasNoFragment()
    {
        CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);

        CelError unknown = Assert.Single(CelEnvironmentJson.Apply(builder, Encoding.UTF8.GetBytes("{\"zzz\": 1}")));
        AssertParts(unknown, CelErrorCode.InvalidConfiguration, "zzz", null, null);
        Assert.Equal(default, unknown.Span);
        Assert.NotNull(unknown.SourceLocation);

        CelError notJson = Assert.Single(CelEnvironmentJson.Apply(builder, Encoding.UTF8.GetBytes("{")));
        AssertParts(notJson, CelErrorCode.InvalidConfiguration, null, null, null);
    }

    [Fact]
    public void InputConflict_IsTheNextNumberOfTheBuildRange()
    {
        Assert.Equal(4019, (int)CelErrorCode.InputConflict);
    }

    private static CelError Single(string expression, CelEnvironment? environment = null) =>
        Assert.Single((environment ?? Environment).Compile<CelValue>(expression).Errors);

    private static CelBindings Bindings<T>(string name, T value) => new CelBindings().Add(name, value);

    private static CelError Evaluate(string expression, CelBindings bindings)
    {
        CelEvalResult<CelValue> result = Environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorStage.Evaluate, error.Stage);
        return error;
    }

    private static CelError BuildError(CelEnvironmentBuilder builder) => Assert.Single(builder.Build().Errors);

    private static void AssertParts(CelError error, CelErrorCode code, string? subject, CelType? expected, CelType? actual)
    {
        Assert.Equal(code, error.Code);
        Assert.Equal(subject, error.Subject);
        Assert.Equal(expected, error.ExpectedType);
        Assert.Equal(actual, error.ActualType);
    }
}
