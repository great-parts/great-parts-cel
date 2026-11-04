using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Checking;

// The expected type of a compilation: from T of Compile<T> by the expected-type table, or from
// CelCompileOptions.ExpectedType. The type of the expression must be assignable to it; otherwise the
// compilation fails with ExpectedTypeMismatch at the root. dyn on either side passes.
public sealed class ExpectedTypeTests
{
    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("x")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    [Trait("Requirement", "+R-10")]
    [Trait("Requirement", "F-4")]
    public void TypeNotAssignableToTheTypeOfT_IsExpectedTypeMismatchAtTheRoot()
    {
        AssertMismatch(environment.Compile<long>("'a' + 'b'").Errors, 4, "string", "int");
        AssertMismatch(environment.Compile<long>("null").Errors, 0, "null_type", "int");
        AssertMismatch(environment.Compile<ulong>("x").Errors, 0, "int", "uint");
        AssertMismatch(environment.Compile<long?>("'a'").Errors, 0, "string", "wrapper(int)");
        AssertMismatch(environment.Compile<IReadOnlyList<long>>("['a']").Errors, 0, "list(string)", "list(int)");
        AssertMismatch(environment.Compile<IReadOnlyDictionary<string, long>>("{'a': 'b'}").Errors, 0, "map(string, string)", "map(string, int)");
        AssertMismatch(environment.Compile<bool>("[1, 2][x]").Errors, 6, "int", "bool");
    }

    [Fact]
    [Trait("Requirement", "+R-10")]
    [Trait("Requirement", "F-4")]
    public void TypeNotAssignableToTheExpectedTypeOption_IsExpectedTypeMismatchAtTheRoot()
    {
        AssertMismatch(environment.Compile<object>("x", Expecting(CelType.String)).Errors, 0, "int", "string");
        AssertMismatch(environment.Compile<CelValue>("[1u]", Expecting(CelType.ListOf(CelType.Int))).Errors, 0, "list(uint)", "list(int)");
        AssertMismatch(environment.Compile<CelList>("{'a': x}", Expecting(CelType.ListOf(CelType.Dyn))).Errors, 0, "map(string, int)", "list(dyn)");
    }

    // An expression whose type is assignable passes: a wrapper takes its primitive and null, a free type
    // parameter of an empty literal is bound by the expected type, dyn passes any expected type and is
    // converted at evaluation, and dyn as the expected type takes anything.
    [Fact]
    [Trait("Requirement", "+R-10")]
    public void AssignableType_Compiles()
    {
        Assert.True(environment.Compile<long?>("null").IsSuccess);
        Assert.True(environment.Compile<long?>("x").IsSuccess);
        Assert.True(environment.Compile<IReadOnlyList<long>>("[]").IsSuccess);
        Assert.True(environment.Compile<IReadOnlyDictionary<string, long>>("{}").IsSuccess);
        Assert.True(environment.Compile<string>("dyn(x)").IsSuccess);
        Assert.True(environment.Compile<CelType>("int").IsSuccess);
        Assert.True(environment.Compile<CelValue>("'a'").IsSuccess);
        Assert.True(environment.Compile<object>("[x]").IsSuccess);
        Assert.True(environment.Compile<long>("'a'", Expecting(CelType.Dyn)).IsSuccess);
        Assert.True(environment.Compile<CelValue>("[x]", Expecting(CelType.ListOf(CelType.Int))).IsSuccess);
    }

    // The expected type is checked only when the expression has no other error.
    [Fact]
    [Trait("Requirement", "F-4")]
    public void ExpressionWithAnotherError_IsNotCheckedAgainstTheExpectedType()
    {
        CelError error = Assert.Single(environment.Compile<long>("y").Errors);

        Assert.Equal(CelErrorCode.UndeclaredReference, error.Code);
    }

    // T outside the tables, or an expected type that T cannot hold, is a violated call condition: the
    // exception comes before the text is read, so a text that does not parse throws the same.
    [Fact]
    [Trait("Requirement", "+R-10")]
    public void TOutsideTheTablesOrIncompatibleWithTheOption_IsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => environment.Compile<short>("x"));
        Assert.Throws<ArgumentException>(() => environment.Compile<Guid>("'a'"));
        Assert.Throws<ArgumentException>(() => environment.Compile<short>("("));
        Assert.Throws<ArgumentException>(() => environment.Compile<long>("x", Expecting(CelType.String)));
        Assert.Throws<ArgumentException>(() => environment.Compile<long>("(", Expecting(CelType.String)));
        Assert.Throws<ArgumentException>(() => environment.Compile<IReadOnlyList<long>>("[]", Expecting(CelType.ListOf(CelType.String))));
        Assert.Throws<ArgumentException>(() => environment.Compile<string>("null", Expecting(CelType.WrapperOf(CelType.Int))));
    }

    private static CelCompileOptions Expecting(CelType type) => new() { ExpectedType = type };

    private static void AssertMismatch(ImmutableArray<CelError> errors, int offset, string actual, string expected)
    {
        CelError error = Assert.Single(errors);
        Assert.Equal(CelErrorCode.ExpectedTypeMismatch, error.Code);
        Assert.Equal(CelErrorStage.Check, error.Stage);
        Assert.Equal((offset, 1, offset + 1), (error.Location.Offset, error.Location.Line, error.Location.Column));
        Assert.Equal("expression of type '" + actual + "' is not assignable to the expected type '" + expected + "'", error.Message);
    }
}
