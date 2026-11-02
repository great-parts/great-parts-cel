using System.Numerics;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Library;

// The implementations of add_int64, divide_int64 and negate_int64, called directly with a call site.
public sealed class IntArithmeticTests
{
    // Values at and near the ends of the range of int, and around zero.
    private static readonly long[] Edges = [long.MinValue, long.MinValue + 1, -3, -1, 0, 1, 2, long.MaxValue - 1, long.MaxValue];

    private static readonly LineTable Lines = new("1\n+ 2");

    private static readonly CallSite Site = new("_+_", 2, Lines);

    [Fact]
    public static void Add_OfEveryPairOfEdges_IsTheExactSumOrOverflow()
    {
        foreach (long left in Edges)
        {
            foreach (long right in Edges)
            {
                AssertExactOrOverflow((BigInteger)left + right, IntArithmetic.Add([Int(left), Int(right)], Site));
            }
        }
    }

    [Fact]
    public static void Divide_OfEveryPairOfEdgesWithANonZeroDivisor_IsTheQuotientTruncatedTowardZeroOrOverflow()
    {
        foreach (long dividend in Edges)
        {
            foreach (long divisor in Edges)
            {
                if (divisor != 0)
                {
                    // BigInteger.Divide truncates toward zero, as CEL does.
                    AssertExactOrOverflow(BigInteger.Divide(dividend, divisor), IntArithmetic.Divide([Int(dividend), Int(divisor)], Site));
                }
            }
        }
    }

    [Fact]
    public static void Negate_OfEveryEdge_IsTheExactNegationOrOverflow()
    {
        foreach (long operand in Edges)
        {
            AssertExactOrOverflow(-(BigInteger)operand, IntArithmetic.Negate([Int(operand)], Site));
        }
    }

    [Theory]
    [InlineData(long.MaxValue, 1L)]
    [InlineData(long.MinValue, -1L)]
    [InlineData(long.MinValue, long.MinValue)]
    public static void Add_BeyondTheRange_IsOverflow(long left, long right)
    {
        AssertError(CelErrorCode.Overflow, "integer overflow", IntArithmetic.Add([Int(left), Int(right)], Site));
    }

    [Theory]
    [InlineData(7L, 2L, 3L)]
    [InlineData(-7L, 2L, -3L)]
    [InlineData(7L, -2L, -3L)]
    [InlineData(-7L, -2L, 3L)]
    [InlineData(long.MinValue, 1L, long.MinValue)]
    [InlineData(long.MaxValue, -1L, -long.MaxValue)]
    public static void Divide_TruncatesTowardZero(long dividend, long divisor, long quotient)
    {
        Assert.True(IntArithmetic.Divide([Int(dividend), Int(divisor)], Site).TryGetInt64(out long result));
        Assert.Equal(quotient, result);
    }

    [Fact]
    public static void Divide_MinimumByMinusOne_IsOverflow()
    {
        AssertError(CelErrorCode.Overflow, "integer overflow", IntArithmetic.Divide([Int(long.MinValue), Int(-1)], Site));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(long.MinValue)]
    public static void Divide_ByZero_IsDivisionByZero(long dividend)
    {
        AssertError(CelErrorCode.DivisionByZero, "division by zero", IntArithmetic.Divide([Int(dividend), Int(0)], Site));
    }

    [Fact]
    public static void Negate_OfTheMinimum_IsOverflow()
    {
        AssertError(CelErrorCode.Overflow, "integer overflow", IntArithmetic.Negate([Int(long.MinValue)], Site));
    }

    // The call site gives the error the stage Evaluate and the position of the call: offset 2 of "1\n+ 2"
    // is the start of line 2; offsets 2 and 7 of "7 /\n 0 / 1" are its two divisions, on lines 1 and 2.
    [Theory]
    [InlineData("add", 2, 2, 1)]
    [InlineData("negate", 2, 2, 1)]
    [InlineData("divide", 2, 1, 3)]
    [InlineData("divide", 7, 2, 4)]
    public static void Error_HasThePositionOfTheCall(string implementation, int offset, int line, int column)
    {
        CelValue result = implementation switch
        {
            "add" => IntArithmetic.Add([Int(long.MaxValue), Int(1)], Site),
            "negate" => IntArithmetic.Negate([Int(long.MinValue)], new CallSite("-_", offset, Lines)),
            _ => IntArithmetic.Divide([Int(7), Int(0)], new CallSite("_/_", offset, new LineTable("7 /\n 0 / 1"))),
        };

        CelError error = Error(result);
        Assert.Equal(CelErrorStage.Evaluate, error.Stage);
        Assert.Equal((offset, line, column), (error.Location.Offset, error.Location.Line, error.Location.Column));
        Assert.Null(error.SourceLocation);
        Assert.Null(error.Exception);
    }

    [Fact]
    public static void ArgumentOfAnotherKind_IsEvalNoMatchingOverloadWithTheKindsOfTheValues()
    {
        CallSite divide = new("_/_", 0, Lines);
        CallSite negate = new("-_", 0, Lines);

        AssertError(CelErrorCode.EvalNoMatchingOverload, "no such overload: _+_(int, string)", IntArithmetic.Add([Int(1), CelValue.FromString("a")], Site));
        AssertError(CelErrorCode.EvalNoMatchingOverload, "no such overload: _+_(uint, int)", IntArithmetic.Add([CelValue.FromUInt64(1), Int(1)], Site));
        AssertError(CelErrorCode.EvalNoMatchingOverload, "no such overload: _/_(int, double)", IntArithmetic.Divide([Int(1), CelValue.FromDouble(0)], divide));
        AssertError(CelErrorCode.EvalNoMatchingOverload, "no such overload: _/_(null_type, bool)", IntArithmetic.Divide([CelValue.Null, CelValue.FromBoolean(true)], divide));
        AssertError(CelErrorCode.EvalNoMatchingOverload, "no such overload: -_(bytes)", IntArithmetic.Negate([CelValue.FromBytes(new byte[] { 1 })], negate));
    }

    private static CelValue Int(long value) => CelValue.FromInt64(value);

    private static void AssertExactOrOverflow(BigInteger exact, CelValue result)
    {
        if (exact >= long.MinValue && exact <= long.MaxValue)
        {
            Assert.True(result.TryGetInt64(out long value), exact.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal((long)exact, value);
        }
        else
        {
            Assert.Equal(CelErrorCode.Overflow, Error(result).Code);
        }
    }

    private static void AssertError(CelErrorCode code, string message, CelValue result)
    {
        CelError error = Error(result);
        Assert.Equal((code, message), (error.Code, error.Message));
    }

    private static CelError Error(CelValue result)
    {
        Assert.True(result.TryGetError(out CelError? error));
        return error;
    }
}
