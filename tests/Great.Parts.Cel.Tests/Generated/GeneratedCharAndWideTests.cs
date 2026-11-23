using System;
using System.Numerics;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Characters;
using Xunit;
using static Great.Parts.Cel.Tests.Examples.Generated.GeneratedExample;

namespace Great.Parts.Cel.Tests.Generated;

// char and the integers wider than 64 bits as members of a type that the generator described (the samples Letters and BigNumbers):
// what each read gives at and around the ends of the ranges, in members, elements, values and keys.
public sealed class GeneratedCharAndWideTests
{
    private static readonly CelEnvironment Environment = Builder(CharactersContext.Default)
        .AddVariable<Letters>("l")
        .AddVariable<BigNumbers>("n")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    [Trait("Requirement", "F-27")]
    public void Char_Member_IsAStringOfOneUnit()
    {
        CelBindings bindings = new CelBindings().Add("l", new Letters { Letter = 'A', Mark = 'x', Row = ['a', 'b'], Chars = ['c'] });

        Assert.True(Evaluate<bool>(Environment, "l.letter == 'A' && type(l.letter) == string", bindings));
        Assert.True(Evaluate<bool>(Environment, "has(l.mark) && l.mark == 'x'", bindings));
        Assert.True(Evaluate<bool>(Environment, "l.row == ['a', 'b'] && l.chars == ['c']", bindings));
    }

    [Fact]
    [Trait("Requirement", "F-27")]
    public void Char_NullableMember_IsAWrapperOfString()
    {
        CelBindings bindings = new CelBindings().Add("l", new Letters());

        Assert.True(Evaluate<bool>(Environment, "!has(l.mark) && l.mark == null", bindings));
    }

    [Fact]
    [Trait("Requirement", "F-27")]
    public void Char_LoneSurrogate_IsInvalidArgumentAtTheRead()
    {
        CelBindings bindings = new CelBindings().Add("l", new Letters { Letter = '\uD800', Row = ['a', '\uDC00'] });

        CelError member = EvaluationError(Environment, "l.letter", bindings);
        CelError element = EvaluationError(Environment, "l.row[1]", bindings);

        Assert.Equal(CelErrorCode.InvalidArgument, member.Code);
        Assert.Contains("samples.Letters.letter", member.Message, StringComparison.Ordinal);
        Assert.Contains("U+D800", member.Message, StringComparison.Ordinal);
        Assert.Equal(CelErrorCode.InvalidArgument, element.Code);
        Assert.True(Evaluate<bool>(Environment, "l.row[0] == 'a'", bindings));
    }

    [Fact]
    [Trait("Requirement", "F-27")]
    public void Char_DictionaryKeyAndSet_AreStringsOfOneUnit()
    {
        var letters = new Letters();
        letters.Counts['a'] = 1;
        letters.Counts['b'] = 2;
        letters.Marks.Add('z');
        letters.Marks.Add('m');
        CelBindings bindings = new CelBindings().Add("l", letters);

        Assert.True(Evaluate<bool>(Environment, "l.counts['a'] == 1 && l.counts['b'] == 2 && l.counts.size() == 2", bindings));
        Assert.True(Evaluate<bool>(Environment, "!('c' in l.counts) && !('ab' in l.counts) && !('' in l.counts)", bindings));
        Assert.True(Evaluate<bool>(Environment, "l.counts.all(k, size(k) == 1)", bindings));
        Assert.True(Evaluate<bool>(Environment, "l.marks == ['m', 'z']", bindings));
    }

    [Fact]
    [Trait("Requirement", "F-27")]
    public void WideIntegers_Members_AreIntAndUint()
    {
        CelBindings bindings = new CelBindings().Add("n", new BigNumbers { I = 42, U = (UInt128)42, B = -5, N = null, Row = [1, 2], Table = { ["a"] = (UInt128)7 } });

        Assert.True(Evaluate<bool>(Environment, "n.i + 1 == 43 && type(n.i) == int", bindings));
        Assert.True(Evaluate<bool>(Environment, "n.u + 1u == 43u && type(n.u) == uint", bindings));
        Assert.True(Evaluate<bool>(Environment, "n.b * 2 == -10 && type(n.b) == int", bindings));
        Assert.True(Evaluate<bool>(Environment, "!has(n.n) && n.n == null", bindings));
        Assert.True(Evaluate<bool>(Environment, "n.row == [1, 2] && n.table['a'] == 7u", bindings));
    }

    [Fact]
    [Trait("Requirement", "F-27")]
    public void WideIntegers_AtTheEndsOfTheRanges_AreRead()
    {
        CelBindings bindings = new CelBindings().Add(
            "n",
            new BigNumbers { I = long.MinValue, U = ulong.MaxValue, B = long.MaxValue, N = long.MaxValue });

        Assert.True(Evaluate<bool>(Environment, "n.i == -9223372036854775808 && n.u == 18446744073709551615u", bindings));
        Assert.True(Evaluate<bool>(Environment, "n.b == 9223372036854775807 && n.n == 9223372036854775807", bindings));
    }

    [Fact]
    [Trait("Requirement", "F-27")]
    public void WideIntegers_OutsideTheRange_AreConversionRangeAtTheRead()
    {
        var big = new BigNumbers
        {
            I = Int128.MaxValue,
            U = (UInt128)ulong.MaxValue + UInt128.One,
            B = ulong.MaxValue,
            N = (Int128)long.MinValue - Int128.One,
        };
        CelBindings bindings = new CelBindings().Add("n", big);

        AssertRange(EvaluationError(Environment, "n.i", bindings), "170141183460469231731687303715884105727 is out of the range of int", "i");
        AssertRange(EvaluationError(Environment, "n.u", bindings), "18446744073709551616 is out of the range of uint", "u");
        AssertRange(EvaluationError(Environment, "n.b", bindings), "18446744073709551615 is out of the range of int", "b");
        AssertRange(EvaluationError(Environment, "n.n", bindings), "-9223372036854775809 is out of the range of int", "n");

        big.B = (BigInteger)long.MaxValue + 1;
        AssertRange(EvaluationError(Environment, "n.b", bindings), "9223372036854775808 is out of the range of int", "b");
        big.B = (BigInteger)long.MinValue - 1;
        AssertRange(EvaluationError(Environment, "n.b", bindings), "-9223372036854775809 is out of the range of int", "b");
        big.B = BigInteger.Pow(2, 200);
        AssertRange(EvaluationError(Environment, "n.b", bindings), "1606938044258990275541962092341162602522202993782792835301376 is out of the range of int", "b");
    }

    [Fact]
    [Trait("Requirement", "F-27")]
    public void WideIntegers_InAListAndInAMap_AreConversionRangeAtTheElement()
    {
        var big = new BigNumbers { Row = [1, Int128.MaxValue], Table = { ["a"] = (UInt128)7, ["b"] = (UInt128)ulong.MaxValue + UInt128.One } };
        CelBindings bindings = new CelBindings().Add("n", big);

        Assert.True(Evaluate<bool>(Environment, "n.row[0] == 1 && n.table['a'] == 7u", bindings));
        AssertRange(EvaluationError(Environment, "n.row[1]", bindings), "170141183460469231731687303715884105727 is out of the range of int", "row");
        AssertRange(EvaluationError(Environment, "n.table['b']", bindings), "18446744073709551616 is out of the range of uint", "table");
    }

    private static void AssertRange(CelError error, string message, string subject)
    {
        Assert.Equal(CelErrorCode.ConversionRange, error.Code);
        Assert.Equal(message, error.Message);
        Assert.Equal(subject, error.Subject);
    }
}
