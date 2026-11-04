using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// The result rows of the type mapping table in this part: the value of the root becomes the requested
// .NET type T after the evaluation, and a value that the row of its type does not convert is
// ResultConversion at the root. There is no conversion between numeric kinds.
[Trait("Requirement", "+R-10")]
public sealed class ResultConversionTests
{
    private static readonly long[] OneTwoAsLong = [1, 2];

    private static readonly int[] OneTwoAsInt = [1, 2];

    private static readonly object?[] OneStringNull = [1L, "a", null];

    private static readonly object[] OneString = [1L, "a"];

    private static readonly object?[] OneTwoAsObjects = [1L, 2L];

    private static readonly string?[] AStringAndNull = ["a", null];

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("d", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("d", 5UL);

    [Fact]
    public void Collections_BecomeReadOnlyCollections()
    {
        Assert.Equal(OneTwoAsLong, Evaluate<IReadOnlyList<long>>("[1, 2]").GetValueOrThrow());
        Assert.Equal(OneTwoAsInt, Evaluate<IReadOnlyList<int>>("[1, 2]").GetValueOrThrow());

        IReadOnlyDictionary<string, long> map = Evaluate<IReadOnlyDictionary<string, long>>("{'a': 1}").GetValueOrThrow();
        Assert.Equal(1L, Assert.Single(map).Value);
        Assert.Equal(1L, map["a"]);

        IReadOnlyList<object?> list = Assert.IsAssignableFrom<IReadOnlyList<object?>>(Evaluate<object>("[1, 'a', null]").GetValueOrThrow());
        Assert.Equal(OneStringNull, list);
        Assert.Equal(2, Evaluate<CelList>("[1, 'a']").GetValueOrThrow().Count);
    }

    // Typed nesting is one step: the element, key and value types of IReadOnlyList<X> and
    // IReadOnlyDictionary<XK, XV> are not such collections themselves. Native AOT cannot build a closed
    // generic type known only at run time, so the rows are a closed list. Deeper values come through
    // CelList, CelMap, CelValue and object, at any depth.
    [Fact]
    public void TypedNesting_IsOneStep()
    {
        Assert.Throws<ArgumentException>(() => environment.Compile<IReadOnlyList<IReadOnlyList<long>>>("[[1]]"));
        Assert.Throws<ArgumentException>(() => environment.Compile<IReadOnlyDictionary<string, IReadOnlyList<long>>>("{'a': [1]}"));

        Assert.Equal(2, Assert.Single(Evaluate<IReadOnlyList<CelList>>("[[1, 2]]").GetValueOrThrow()).Count);
        IReadOnlyList<object?> outer = Assert.IsAssignableFrom<IReadOnlyList<object?>>(Evaluate<object>("[[1, 2]]").GetValueOrThrow());
        Assert.Equal(OneTwoAsObjects, Assert.IsAssignableFrom<IReadOnlyList<object?>>(Assert.Single(outer)));
    }

    // [1] + dyn(['a']) has the type list(int) and the value [1, 'a']. The check before a lazy view refuses
    // only what the view reads by a static type — the same values that a copy would refuse: it is made
    // against the expected type that T derives, not against the static type of the root. An element read
    // as object or as CelValue is checked as dyn, so these views give the value, and only the view of
    // long is ResultConversion, at the root: the + at offset 4.
    // A position that the view gives as string, byte[], CelList, CelMap or CelType also takes null, which
    // the rows read there as a null reference, as a copy does: ['a'] + dyn([null]) is a view and
    // dyn(['a', null]) a copy, and both give ['a', null]; at the root, T = string gives the null of the
    // static type string. A position of a value type does not take null: ReadOnlyMemory<byte> and long
    // are ResultConversion for a view and for a copy alike.
    [Fact]
    [Trait("Requirement", "F-4")]
    public void CheckBeforeALazyView_RefusesOnlyWhatTheViewReadsByAType()
    {
        Assert.Equal(OneString, Evaluate<IReadOnlyList<object>>("[1] + dyn(['a'])").GetValueOrThrow());
        Assert.Equal(2, Evaluate<IReadOnlyList<CelValue>>("[1] + dyn(['a'])").GetValueOrThrow().Count);
        Assert.Equal(OneString, Assert.IsAssignableFrom<IReadOnlyList<object?>>(Evaluate<object>("[1] + dyn(['a'])").GetValueOrThrow()));

        CelError error = Assert.Single(Evaluate<IReadOnlyList<long>>("[1] + dyn(['a'])").Errors);
        Assert.Equal((CelErrorCode.ResultConversion, CelErrorStage.Evaluate, 4), (error.Code, error.Stage, error.Location.Offset));

        Assert.Equal(AStringAndNull, Evaluate<IReadOnlyList<string?>>("['a'] + dyn([null])").GetValueOrThrow());
        Assert.Equal(AStringAndNull, Evaluate<IReadOnlyList<string?>>("dyn(['a', null])").GetValueOrThrow());
        Assert.Null(Evaluate<string>("(['a'] + dyn([null]))[1]").GetValueOrThrow());

        IReadOnlyList<CelList?> lists = Evaluate<IReadOnlyList<CelList?>>("[[1]] + dyn([null])").GetValueOrThrow();
        Assert.Equal((2, 1, true), (lists.Count, lists[0]!.Count, lists[1] is null));
        Assert.Null(Evaluate<IReadOnlyList<CelList?>>("dyn([[1], null])").GetValueOrThrow()[1]);

        Assert.Equal(CelErrorCode.ResultConversion, Assert.Single(Evaluate<IReadOnlyList<ReadOnlyMemory<byte>>>("[b'a'] + dyn([null])").Errors).Code);
        Assert.Equal(CelErrorCode.ResultConversion, Assert.Single(Evaluate<IReadOnlyList<ReadOnlyMemory<byte>>>("dyn([b'a', null])").Errors).Code);
        Assert.Equal(CelErrorCode.ResultConversion, Assert.Single(Evaluate<IReadOnlyList<long>>("[1] + dyn([null])").Errors).Code);
        Assert.Equal(CelErrorCode.ResultConversion, Assert.Single(Evaluate<IReadOnlyList<long>>("dyn([1, null])").Errors).Code);
    }

    // Replaces the case null as string of the walking skeleton: null is not assignable to the expected
    // type string, so that text no longer compiles for T = string.
    [Fact]
    public void TypeValueAndNull_BecomeCelTypeAndNull()
    {
        Assert.Equal(CelType.Int, Evaluate<CelType>("type(1)").GetValueOrThrow());
        Assert.Equal(CelType.ListOf(CelType.Dyn), Evaluate<CelType>("list").GetValueOrThrow());
        Assert.Null(Evaluate<long?>("null").GetValueOrThrow());
        Assert.Equal(7L, Evaluate<long?>("7").GetValueOrThrow());
        Assert.Null(Evaluate<object>("null").GetValueOrThrow());
    }

    // 2^32 does not fit System.Int32: the list is copied at the end of the evaluation, and its element fails.
    [Fact]
    [Trait("Requirement", "F-4")]
    public void ElementThatTheRowDoesNotConvert_IsResultConversionAtTheRoot()
    {
        AssertResultConversion(Evaluate<IReadOnlyList<int>>("  [1, 4294967296]").Errors);
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void UIntForLong_IsResultConversion()
    {
        CelError error = AssertResultConversion(Evaluate<long>("  d").Errors);

        Assert.Equal("result of type 'uint' cannot be converted to .NET type 'System.Int64'", error.Message);
    }

    private static CelError AssertResultConversion(IReadOnlyList<CelError> errors)
    {
        CelError error = Assert.Single(errors);
        Assert.Equal((CelErrorCode.ResultConversion, CelErrorStage.Evaluate), (error.Code, error.Stage));
        Assert.Equal((2, 1, 3), (error.Location.Offset, error.Location.Line, error.Location.Column));
        return error;
    }

    private CelEvalResult<T> Evaluate<T>(string expression) =>
        environment.Compile<T>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);
}
