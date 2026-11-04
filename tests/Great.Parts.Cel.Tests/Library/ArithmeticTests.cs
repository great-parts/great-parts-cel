using System.Globalization;
using System.Numerics;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Library;

// The implementations of the arithmetic of int, uint and double, called directly with a call site. The
// integers are checked against BigInteger on every pair of values at and near the ends of their ranges.
public sealed class ArithmeticTests
{
    private static readonly long[] IntEdges = [long.MinValue, long.MinValue + 1, -4_294_967_296, -3, -1, 0, 1, 2, 3_037_000_500, long.MaxValue - 1, long.MaxValue];

    private static readonly ulong[] UIntEdges = [0, 1, 2, 4_294_967_296, (ulong)long.MaxValue, (ulong)long.MaxValue + 1, ulong.MaxValue - 1, ulong.MaxValue];

    private static readonly LineTable Lines = new("1\n+ 2");

    private static readonly CallSite Site = new("_+_", 2, Lines);

    [Fact]
    public static void IntOperations_OfEveryPairOfEdges_AreExactOrOverflow()
    {
        foreach (long left in IntEdges)
        {
            foreach (long right in IntEdges)
            {
                AssertInt((BigInteger)left + right, Arithmetic.AddInt64([Int(left), Int(right)], Site));
                AssertInt((BigInteger)left - right, Arithmetic.SubtractInt64([Int(left), Int(right)], Site));
                AssertInt((BigInteger)left * right, Arithmetic.MultiplyInt64([Int(left), Int(right)], Site));
                if (right != 0)
                {
                    // BigInteger truncates toward zero and gives the remainder the sign of the dividend, as CEL;
                    // the remainder of -2^63 by -1 belongs to the quotient 2^63, which overflows.
                    AssertInt(BigInteger.Divide(left, right), Arithmetic.DivideInt64([Int(left), Int(right)], Site));
                    AssertInt(
                        left == long.MinValue && right == -1 ? BigInteger.Pow(2, 63) : BigInteger.Remainder(left, right),
                        Arithmetic.ModuloInt64([Int(left), Int(right)], Site));
                }
            }

            AssertInt(-(BigInteger)left, Arithmetic.NegateInt64([Int(left)], Site));
        }
    }

    [Fact]
    public static void UIntOperations_OfEveryPairOfEdges_AreExactOrOverflow()
    {
        foreach (ulong left in UIntEdges)
        {
            foreach (ulong right in UIntEdges)
            {
                AssertUInt((BigInteger)left + right, Arithmetic.AddUInt64([UInt(left), UInt(right)], Site));
                AssertUInt((BigInteger)left - right, Arithmetic.SubtractUInt64([UInt(left), UInt(right)], Site));
                AssertUInt((BigInteger)left * right, Arithmetic.MultiplyUInt64([UInt(left), UInt(right)], Site));
                if (right != 0)
                {
                    AssertUInt(BigInteger.Divide(left, right), Arithmetic.DivideUInt64([UInt(left), UInt(right)], Site));
                    AssertUInt(BigInteger.Remainder(left, right), Arithmetic.ModuloUInt64([UInt(left), UInt(right)], Site));
                }
            }
        }
    }

    [Theory]
    [InlineData(-7L, 2L, -3L, -1L)]
    [InlineData(7L, -2L, -3L, 1L)]
    [InlineData(-7L, -2L, 3L, -1L)]
    [InlineData(long.MinValue, 1L, long.MinValue, 0L)]
    public static void IntDivision_TruncatesTowardZero_AndTheRemainderHasTheSignOfTheDividend(long dividend, long divisor, long quotient, long remainder)
    {
        Assert.Equal(quotient, IntOf(Arithmetic.DivideInt64([Int(dividend), Int(divisor)], Site)));
        Assert.Equal(remainder, IntOf(Arithmetic.ModuloInt64([Int(dividend), Int(divisor)], Site)));
    }

    [Fact]
    public static void Overflow_HasTheTextOfItsKind()
    {
        AssertError(CelErrorCode.Overflow, "integer overflow", Arithmetic.MultiplyInt64([Int(long.MaxValue), Int(2)], Site));
        AssertError(CelErrorCode.Overflow, "integer overflow", Arithmetic.SubtractInt64([Int(long.MinValue), Int(1)], Site));
        AssertError(CelErrorCode.Overflow, "integer overflow", Arithmetic.DivideInt64([Int(long.MinValue), Int(-1)], Site));
        AssertError(CelErrorCode.Overflow, "integer overflow", Arithmetic.ModuloInt64([Int(long.MinValue), Int(-1)], Site));
        AssertError(CelErrorCode.Overflow, "integer overflow", Arithmetic.NegateInt64([Int(long.MinValue)], Site));
        AssertError(CelErrorCode.Overflow, "unsigned integer overflow", Arithmetic.AddUInt64([UInt(ulong.MaxValue), UInt(1)], Site));
        AssertError(CelErrorCode.Overflow, "unsigned integer overflow", Arithmetic.SubtractUInt64([UInt(1), UInt(2)], Site));
        AssertError(CelErrorCode.Overflow, "unsigned integer overflow", Arithmetic.MultiplyUInt64([UInt(1UL << 32), UInt(1UL << 32)], Site));
    }

    [Fact]
    public static void ZeroDivisor_IsDivisionByZeroOrModulusByZero()
    {
        AssertError(CelErrorCode.DivisionByZero, "division by zero", Arithmetic.DivideInt64([Int(long.MinValue), Int(0)], Site));
        AssertError(CelErrorCode.DivisionByZero, "division by zero", Arithmetic.DivideUInt64([UInt(0), UInt(0)], Site));
        AssertError(CelErrorCode.ModulusByZero, "modulus by zero", Arithmetic.ModuloInt64([Int(5), Int(0)], Site));
        AssertError(CelErrorCode.ModulusByZero, "modulus by zero", Arithmetic.ModuloUInt64([UInt(ulong.MaxValue), UInt(0)], Site));
    }

    [Fact]
    public static void DoubleOperations_AreIeee754WithoutErrors()
    {
        Assert.Equal(0.30000000000000004, DoubleOf(Arithmetic.AddDouble([Double(0.1), Double(0.2)], Site)));
        Assert.Equal(-0.5, DoubleOf(Arithmetic.SubtractDouble([Double(0.25), Double(0.75)], Site)));
        Assert.Equal(double.PositiveInfinity, DoubleOf(Arithmetic.MultiplyDouble([Double(double.MaxValue), Double(2)], Site)));
        Assert.Equal(double.NegativeInfinity, DoubleOf(Arithmetic.DivideDouble([Double(-3), Double(0)], Site)));
        Assert.True(double.IsNaN(DoubleOf(Arithmetic.DivideDouble([Double(0), Double(0)], Site))));
        Assert.True(double.IsNaN(DoubleOf(Arithmetic.AddDouble([Double(double.PositiveInfinity), Double(double.NegativeInfinity)], Site))));
        Assert.True(double.IsNegative(DoubleOf(Arithmetic.NegateDouble([Double(0)], Site))));
    }

    // The call site gives the error the stage Evaluate and the position of the call: offset 2 of "1\n+ 2"
    // is the start of line 2; offset 7 of "7 /\n 0 / 1" is the second division, on line 2.
    [Fact]
    public static void Error_HasThePositionOfTheCall()
    {
        CelError first = Error(Arithmetic.AddInt64([Int(long.MaxValue), Int(1)], Site));
        CelError second = Error(Arithmetic.DivideInt64([Int(7), Int(0)], new CallSite("_/_", 7, new LineTable("7 /\n 0 / 1"))));

        Assert.Equal((CelErrorStage.Evaluate, 2, 2, 1), (first.Stage, first.Location.Offset, first.Location.Line, first.Location.Column));
        Assert.Equal((CelErrorStage.Evaluate, 7, 2, 4), (second.Stage, second.Location.Offset, second.Location.Line, second.Location.Column));
        Assert.Null(first.SourceLocation);
        Assert.Null(first.Exception);
    }

    [Fact]
    public static void ArgumentOfAnotherKind_IsEvalNoMatchingOverloadWithTheKindsOfTheValues()
    {
        CallSite divide = new("_/_", 0, Lines);
        CallSite negate = new("-_", 0, Lines);

        AssertError(CelErrorCode.EvalNoMatchingOverload, "no such overload: _+_(int, string)", Arithmetic.AddInt64([Int(1), CelValue.FromString("a")], Site));
        AssertError(CelErrorCode.EvalNoMatchingOverload, "no such overload: _+_(int, uint)", Arithmetic.AddUInt64([Int(1), UInt(1)], Site));
        AssertError(CelErrorCode.EvalNoMatchingOverload, "no such overload: _+_(double, int)", Arithmetic.AddDouble([Double(1), Int(1)], Site));
        AssertError(CelErrorCode.EvalNoMatchingOverload, "no such overload: _/_(null_type, bool)", Arithmetic.DivideInt64([CelValue.Null, CelValue.FromBoolean(true)], divide));
        AssertError(CelErrorCode.EvalNoMatchingOverload, "no such overload: -_(bytes)", Arithmetic.NegateInt64([CelValue.FromBytes(new byte[] { 1 })], negate));
        AssertError(CelErrorCode.EvalNoMatchingOverload, "no such overload: -_(int)", Arithmetic.NegateDouble([Int(1)], negate));
    }

    private static CelValue Int(long value) => CelValue.FromInt64(value);

    private static CelValue UInt(ulong value) => CelValue.FromUInt64(value);

    private static CelValue Double(double value) => CelValue.FromDouble(value);

    private static long IntOf(CelValue value)
    {
        Assert.True(value.TryGetInt64(out long result), value.Kind.ToString());
        return result;
    }

    private static double DoubleOf(CelValue value)
    {
        Assert.True(value.TryGetDouble(out double result), value.Kind.ToString());
        return result;
    }

    private static void AssertInt(BigInteger exact, CelValue result)
    {
        if (exact >= long.MinValue && exact <= long.MaxValue)
        {
            Assert.True(result.TryGetInt64(out long value), exact.ToString(CultureInfo.InvariantCulture));
            Assert.Equal((long)exact, value);
        }
        else
        {
            AssertError(CelErrorCode.Overflow, "integer overflow", result);
        }
    }

    private static void AssertUInt(BigInteger exact, CelValue result)
    {
        if (exact >= ulong.MinValue && exact <= ulong.MaxValue)
        {
            Assert.True(result.TryGetUInt64(out ulong value), exact.ToString(CultureInfo.InvariantCulture));
            Assert.Equal((ulong)exact, value);
        }
        else
        {
            AssertError(CelErrorCode.Overflow, "unsigned integer overflow", result);
        }
    }

    private static void AssertError(CelErrorCode code, string message, CelValue result)
    {
        CelError error = Error(result);
        Assert.Equal((code, message), (error.Code, error.Message));
    }

    private static CelError Error(CelValue result)
    {
        Assert.True(result.TryGetError(out CelError? error), result.Kind.ToString());
        return error;
    }
}
