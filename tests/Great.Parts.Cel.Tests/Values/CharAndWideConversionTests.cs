using System;
using System.Collections.Generic;
using System.Numerics;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// char, Int128, UInt128 and BigInteger and their Nullable<>, as variables, bindings, results, parameters and results of functions of the
// application, elements of lists, values and keys of dictionaries, and values of the simple entry. char is a string of one UTF-16 unit;
// the wide integers are int and uint, with the range check where the value is read.
[Trait("Requirement", "F-27")]
public sealed class CharAndWideConversionTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<char>("c")
        .AddVariable<char?>("mc")
        .AddVariable<Int128>("i")
        .AddVariable<UInt128>("u")
        .AddVariable<BigInteger>("b")
        .AddVariable<Int128?>("ni")
        .AddVariable<IReadOnlyList<char>>("cs")
        .AddVariable<IReadOnlyDictionary<char, long>>("byChar")
        .AddVariable<IReadOnlyList<Int128>>("wides")
        .AddVariable<IReadOnlyDictionary<string, UInt128>>("units")
        .AddFunction("isA", (char letter) => letter == 'a')
        .AddFunction("quadruple", (Int128 number) => number * 4)
        .AddFunction("next", (char letter) => (char)(letter + 1))
        .AddFunction("lone", (long number) => (char)number)
        .Build()
        .GetEnvironmentOrThrow();

    private static T Value<T>(string text, CelBindings? bindings = null) =>
        Environment.Compile<T>(text).GetProgramOrThrow().Evaluate(bindings ?? new CelBindings(), cancellationToken: TestContext.Current.CancellationToken).GetValueOrThrow();

    private static CelError EvalError<T>(string text, CelBindings? bindings = null) =>
        Assert.Single(Environment.Compile<T>(text).GetProgramOrThrow().Evaluate(bindings ?? new CelBindings(), cancellationToken: TestContext.Current.CancellationToken).Errors);

    [Fact]
    public void Char_Variable_IsAString()
    {
        CelBindings bindings = new CelBindings().Add("c", 'x').Add("mc", 'y');

        Assert.Equal("xy", Value<string>("c + 'y'", bindings));
        Assert.True(Value<bool>("type(c) == string && mc == 'y'", bindings));
        Assert.True(Value<bool>("mc == null", new CelBindings().Add("c", 'x').Add<char?>("mc", null)));
    }

    [Fact]
    public void Char_Binding_OfALoneSurrogate_IsArgumentException()
    {
        var bindings = new CelBindings();

        ArgumentException low = Assert.Throws<ArgumentException>(() => bindings.Add("c", '\uDC00'));
        Assert.Throws<ArgumentException>(() => bindings.Add("c", '\uD800'));
        Assert.Throws<ArgumentException>(() => bindings.Add<char?>("c", '\uD83D'));
        Assert.Contains("U+DC00", low.Message, StringComparison.Ordinal);
        Assert.Empty(bindings.Values);
    }

    [Fact]
    public void Char_Result_IsAStringOfExactlyOneUnit()
    {
        Assert.Equal('é', Value<char>("'é'"));
        Assert.Equal('z', Value<char?>("'z'"));
        Assert.Null(Value<char?>("null", null));
        Assert.Equal(CelErrorCode.ResultConversion, EvalError<char>("'ab'").Code);
        Assert.Equal(CelErrorCode.ResultConversion, EvalError<char>("''").Code);
        Assert.Equal(CelErrorCode.ResultConversion, EvalError<char>("'😀'").Code);
        Assert.Equal(CelErrorCode.ExpectedTypeMismatch, Assert.Single(Environment.Compile<char>("1").Errors).Code);
        Assert.Equal(CelErrorCode.ExpectedTypeMismatch, Assert.Single(Environment.Compile<UInt128>("3").Errors).Code);
    }

    [Fact]
    public void Char_Function_TakesAndReturnsAStringOfOneUnit()
    {
        Assert.True(Value<bool>("isA('a')"));
        Assert.False(Value<bool>("isA('b')"));
        Assert.Equal('b', Value<char>("next('a')"));
        Assert.Equal(CelErrorCode.ArgumentConversion, EvalError<bool>("isA('ab')").Code);
        Assert.Equal(CelErrorCode.ArgumentConversion, EvalError<bool>("isA('')").Code);
        Assert.Equal(CelErrorCode.InvalidArgument, EvalError<char>("lone(55296)").Code);
    }

    [Fact]
    public void WideIntegers_Bindings_AreIntAndUint()
    {
        CelBindings bindings = new CelBindings().Add("i", (Int128)41).Add("u", (UInt128)41).Add("b", (BigInteger)(-5)).Add<Int128?>("ni", null);

        Assert.Equal(42L, Value<long>("i + 1", bindings));
        Assert.Equal(42UL, Value<ulong>("u + 1u", bindings));
        Assert.Equal(-10L, Value<long>("b * 2", bindings));
        Assert.True(Value<bool>("type(i) == int && type(u) == uint && type(b) == int && ni == null", bindings));
    }

    [Fact]
    public void WideIntegers_Bindings_AtTheEndsOfTheRanges_Convert()
    {
        var bindings = new CelBindings();

        bindings.Add("i", (Int128)long.MinValue).Add("u", (UInt128)ulong.MaxValue).Add("b", (BigInteger)long.MaxValue).Add("ni", (Int128?)long.MaxValue);

        Assert.True(Value<bool>("i == -9223372036854775808 && u == 18446744073709551615u && b == 9223372036854775807 && ni == 9223372036854775807", bindings));
        Assert.Equal(long.MinValue, Value<long>("i", bindings));
    }

    [Fact]
    public void WideIntegers_Bindings_OutsideTheRange_AreArgumentException()
    {
        var bindings = new CelBindings();

        Assert.Throws<ArgumentException>(() => bindings.Add("i", (Int128)long.MaxValue + Int128.One));
        Assert.Throws<ArgumentException>(() => bindings.Add("i", (Int128)long.MinValue - Int128.One));
        Assert.Throws<ArgumentException>(() => bindings.Add("i", Int128.MaxValue));
        Assert.Throws<ArgumentException>(() => bindings.Add("u", (UInt128)ulong.MaxValue + UInt128.One));
        Assert.Throws<ArgumentException>(() => bindings.Add("u", UInt128.MaxValue));
        Assert.Throws<ArgumentException>(() => bindings.Add("b", (BigInteger)ulong.MaxValue));
        Assert.Throws<ArgumentException>(() => bindings.Add("b", (BigInteger)long.MaxValue + 1));
        Assert.Throws<ArgumentException>(() => bindings.Add("b", (BigInteger)long.MinValue - 1));
        Assert.Throws<ArgumentException>(() => bindings.Add("b", BigInteger.Pow(2, 200)));
        Assert.Throws<ArgumentException>(() => bindings.Add("b", -BigInteger.Pow(2, 200)));
        Assert.Throws<ArgumentException>(() => bindings.Add<Int128?>("ni", Int128.MaxValue));
        ArgumentException refused = Assert.Throws<ArgumentException>(() => bindings.Add("u", UInt128.MaxValue));
        Assert.StartsWith("The value 340282366920938463463374607431768211455 is out of the range of uint.", refused.Message, StringComparison.Ordinal);
        Assert.Empty(bindings.Values);
    }

    [Fact]
    public void WideIntegers_Results_AreExactAndKeepTheirCelType()
    {
        Assert.Equal((Int128)3, Value<Int128>("1 + 2"));
        Assert.Equal((UInt128)3, Value<UInt128>("3u"));
        Assert.Equal((BigInteger)(-7), Value<BigInteger>("-7"));
        Assert.Equal((BigInteger)long.MaxValue, Value<BigInteger>("9223372036854775807"));
        Assert.Equal((Int128)long.MinValue, Value<Int128>("-9223372036854775808"));
        Assert.Equal((UInt128)ulong.MaxValue, Value<UInt128>("18446744073709551615u"));
        Assert.Equal((Int128?)5, Value<Int128?>("5"));
        Assert.Null(Value<Int128?>("null"));
        Assert.Equal(CelErrorCode.ResultConversion, EvalError<Int128>("dyn('text')").Code);
        Assert.Equal(CelErrorCode.ResultConversion, EvalError<UInt128>("dyn(1)").Code);
    }

    [Fact]
    public void WideIntegers_FunctionResultOutsideTheRange_IsConversionRangeNamingTheFunction()
    {
        Assert.Equal(20L, Value<long>("quadruple(5)"));

        CelError error = EvalError<long>("quadruple(4611686018427387904)");

        Assert.Equal(CelErrorCode.ConversionRange, error.Code);
        Assert.Contains("quadruple", error.Message, StringComparison.Ordinal);
        Assert.Contains("18446744073709551616 is out of the range of int", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Collections_OfCharAndWideIntegers_AreListsAndMaps()
    {
        CelBindings bindings = new CelBindings()
            .Add<IReadOnlyList<char>>("cs", ['a', 'b'])
            .Add<IReadOnlyDictionary<char, long>>("byChar", new Dictionary<char, long> { ['a'] = 1, ['b'] = 2 })
            .Add<IReadOnlyList<Int128>>("wides", [1, 2])
            .Add<IReadOnlyDictionary<string, UInt128>>("units", new Dictionary<string, UInt128> { ["a"] = 7 });

        Assert.True(Value<bool>("cs == ['a', 'b'] && type(cs) == list", bindings));
        Assert.True(Value<bool>("byChar['a'] == 1 && !('ab' in byChar) && !('z' in byChar) && byChar.size() == 2", bindings));
        Assert.True(Value<bool>("wides == [1, 2] && units['a'] == 7u", bindings));
    }

    [Fact]
    public void Collections_ElementOutsideTheRange_IsAnErrorAtTheRead()
    {
        CelBindings bindings = new CelBindings()
            .Add<IReadOnlyList<char>>("cs", ['a', '\uD800'])
            .Add<IReadOnlyList<Int128>>("wides", [1, Int128.MaxValue]);

        Assert.True(Value<bool>("cs[0] == 'a' && wides[0] == 1", bindings));
        Assert.Equal(CelErrorCode.InvalidArgument, EvalError<string>("cs[1]", bindings).Code);
        Assert.Equal(CelErrorCode.ConversionRange, EvalError<long>("wides[1]", bindings).Code);
    }

    [Fact]
    public void Collections_Results_AreCopiesOrViewsOfTheRows()
    {
        IReadOnlyList<char> letters = Value<IReadOnlyList<char>>("['a', 'b']");
        IReadOnlyList<Int128> wides = Value<IReadOnlyList<Int128>>("[1, 2, 3]");
        IReadOnlyDictionary<char, long> byChar = Value<IReadOnlyDictionary<char, long>>("{'a': 1, 'b': 2}");
        IReadOnlyDictionary<string, UInt128> units = Value<IReadOnlyDictionary<string, UInt128>>("{'a': 7u}");

        Assert.Equal(2, letters.Count);
        Assert.Equal('a', letters[0]);
        Assert.Equal('b', letters[1]);
        Assert.Equal(3, wides.Count);
        Assert.Equal((Int128)3, wides[2]);
        Assert.Equal(2L, byChar['b']);
        Assert.Equal(2, byChar.Count);
        Assert.Equal((UInt128)7, units["a"]);
        Assert.Equal(CelErrorCode.ResultConversion, EvalError<IReadOnlyList<char>>("['a', 'bc']").Code);
        Assert.Equal(CelErrorCode.ResultConversion, EvalError<IReadOnlyDictionary<char, long>>("{'ab': 1}").Code);
    }

    [Fact]
    public void Indexer_ConvertsByTheRunTimeType()
    {
        var bindings = new CelBindings();

        bindings["c"] = 'q';
        bindings["i"] = (Int128)9;
        bindings["u"] = (UInt128)9;
        bindings["b"] = (BigInteger)9;
        bindings["cs"] = new[] { 'a', 'b' };

        Assert.True(Value<bool>("c == 'q' && i == 9 && u == 9u && b == 9 && cs == ['a', 'b']", bindings));
        Assert.Throws<ArgumentException>(() => bindings["c"] = '\uD800');
        Assert.Throws<ArgumentException>(() => bindings["i"] = Int128.MaxValue);
        Assert.Throws<ArgumentException>(() => bindings["u"] = UInt128.MaxValue);
        Assert.Throws<ArgumentException>(() => bindings["b"] = BigInteger.Pow(2, 64));
    }

    [Fact]
    public void SimpleEntry_ReadsCharAndWideIntegersWithoutASet()
    {
        Assert.True(CelExpression.Evaluate<bool>("v == 'z'", new Dictionary<string, object?> { ["v"] = 'z' }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(42L, CelExpression.Evaluate<long>("v + 1", new Dictionary<string, object?> { ["v"] = (Int128)41 }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(42UL, CelExpression.Evaluate<ulong>("v + 1u", new Dictionary<string, object?> { ["v"] = (UInt128)41 }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(42L, CelExpression.Evaluate<long>("v + 1", new Dictionary<string, object?> { ["v"] = (BigInteger)41 }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(CelExpression.Evaluate<bool>("v == ['a', 'b']", new Dictionary<string, object?> { ["v"] = new List<char> { 'a', 'b' } }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => CelExpression.Evaluate<bool>("true", new Dictionary<string, object?> { ["v"] = '\uDC00' }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => CelExpression.Evaluate<bool>("true", new Dictionary<string, object?> { ["v"] = Int128.MaxValue }, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SimpleEntry_AnonymousObject_ReadsCharAndWideIntegers()
    {
        Assert.True(CelExpression.Evaluate<bool>("c == 'z' && i == 41 && type(u) == uint", new { c = 'z', i = (Int128)41, u = (UInt128)7 }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(42L, CelExpression.Evaluate<long>("x + 1", new { x = (Int128)41 }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(42L, CelExpression.Evaluate<long>("x + 1", new { x = (BigInteger)41 }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(CelExpression.Evaluate<bool>("m == null && n == 'a'", new { m = (Int128?)null, n = (char?)'a' }, cancellationToken: TestContext.Current.CancellationToken));
    }
}
