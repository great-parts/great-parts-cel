using System;
using System.Linq;
using System.Text;
using System.Threading;
using Great.Parts.Cel.Configuration;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// The end-to-end path through the public API: build an environment, compile an expression, evaluate
// the program on bindings and receive the value as the requested .NET type, or the errors.
// Evaluations pass CancellationToken.None: these examples do not cancel.
public sealed class EndToEndTests
{
    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("x")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    [Trait("Requirement", "+R-5")]
    [Trait("Requirement", "+R-10")]
    public void XPlusOne_With41_Is42AsLong()
    {
        CelBuildResult build = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<long>("x")
            .Build();
        CelCompileResult<long> compiled = build.GetEnvironmentOrThrow().Compile<long>("x + 1");
        CelProgram<long> program = compiled.GetProgramOrThrow();

        CelEvalResult<long> result = program.Evaluate(new CelBindings().Add("x", 41L), cancellationToken: CancellationToken.None);

        Assert.True(build.IsSuccess);
        Assert.True(compiled.IsSuccess);
        Assert.Equal(CelType.Int, program.ResultType);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
        Assert.Equal(42L, result.Value);
    }

    // One compilation is evaluated any number of times; an int binding is read as the CEL type int.
    [Fact]
    [Trait("Requirement", "+R-5")]
    public void OneProgram_IsEvaluatedOnAnyBindings()
    {
        CelProgram<long> program = environment.Compile<long>("x + 1").GetProgramOrThrow();

        Assert.Equal(1L, Run(program, new CelBindings().Add("x", 0L)));
        Assert.Equal(-41L, Run(program, new CelBindings().Add("x", -42L)));
        Assert.Equal(42L, Run(program, new CelBindings().Add("x", 41)));
    }

    [Fact]
    [Trait("Requirement", "F-7")]
    public void EnvironmentFromJson_EvaluatesAsTheEnvironmentFromCode()
    {
        var builder = new CelEnvironmentBuilder(CelStandardLibrary.V1);
        byte[] json = Encoding.UTF8.GetBytes("""{"name": "manifest", "variables": [{"name": "x", "type": "int"}]}""");

        Assert.Empty(CelEnvironmentJson.Apply(builder, json));
        CelProgram<long> program = builder.Build().GetEnvironmentOrThrow().Compile<long>("x + 1").GetProgramOrThrow();

        Assert.Equal(42L, Run(program, new CelBindings().Add("x", 41L)));
    }

    // A parse error comes as the result of compilation, with the offset, the line and the column.
    [Fact]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "+R-9")]
    public void ParseError_ComesAsTheResultWithItsPlace()
    {
        CelCompileResult<long> compiled = environment.Compile<long>("x +\n  (1");

        Assert.False(compiled.IsSuccess);
        Assert.Throws<InvalidOperationException>(() => compiled.Program);
        CelError error = compiled.Errors[0];
        Assert.Equal(CelErrorCode.SyntaxError, error.Code);
        Assert.Equal(CelErrorStage.Parse, error.Stage);
        Assert.Equal((8, 2, 5), (error.Location.Offset, error.Location.Line, error.Location.Column));
        Assert.Null(error.Location.SourceName);
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void UndeclaredNames_AreAllReportedAtTheirIdentifiers()
    {
        CelCompileResult<long> compiled = environment.Compile<long>("y + z");

        Assert.Equal(
            new[] { (CelErrorCode.UndeclaredReference, 0), (CelErrorCode.UndeclaredReference, 4) },
            compiled.Errors.Select(error => (error.Code, error.Location.Offset)));
        Assert.All(compiled.Errors, error => Assert.Equal(CelErrorStage.Check, error.Stage));
        Assert.Equal("undeclared reference to 'y' (in container '')", compiled.Errors[0].Message);
    }

    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("-1u", 0, "found no matching overload for '-_' applied to '(uint)'")]
    [InlineData("'a' + 1", 4, "found no matching overload for '_+_' applied to '(string, int)'")]
    [InlineData("x / 2u", 2, "found no matching overload for '_/_' applied to '(int, uint)'")]
    public void ArgumentsThatNoOverloadAccepts_AreNoMatchingOverloadAtTheCall(string expression, int offset, string message)
    {
        CelError error = Assert.Single(environment.Compile<CelValue>(expression).Errors);

        Assert.Equal(CelErrorCode.NoMatchingOverload, error.Code);
        Assert.Equal(CelErrorStage.Check, error.Stage);
        Assert.Equal(offset, error.Location.Offset);
        Assert.Equal(message, error.Message);
    }

    [Fact]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "+R-10")]
    public void Literals_GiveValuesOfTheirKinds()
    {
        Assert.Equal(-16L, Evaluate<long>("-0x10"));
        Assert.Equal(ulong.MaxValue, Evaluate<ulong>("18446744073709551615u"));
        Assert.Equal(0.25, Evaluate<double>("2.5e-1"));
        Assert.Equal("a\tb\u00e9", Evaluate<string>("'a\\tb\\u00e9'"));
        Assert.Equal("a\\tb", Evaluate<string>("r'a\\tb'"));
        Assert.Equal("line 1\nline 2", Evaluate<string>("'''line 1\nline 2'''"));
        Assert.True(Evaluate<bool>("true"));
        Assert.Equal(new byte[] { 0x00, 0xFF, 0x61 }, Evaluate<byte[]>("b'\\000\\xffa'"));
        Assert.Null(Evaluate<object>("null"));
        Assert.Equal(CelValueKind.Null, Evaluate<CelValue>("null").Kind);
    }

    [Fact]
    [Trait("Requirement", "F-1")]
    public void ListLiteral_KeepsItsElementsInOrder()
    {
        Assert.True(Evaluate<CelValue>("[1, 'two', 3u, [], {}]").TryGetList(out CelList? list));

        Assert.Equal(
            new[] { CelValueKind.Int, CelValueKind.String, CelValueKind.UInt, CelValueKind.List, CelValueKind.Map },
            list.Select(element => element.Kind));
        Assert.True(list[1].TryGetString(out string? second));
        Assert.Equal("two", second);
    }

    // The enumeration of a map literal follows the order of its text, not the hash codes of the keys.
    [Fact]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "R-5")]
    public void MapLiteral_KeepsTheOrderOfInsertion()
    {
        Assert.True(Evaluate<CelValue>("{'z': 1, 'a': 2, 7: 3, true: 4, 0u: 5, 'm': 6}").TryGetMap(out CelMap? map));

        Assert.Equal(
            new[] { CelValueKind.String, CelValueKind.String, CelValueKind.Int, CelValueKind.Bool, CelValueKind.UInt, CelValueKind.String },
            map.Select(entry => entry.Key.Kind));
        Assert.Equal(new long[] { 1, 2, 3, 4, 5, 6 }, map.Select(entry => Int64(entry.Value)));
        Assert.True(map.TryGetValue(CelValue.FromUInt64(7), out CelValue seven));
        Assert.Equal(3L, Int64(seven));
    }

    // A map literal first refuses a key of a kind that cannot be a map key, at that key, and then a key
    // equal by CEL equality to an earlier key, at the literal. Type checking accepts both literals.
    [Theory]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "F-4")]
    [InlineData("{1: 'a', 1u: 'b'}", CelErrorCode.DuplicateMapKey, 0)]
    [InlineData("{'k': 1, 'k': 2}", CelErrorCode.DuplicateMapKey, 0)]
    [InlineData("{1.0: 'a'}", CelErrorCode.UnsupportedMapKeyType, 1)]
    [InlineData("{1: 'a', 1.0: 'b'}", CelErrorCode.UnsupportedMapKeyType, 9)]
    public void MapLiteralWithAnInvalidKey_IsAnEvaluationError(string expression, CelErrorCode code, int offset)
    {
        CelError error = Assert.Single(
            environment.Compile<CelValue>(expression).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).Errors);

        Assert.Equal(code, error.Code);
        Assert.Equal(CelErrorStage.Evaluate, error.Stage);
        Assert.Equal(offset, error.Location.Offset);
    }

    // A result whose type has no row for T, or that the row does not convert, is ResultConversion at the
    // root. Both expressions pass type checking, also once the expected type is checked.
    [Fact]
    [Trait("Requirement", "+R-10")]
    public void ValueThatTDoesNotHold_IsResultConversionAtTheRoot()
    {
        CelEnvironment withDyn = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<long>("x")
            .AddVariable("d", CelType.Dyn)
            .Build()
            .GetEnvironmentOrThrow();
        var bindings = new CelBindings().Add("x", (long)int.MaxValue).Add("d", "text");

        CelEvalResult<int> tooLarge = withDyn.Compile<int>("x + 1").GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);
        CelEvalResult<long> notAnInt = withDyn.Compile<long>("  d").GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);

        Assert.Equal((CelErrorCode.ResultConversion, 2), (tooLarge.Errors[0].Code, tooLarge.Errors[0].Location.Offset));
        Assert.Equal((CelErrorCode.ResultConversion, 2), (notAnInt.Errors[0].Code, notAnInt.Errors[0].Location.Offset));
        Assert.Equal(CelErrorStage.Evaluate, Assert.Single(tooLarge.Errors).Stage);
    }

    private static T Run<T>(CelProgram<T> program, CelBindings bindings) =>
        program.Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();

    private static long Int64(CelValue value)
    {
        Assert.True(value.TryGetInt64(out long result));
        return result;
    }

    private T Evaluate<T>(string expression) => Run(environment.Compile<T>(expression).GetProgramOrThrow(), new CelBindings());
}
