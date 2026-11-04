using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Values;

// Map keys are int, uint, bool and string. An int and a uint of the same value are one key; a double finds
// the key of its value only when it converts without loss — to int inside the open range of int, else to uint
// below 2^64; any other kind finds nothing. Every value is the one cel-go v0.32.0 gives.
public sealed class MapKeyTests
{
    private readonly CelEnvironment environment =
        new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    [Theory]
    [Trait("Requirement", "F-1")]
    [InlineData("{1: 'a'}[dyn(1u)]")]
    [InlineData("{1u: 'a'}[dyn(1)]")]
    [InlineData("{1: 'a'}[dyn(1.0)]")]
    [InlineData("{1u: 'a'}[dyn(1.0)]")]
    [InlineData("{0: 'a'}[dyn(-0.0)]")]
    [InlineData("{9223372036854775807: 'a'}[dyn(9223372036854775807u)]")]
    [InlineData("{9223372036854774784: 'a'}[dyn(9223372036854774784.0)]")]
    [InlineData("{true: 'a'}[true]")]
    [InlineData("{'k': 'a'}['k']")]
    public void KeyOfAnotherNumericKindWithTheSameValue_FindsTheEntry(string expression)
    {
        Assert.Equal("a", Evaluate<string>(expression));
    }

    [Theory]
    [Trait("Requirement", "F-1")]
    [InlineData("dyn(1u) in {1: 'a'}", true)]
    [InlineData("dyn(2.0) in {2u: 'a'}", true)]
    [InlineData("dyn(1.5) in {1: 'a'}", false)]
    [InlineData("dyn(-1) in {18446744073709551615u: 'a'}", false)]
    [InlineData("dyn(18446744073709551615u) in {-1: 'a'}", false)]
    [InlineData("dyn(9223372036854775807.0) in {9223372036854775807: 'a'}", false)]
    [InlineData("dyn(-9223372036854775808.0) in {-9223372036854775808: 'a'}", false)]
    [InlineData("dyn(18446744073709551615.0) in {18446744073709551615u: 'a'}", false)]
    [InlineData("dyn(1e300) in {1: 'a'}", false)]
    [InlineData("dyn(0.0 / 0.0) in {1: 'a'}", false)]
    [InlineData("dyn('1') in {1: 'a'}", false)]
    [InlineData("dyn(true) in {1: 'a'}", false)]
    [InlineData("dyn(1) in {true: 'a'}", false)]
    public void KeyLookup_FindsOnlyAKeyOfTheSameValue(string expression, bool expected)
    {
        Assert.Equal(expected, Evaluate<bool>(expression));
    }

    // CelMap.TryGetValue uses the same key lookup.
    [Fact]
    [Trait("Requirement", "F-1")]
    public void TryGetValue_UsesTheKeyLookupOfCel()
    {
        CelMap map = CelMap.Create([Entry(CelValue.FromInt64(1), "int"), Entry(CelValue.FromString("1"), "string")]);

        Assert.Equal("int", Text(map, CelValue.FromUInt64(1)));
        Assert.Equal("int", Text(map, CelValue.FromDouble(1.0)));
        Assert.Equal("string", Text(map, CelValue.FromString("1")));
        Assert.False(map.TryGetValue(CelValue.FromDouble(1.5), out _));
        Assert.False(map.TryGetValue(CelValue.FromBoolean(true), out _));
        Assert.False(map.TryGetValue(CelValue.Null, out _));
    }

    // CelMap.Create refuses a key of a kind that cannot be a map key, then a key equal to one already present.
    [Fact]
    [Trait("Requirement", "F-1")]
    public void CreateWithAnInvalidOrRepeatedKey_IsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => CelMap.Create([Entry(CelValue.FromInt64(1), "a"), Entry(CelValue.FromUInt64(1), "b")]));
        Assert.Throws<ArgumentException>(() => CelMap.Create([Entry(CelValue.FromString("k"), "a"), Entry(CelValue.FromString("k"), "b")]));
        Assert.Throws<ArgumentException>(() => CelMap.Create([Entry(CelValue.FromDouble(1.0), "a")]));
        Assert.Throws<ArgumentException>(() => CelMap.Create([Entry(CelValue.Null, "a")]));
        Assert.Throws<ArgumentException>(() => CelMap.Create([Entry(CelValue.FromList(CelList.Create()), "a")]));
    }

    private static KeyValuePair<CelValue, CelValue> Entry(CelValue key, string value) => new(key, CelValue.FromString(value));

    private static string? Text(CelMap map, CelValue key)
    {
        Assert.True(map.TryGetValue(key, out CelValue value));
        Assert.True(value.TryGetString(out string? text));
        return text;
    }

    private T Evaluate<T>(string expression) =>
        environment.Compile<T>(expression).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow();
}
