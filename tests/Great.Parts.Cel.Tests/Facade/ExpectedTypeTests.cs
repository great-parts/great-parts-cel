using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// The expected CEL type from T and from CelCompileOptions.ExpectedType (docs/design/facade.md, "Expected type").
public sealed class ExpectedTypeTests
{
    private static readonly string[] Rows =
    [
        "bool", "int", "int", "uint", "uint", "double", "string", "bytes", "bytes",
        "list(int)", "list(string)", "map(string, int)", "type(dyn)",
        "wrapper(int)", "wrapper(int)", "wrapper(uint)", "wrapper(uint)", "wrapper(double)", "wrapper(bool)", "wrapper(bytes)",
        "dyn", "dyn", "list(dyn)", "map(dyn, dyn)",
    ];

    private static readonly CelType ListOfInt = CelType.ListOf(CelType.Int);

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("d", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    // Each row of the table, by the derive step that Compile<T> runs.
    [Fact]
    public void EachRow_GivesItsType()
    {
        string[] derived =
        [
            Derived<bool>(), Derived<long>(), Derived<int>(), Derived<ulong>(), Derived<uint>(), Derived<double>(), Derived<string>(),
            Derived<ReadOnlyMemory<byte>>(), Derived<byte[]>(),
            Derived<IReadOnlyList<long>>(), Derived<IReadOnlyList<string>>(), Derived<IReadOnlyDictionary<string, long>>(),
            Derived<CelType>(),
            Derived<long?>(), Derived<int?>(), Derived<ulong?>(), Derived<uint?>(), Derived<double?>(), Derived<bool?>(), Derived<ReadOnlyMemory<byte>?>(),
            Derived<CelValue>(), Derived<object>(), Derived<CelList>(), Derived<CelMap>(),
        ];

        Assert.Equal(Rows, derived);
    }

    [Fact]
    public void TypeOutsideTheTables_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Environment.Compile<short>("1"));
        Assert.Throws<ArgumentException>(() => Environment.Compile<Guid>("1"));
        Assert.Throws<ArgumentException>(() => Environment.Compile<float?>("1"));
        Assert.Throws<ArgumentException>(() => Environment.Compile<IReadOnlyList<short>>("[1]"));
        Assert.Throws<ArgumentException>(() => Environment.Compile<long[]>("[1]"));
        Assert.Throws<ArgumentException>(() => Environment.Compile<System.IO.Stream>("1"));

        // Typed nesting is one step: a list of a typed list is outside the tables.
        Assert.Throws<ArgumentException>(() => Environment.Compile<IReadOnlyList<IReadOnlyList<long>>>("[[1]]"));
        Assert.Throws<ArgumentException>(() => Environment.Compile<IReadOnlyDictionary<string, IReadOnlyList<long>>>("{'a': [1]}"));
    }

    // The rows of time have their expected types; an int is not assignable to a timestamp or a duration.
    [Fact]
    public void TypeOfTime_ExpectsItsType_AndAnIntIsExpectedTypeMismatch()
    {
        Assert.Equal(["timestamp", "timestamp", "timestamp", "duration", "duration", "duration"], new[]
        {
            Derived<CelTimestamp>(), Derived<DateTimeOffset>(), Derived<DateTimeOffset?>(), Derived<CelDuration>(), Derived<TimeSpan>(), Derived<TimeSpan?>(),
        });
        Assert.Equal(CelErrorCode.ExpectedTypeMismatch, Assert.Single(Environment.Compile<TimeSpan>("1").Errors).Code);
        Assert.Equal(CelErrorCode.ExpectedTypeMismatch, Assert.Single(Environment.Compile<DateTimeOffset>("1").Errors).Code);
        Assert.Throws<ArgumentException>(() => Environment.Compile<DateTime>("1"));
        Assert.Throws<ArgumentException>(() => Environment.Compile<IReadOnlyList<DateTimeOffset>>("[1]"));
    }

    [Fact]
    public void TypeOfALaterPart_ThrowsNotImplementedException()
    {
        Assert.Throws<NotImplementedException>(() => Environment.Compile<IReadOnlyList<decimal>>("1"));
        Assert.Throws<NotImplementedException>(() => Environment.Compile<CelValue>("1", Expect(CelType.OptionalOf(CelType.Int))));
    }

    // The derived type is the expected type of checking: list(string) does not take a list of int.
    [Fact]
    public void DerivedType_IsCheckedAgainstTheExpression()
    {
        CelError error = Assert.Single(Environment.Compile<IReadOnlyList<string>>("[1]").Errors);

        Assert.Equal((CelErrorCode.ExpectedTypeMismatch, CelErrorStage.Check), (error.Code, error.Stage));
        Assert.True(Environment.Compile<IReadOnlyDictionary<string, long>>("{'a': 1}").IsSuccess);
        Assert.Equal(ListOfInt, Environment.Compile<IReadOnlyList<long>>("[1]").GetProgramOrThrow().ResultType);
    }

    // dyn and a type parameter for every T; null_type and wrappers when T takes null; lists and maps by their parameters;
    // CelValue and object hold every type.
    [Fact]
    public void ExplicitType_ThatTHolds_ReplacesTheDerivedOne()
    {
        Assert.Equal(CelType.Dyn, Explicit<long>(CelType.Dyn));
        Assert.Equal(CelType.TypeParameter("A"), Explicit<IReadOnlyList<long>>(CelType.TypeParameter("A")));
        Assert.Equal(CelType.Null, Explicit<string>(CelType.Null));
        Assert.Equal(CelType.WrapperOf(CelType.String), Explicit<string>(CelType.WrapperOf(CelType.String)));
        Assert.Equal(CelType.WrapperOf(CelType.Int), Explicit<long?>(CelType.WrapperOf(CelType.Int)));
        Assert.Equal(CelType.Int, Explicit<long?>(CelType.Int));
        Assert.Equal(CelType.ListOf(CelType.Dyn), Explicit<IReadOnlyList<long>>(CelType.ListOf(CelType.Dyn)));
        Assert.Equal(ListOfInt, Explicit<CelList>(ListOfInt));
        Assert.Equal(CelType.MapOf(CelType.String, ListOfInt), Explicit<CelValue>(CelType.MapOf(CelType.String, ListOfInt)));
        Assert.Equal(ListOfInt, Explicit<object>(ListOfInt));
        Assert.Equal(CelType.TypeOf(CelType.Int), Explicit<CelType>(CelType.TypeOf(CelType.Int)));
    }

    [Fact]
    public void ExplicitType_ThatTDoesNotHold_ThrowsArgumentExceptionBeforeTheText()
    {
        Assert.Equal("options", Assert.Throws<ArgumentException>(() => Environment.Compile<long>("1 +", Expect(CelType.UInt))).ParamName);
        Assert.Throws<ArgumentException>(() => Environment.Compile<long>("1", Expect(CelType.Null)));
        Assert.Throws<ArgumentException>(() => Environment.Compile<long>("1", Expect(CelType.WrapperOf(CelType.Int))));
        Assert.Throws<ArgumentException>(() => Environment.Compile<IReadOnlyList<string>>("[1]", Expect(ListOfInt)));
        Assert.Throws<ArgumentException>(() => Environment.Compile<IReadOnlyDictionary<string, long>>("{}", Expect(CelType.MapOf(CelType.Int, CelType.Int))));
        Assert.Throws<ArgumentException>(() => Environment.Compile<CelMap>("[]", Expect(ListOfInt)));
    }

    private static CelCompileOptions Expect(CelType type) => new() { ExpectedType = type };

    // The derived type in its text form.
    private static string Derived<T>() => ConversionRules.ExpectedType<T>(null, "options").ToString();

    private static CelType Explicit<T>(CelType type) => ConversionRules.ExpectedType<T>(type, "options");
}
