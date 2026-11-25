using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Generated;

// The generator of call sites binds a property whose type is a row of the type mapping that cannot fail with the factory of CelValue,
// where it used to call the generic Add<T>. For each such row the value of the simple entry must be the value that an environment
// with AddVariable<T> and Add<T> gives, at the values of the limits of the type: the smallest, the largest, zero, null, an empty
// value. The anonymous objects are written at each call, since the generator reads the static type there.
[Trait("Requirement", "U-7")]
[Trait("Requirement", "S-1")]
public sealed class GeneratedDirectTranslationTests
{
    [Fact]
    public void Booleans_AreTheValueOfAdd()
    {
        foreach (bool value in new[] { true, false })
        {
            Assert.Equal(ViaAdd(value), CelExpression.Evaluate<CelValue>("p", new { p = value }, cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public void SignedIntegers_AreTheValueOfAdd()
    {
        foreach (sbyte value in new sbyte[] { sbyte.MinValue, 0, sbyte.MaxValue })
        {
            Assert.Equal(ViaAdd(value), CelExpression.Evaluate<CelValue>("p", new { p = value }, cancellationToken: TestContext.Current.CancellationToken));
        }

        foreach (short value in new short[] { short.MinValue, 0, short.MaxValue })
        {
            Assert.Equal(ViaAdd(value), CelExpression.Evaluate<CelValue>("p", new { p = value }, cancellationToken: TestContext.Current.CancellationToken));
        }

        foreach (int value in new[] { int.MinValue, 0, int.MaxValue })
        {
            Assert.Equal(ViaAdd(value), CelExpression.Evaluate<CelValue>("p", new { p = value }, cancellationToken: TestContext.Current.CancellationToken));
        }

        foreach (long value in new[] { long.MinValue, -1L, 0L, long.MaxValue })
        {
            Assert.Equal(ViaAdd(value), CelExpression.Evaluate<CelValue>("p", new { p = value }, cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public void UnsignedIntegers_AreTheValueOfAdd()
    {
        foreach (byte value in new byte[] { 0, byte.MaxValue })
        {
            Assert.Equal(ViaAdd(value), CelExpression.Evaluate<CelValue>("p", new { p = value }, cancellationToken: TestContext.Current.CancellationToken));
        }

        foreach (ushort value in new ushort[] { 0, ushort.MaxValue })
        {
            Assert.Equal(ViaAdd(value), CelExpression.Evaluate<CelValue>("p", new { p = value }, cancellationToken: TestContext.Current.CancellationToken));
        }

        foreach (uint value in new[] { 0u, uint.MaxValue })
        {
            Assert.Equal(ViaAdd(value), CelExpression.Evaluate<CelValue>("p", new { p = value }, cancellationToken: TestContext.Current.CancellationToken));
        }

        foreach (ulong value in new[] { 0UL, ulong.MaxValue })
        {
            Assert.Equal(ViaAdd(value), CelExpression.Evaluate<CelValue>("p", new { p = value }, cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public void Doubles_AreTheValueOfAdd()
    {
        foreach (double value in new[] { 0.0, -0.0, 1.5, double.MaxValue, double.MinValue, double.Epsilon, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Equal(ViaAdd(value), CelExpression.Evaluate<CelValue>("p", new { p = value }, cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public void Strings_AreTheValueOfAdd()
    {
        foreach (string value in new[] { string.Empty, "x", "caf" + (char)0xE9 })
        {
            Assert.Equal(ViaAdd(value), CelExpression.Evaluate<CelValue>("p", new { p = value }, cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    // A null reference is bound as the value null, which does not fit the declared type string: the read of the variable is a mismatch
    // of the binding, the same one on both paths.
    [Fact]
    public void NullString_IsTheMismatchOfAdd()
    {
        string? value = null;

        AssertSameMismatch(
            () => ViaAdd(value),
            () => CelExpression.Evaluate<CelValue>("p", new { p = value }, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Guids_AreTheValueOfAdd()
    {
        foreach (Guid value in new[] { Guid.Empty, new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"), new Guid("01234567-89ab-cdef-0123-456789abcdef") })
        {
            Assert.Equal(ViaAdd(value), CelExpression.Evaluate<CelValue>("p", new { p = value }, cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public void ListsAndMaps_AreTheValueOfAdd()
    {
        CelList list = CelList.Create(CelValue.FromInt64(1), CelValue.FromString("a"));
        CelMap map = CelMap.Create(new List<KeyValuePair<CelValue, CelValue>>());

        Assert.Equal(ViaAdd(list), CelExpression.Evaluate<CelValue>("p", new { p = list }, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(ViaAdd(map), CelExpression.Evaluate<CelValue>("p", new { p = map }, cancellationToken: TestContext.Current.CancellationToken));
    }

    // The value null does not fit list(dyn) and map(dyn, dyn): the read of the variable is the same mismatch of the binding on both paths.
    [Fact]
    public void NullListAndMap_AreTheMismatchOfAdd()
    {
        AssertSameMismatch(
            () => ViaAdd((CelList)null!),
            () => CelExpression.Evaluate<CelValue>("p", new { p = (CelList)null! }, cancellationToken: TestContext.Current.CancellationToken));
        AssertSameMismatch(
            () => ViaAdd((CelMap)null!),
            () => CelExpression.Evaluate<CelValue>("p", new { p = (CelMap)null! }, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void NullableValues_AreTheValueOfAdd_AndNoValueIsTheNullValue()
    {
        foreach (long? value in new long?[] { null, long.MinValue, 0, long.MaxValue })
        {
            Assert.Equal(ViaAdd(value), CelExpression.Evaluate<CelValue>("p", new { p = value }, cancellationToken: TestContext.Current.CancellationToken));
        }

        foreach (int? value in new int?[] { null, int.MinValue, int.MaxValue })
        {
            Assert.Equal(ViaAdd(value), CelExpression.Evaluate<CelValue>("p", new { p = value }, cancellationToken: TestContext.Current.CancellationToken));
        }

        foreach (ulong? value in new ulong?[] { null, 0UL, ulong.MaxValue })
        {
            Assert.Equal(ViaAdd(value), CelExpression.Evaluate<CelValue>("p", new { p = value }, cancellationToken: TestContext.Current.CancellationToken));
        }

        foreach (bool? value in new bool?[] { null, true, false })
        {
            Assert.Equal(ViaAdd(value), CelExpression.Evaluate<CelValue>("p", new { p = value }, cancellationToken: TestContext.Current.CancellationToken));
        }

        foreach (double? value in new double?[] { null, 0.0, double.NaN, double.MaxValue })
        {
            Assert.Equal(ViaAdd(value), CelExpression.Evaluate<CelValue>("p", new { p = value }, cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    // Both paths fail with BindingTypeMismatch and the same text.
    private static void AssertSameMismatch(Func<CelValue> viaAdd, Func<CelValue> viaDirect)
    {
        CelException expected = Assert.Throws<CelException>(() => { _ = viaAdd(); });
        CelException actual = Assert.Throws<CelException>(() => { _ = viaDirect(); });

        Assert.Equal(CelErrorCode.BindingTypeMismatch, Assert.Single(expected.Errors).Code);
        Assert.Equal(CelErrorCode.BindingTypeMismatch, Assert.Single(actual.Errors).Code);
        Assert.Equal(expected.Message, actual.Message);
    }

    // The value of an environment of one variable that Add<T> binds: the other path of the same row.
    private static CelValue ViaAdd<T>(T value)
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<T>("p")
            .Build()
            .GetEnvironmentOrThrow();
        return environment
            .Compile<CelValue>("p")
            .GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("p", value), cancellationToken: TestContext.Current.CancellationToken)
            .GetValueOrThrow();
    }
}
