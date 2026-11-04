using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Checking;

// The expected type of Compile<T>: from T, or from CelCompileOptions.ExpectedType when T can hold it, for the
// result types implemented so far.
public sealed class CompileExpectedTypeTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("n", CelType.Int)
        .AddVariable("s", CelType.String)
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public static void TypeNotAssignableToTheTypeOfT_IsExpectedTypeMismatch()
    {
        AssertMismatch(Environment.Compile<long>("s").Errors, 0, "string", "int");
        AssertMismatch(Environment.Compile<uint>("n").Errors, 0, "int", "uint");
        AssertMismatch(Environment.Compile<bool>("n + 2").Errors, 2, "int", "bool");
        AssertMismatch(Environment.Compile<byte[]>("'x'").Errors, 0, "string", "bytes");
        AssertMismatch(Environment.Compile<double>("null").Errors, 0, "null_type", "double");
    }

    [Fact]
    public static void TypeNotAssignableToTheExplicitExpectedType_IsExpectedTypeMismatch()
    {
        AssertMismatch(Environment.Compile<CelValue>("[n]", Expecting(CelType.ListOf(CelType.String))).Errors, 0, "list(int)", "list(string)");
        AssertMismatch(Environment.Compile<string>("n", Expecting(CelType.WrapperOf(CelType.String))).Errors, 0, "int", "wrapper(string)");
    }

    [Fact]
    public static void AssignableType_Compiles()
    {
        Assert.True(Environment.Compile<long>("n").IsSuccess);
        Assert.True(Environment.Compile<CelValue>("s").IsSuccess);
        Assert.True(Environment.Compile<long>("s", Expecting(CelType.Dyn)).IsSuccess);
        Assert.True(Environment.Compile<int>("n", Expecting(CelType.TypeParameter("T"))).IsSuccess);
        Assert.True(Environment.Compile<string>("null", Expecting(CelType.Null)).IsSuccess);
        Assert.True(Environment.Compile<string>("s", Expecting(CelType.WrapperOf(CelType.String))).IsSuccess);
        Assert.True(Environment.Compile<byte[]>("null", Expecting(CelType.WrapperOf(CelType.Bytes))).IsSuccess);
        Assert.True(Environment.Compile<CelValue>("[]", Expecting(CelType.ListOf(CelType.Int))).IsSuccess);
    }

    // The pair is checked before the text is read, so a text that does not parse throws the same.
    [Theory]
    [InlineData("n")]
    [InlineData("(")]
    public static void ExplicitTypeThatTCannotHold_IsArgumentException(string expression)
    {
        Assert.Equal("options", Assert.Throws<ArgumentException>(() => Environment.Compile<long>(expression, Expecting(CelType.String))).ParamName);
        Assert.Throws<ArgumentException>(() => Environment.Compile<string>(expression, Expecting(CelType.WrapperOf(CelType.Int))));
        Assert.Throws<ArgumentException>(() => Environment.Compile<ReadOnlyMemory<byte>>(expression, Expecting(CelType.Null)));
        Assert.Throws<ArgumentException>(() => Environment.Compile<bool>(expression, Expecting(CelType.ListOf(CelType.Bool))));
        Assert.Throws<ArgumentException>(() => Environment.Compile<ulong>(expression, Expecting(CelType.WrapperOf(CelType.UInt))));
    }

    [Fact]
    public static void ExpectedType_IsTheValueSet()
    {
        CelType type = CelType.ListOf(CelType.Int);

        Assert.Same(type, new CelCompileOptions { ExpectedType = type }.ExpectedType);
        Assert.Null(new CelCompileOptions().ExpectedType);
    }

    private static CelCompileOptions Expecting(CelType type) => new() { ExpectedType = type };

    private static void AssertMismatch(System.Collections.Immutable.ImmutableArray<CelError> errors, int offset, string actual, string expected)
    {
        CelError error = Assert.Single(errors);
        Assert.Equal((CelErrorCode.ExpectedTypeMismatch, CelErrorStage.Check, offset), (error.Code, error.Stage, error.Location.Offset));
        Assert.Equal("expression of type '" + actual + "' is not assignable to the expected type '" + expected + "'", error.Message);
    }
}
