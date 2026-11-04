using System;
using System.IO;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Values;

// The input rows of the type mapping table for values: the CEL type that AddVariable<T> declares for a .NET
// type, and the value that CelBindings.Add<T> gives for a .NET value. Each case declares a variable v of the
// .NET type, binds a value and evaluates "v".
public sealed class ConversionTests
{
    private enum Wide : ulong
    {
        Small = 7,
        Huge = ulong.MaxValue,
    }

    [Fact]
    [Trait("Requirement", "F-3")]
    public void Integers_AreIntOrUIntExactly()
    {
        AssertRead(CelType.Int, CelValue.FromInt64(long.MinValue), long.MinValue);
        AssertRead(CelType.Int, CelValue.FromInt64(-5), -5);
        AssertRead(CelType.Int, CelValue.FromInt64(-5), (short)-5);
        AssertRead(CelType.Int, CelValue.FromInt64(-5), (sbyte)-5);
        AssertRead(CelType.Int, CelValue.FromInt64(-5), (nint)(-5));
        AssertRead(CelType.UInt, CelValue.FromUInt64(ulong.MaxValue), ulong.MaxValue);
        AssertRead(CelType.UInt, CelValue.FromUInt64(200), 200u);
        AssertRead(CelType.UInt, CelValue.FromUInt64(200), (ushort)200);
        AssertRead(CelType.UInt, CelValue.FromUInt64(200), (byte)200);
        AssertRead(CelType.UInt, CelValue.FromUInt64(200), (nuint)200);
    }

    [Fact]
    [Trait("Requirement", "F-3")]
    public void FloatingPoint_IsDoubleByExactWidening()
    {
        AssertRead(CelType.Double, CelValue.FromDouble(0.1), 0.1);
        AssertRead(CelType.Double, CelValue.FromDouble(0.1f), 0.1f);
        AssertRead(CelType.Double, CelValue.FromDouble(0.5), (Half)0.5);
        AssertRead(CelType.Double, CelValue.FromDouble(double.NegativeInfinity), double.NegativeInfinity);
    }

    // string and bytes are primitive types: they take no null, so a null input is a binding of another kind.
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "R-5")]
    public void NullInputOfStringAndBytes_IsBindingTypeMismatch()
    {
        foreach ((CelEvalResult<CelValue> result, string declared) in new[] { (Read((string?)null).Result, "string"), (Read((byte[]?)null).Result, "bytes") })
        {
            CelError error = Assert.Single(result.Errors);

            Assert.Equal((CelErrorCode.BindingTypeMismatch, CelErrorStage.Evaluate, 0), (error.Code, error.Stage, error.Location.Offset));
            Assert.Equal(
                "value of variable 'v' has type 'null_type', which does not match the declared type '" + declared + "'",
                error.Message);
        }
    }

    // A Guid is a string in the format D, lower case, whatever the culture.
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "R-5")]
    public void OtherScalars_AreTheirCelValues()
    {
        AssertRead(CelType.Bool, CelValue.FromBoolean(true), true);
        AssertRead(CelType.String, CelValue.FromString("é"), "é");
        AssertRead(CelType.String, CelValue.FromString("0f8fad5b-d9cb-469f-a165-70867728950e"), Guid.Parse("0F8FAD5B-D9CB-469F-A165-70867728950E"));
        AssertRead(CelType.Bytes, CelValue.FromBytes(new byte[] { 0, 255 }), new byte[] { 0, 255 });
        AssertRead(CelType.Bytes, CelValue.FromBytes(new byte[] { 1 }), new ReadOnlyMemory<byte>(new byte[] { 1 }));
    }

    // An enumeration is int by its underlying value, [Flags] included; above the range of int the read of the
    // variable is ConversionRange.
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "F-4")]
    public void Enumeration_IsIntByItsUnderlyingValue()
    {
        AssertRead(CelType.Int, CelValue.FromInt64(5), DayOfWeek.Friday);
        AssertRead(CelType.Int, CelValue.FromInt64(3), FileAccess.ReadWrite);
        AssertRead(CelType.Int, CelValue.FromInt64(7), Wide.Small);

        CelError error = Assert.Single(Read(Wide.Huge).Result.Errors);

        Assert.Equal((CelErrorCode.ConversionRange, CelErrorStage.Evaluate, 0), (error.Code, error.Stage, error.Location.Offset));
    }

    [Fact]
    [Trait("Requirement", "F-3")]
    public void NullableValueType_IsTheWrapperOfItsType()
    {
        AssertRead(CelType.WrapperOf(CelType.Int), CelValue.FromInt64(5), (int?)5);
        AssertRead(CelType.WrapperOf(CelType.Int), CelValue.Null, (long?)null);
        AssertRead(CelType.WrapperOf(CelType.UInt), CelValue.Null, (ulong?)null);
        AssertRead(CelType.WrapperOf(CelType.Double), CelValue.FromDouble(1.5), (double?)1.5);
        AssertRead(CelType.WrapperOf(CelType.Bool), CelValue.FromBoolean(false), (bool?)false);
    }

    // CelValue and object are dyn; CelList and CelMap are a list and a map of dyn, all taken as they are.
    [Fact]
    [Trait("Requirement", "F-3")]
    public void CelValuesAndObjects_AreTakenAsTheyAre()
    {
        CelList list = CelList.Create(CelValue.FromInt64(1));
        CelMap map = CelMap.Create([]);

        AssertRead(CelType.Dyn, CelValue.FromString("a"), CelValue.FromString("a"));
        AssertRead(CelType.ListOf(CelType.Dyn), CelValue.FromList(list), list);
        AssertRead(CelType.MapOf(CelType.Dyn, CelType.Dyn), CelValue.FromMap(map), map);
        AssertRead(CelType.Dyn, CelValue.FromInt64(5), (object)5L);
        AssertRead(CelType.Dyn, CelValue.FromUInt64(5), (object)(byte)5);
        AssertRead(CelType.Dyn, CelValue.FromList(list), (object)list);
        AssertRead(CelType.Dyn, CelValue.FromString("a"), (object)CelValue.FromString("a"));
    }

    private static void AssertRead<T>(CelType type, CelValue value, T input)
    {
        (CelType declared, CelEvalResult<CelValue> result) = Read(input);

        Assert.Equal(type, declared);
        Assert.Equal(value, result.GetValueOrThrow());
    }

    private static (CelType Type, CelEvalResult<CelValue> Result) Read<T>(T input)
    {
        CelProgram<CelValue> program = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<T>("v")
            .Build()
            .GetEnvironmentOrThrow()
            .Compile<CelValue>("v")
            .GetProgramOrThrow();

        return (program.ResultType, program.Evaluate(new CelBindings().Add("v", input), cancellationToken: CancellationToken.None));
    }
}
