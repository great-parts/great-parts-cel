using System.Collections.Generic;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// An error value that an element, a key or a value of a collection of the application is: the check against a declared type
// and the conversion of a result return it as the outcome, the first in the order of the walk, and never as a mismatch of
// type; a mismatch is named only when there is one.
public sealed class ErrorElementRulesTests
{
    private static readonly CelError First = Failure("first");

    private static readonly CelError Second = Failure("second");

    [Fact]
    public void ErrorElementOfAList_IsTheOutcomeAndNotAMismatch()
    {
        CelValue list = ListOf(1L, First, 3L);

        TypeFit fit = ValueTypeRules.Fits(list, CelType.ListOf(CelType.Int), null);

        Assert.False(fit.IsFit);
        Assert.Same(First, fit.ElementError);
    }

    [Fact]
    public void ListWithoutErrorsAndMismatch_FitsAndHasNoElementError()
    {
        TypeFit fit = ValueTypeRules.Fits(ListOf(1L, 2L), CelType.ListOf(CelType.Int), null);

        Assert.True(fit.IsFit);
        Assert.Null(fit.ElementError);
    }

    [Fact]
    public void MismatchOfAnElement_HasNoElementError()
    {
        TypeFit fit = ValueTypeRules.Fits(ListOf(1L, "x"), CelType.ListOf(CelType.Int), null);

        Assert.False(fit.IsFit);
        Assert.Null(fit.ElementError);
    }

    // The first element in the order of the walk decides: an error before a mismatch is the outcome, a mismatch before an
    // error is a mismatch, and of two errors the first.
    [Fact]
    public void FirstProblemInTheOrderOfTheWalk_Decides()
    {
        CelType ints = CelType.ListOf(CelType.Int);

        Assert.Same(First, ValueTypeRules.Fits(ListOf(First, "x"), ints, null).ElementError);
        Assert.Null(ValueTypeRules.Fits(ListOf("x", First), ints, null).ElementError);
        Assert.False(ValueTypeRules.Fits(ListOf("x", First), ints, null).IsFit);
        Assert.Same(First, ValueTypeRules.Fits(ListOf(1L, First, Second), ints, null).ElementError);
    }

    [Fact]
    public void ErrorElementInANestedList_IsTheOutcome()
    {
        CelValue nested = CelValue.FromList(CelList.Create(CelValue.FromList(CelList.Create(CelValue.FromInt64(1))), ListOf(2L, Second)));

        TypeFit fit = ValueTypeRules.Fits(nested, CelType.ListOf(CelType.ListOf(CelType.Int)), null);

        Assert.Same(Second, fit.ElementError);
    }

    [Fact]
    public void ErrorValueOfAMap_IsTheOutcomeInTheOrderOfIteration()
    {
        CelType type = CelType.MapOf(CelType.String, CelType.Int);

        Assert.Same(First, ValueTypeRules.Fits(MapOf(("a", 1L), ("b", First), ("c", Second)), type, null).ElementError);
        Assert.Same(Second, ValueTypeRules.Fits(MapOf(("a", Second), ("b", First)), type, null).ElementError);
        Assert.Null(ValueTypeRules.Fits(MapOf(("a", "x"), ("b", First)), type, null).ElementError);
        Assert.False(ValueTypeRules.Fits(MapOf(("a", "x"), ("b", First)), type, null).IsFit);
    }

    [Fact]
    public void ErrorElementUnderDyn_IsNotReachedBecauseTheCheckDoesNotDescend()
    {
        var meter = new RecordingMeter();

        Assert.True(ValueTypeRules.Fits(ListOf(First), CelType.ListOf(CelType.Dyn), meter).IsFit);
        Assert.Empty(meter.Charges);

        // The keys of a map are checked against `string`, so the check descends into the entries and pays for them; the value
        // under `dyn` is still not read, and the error element in it is not reached.
        Assert.True(ValueTypeRules.Fits(MapOf(("a", First)), CelType.MapOf(CelType.String, CelType.Dyn), meter).IsFit);
        Assert.Equal([1UL], meter.Charges);
    }

    [Fact]
    public void ErrorValueAtTheTop_IsAMismatchBecauseNoElementIsRead()
    {
        TypeFit fit = ValueTypeRules.Fits(CelValue.FromError(First), CelType.Int, null);

        Assert.False(fit.IsFit);
        Assert.Null(fit.ElementError);
    }

    [Fact]
    public void ErrorElement_DoesNotChangeTheCostOfTheCheck()
    {
        var meter = new RecordingMeter();

        _ = ValueTypeRules.Fits(ListOf(1L, First, 3L), CelType.ListOf(CelType.Int), meter);

        Assert.Equal([3UL], meter.Charges);
    }

    [Fact]
    public void ResultOfACheckedView_ReturnsTheErrorElementAsTheOutcome()
    {
        CelValue list = ListOf(1L, First);

        bool converted = ConversionRules.TryToResult(
            list,
            CelType.ListOf(CelType.Int),
            out IReadOnlyList<long>? result,
            out string? actualType,
            out CelError? elementError);

        Assert.False(converted);
        Assert.Null(result);
        Assert.Same(First, elementError);
        Assert.NotNull(actualType);
    }

    [Fact]
    public void ResultOfACopy_ReturnsTheErrorElementWhereTheConversionFails()
    {
        CelValue list = ListOf(1L, First);
        CelValue map = MapOf(("a", 1L), ("b", Second));

        Assert.False(ConversionRules.TryToResult(list, CelType.Dyn, out IReadOnlyList<long>? _, out _, out CelError? inList));
        Assert.False(ConversionRules.TryToResult(map, CelType.Dyn, out IReadOnlyDictionary<string, long>? _, out _, out CelError? inMap));

        Assert.Same(First, inList);
        Assert.Same(Second, inMap);
    }

    [Fact]
    public void ResultThatDoesNotConvertWithoutAnErrorElement_IsAConversionFailureOnly()
    {
        CelValue mixed = ListOf(1L, "x");

        Assert.False(ConversionRules.TryToResult(mixed, CelType.Dyn, out IReadOnlyList<long>? _, out string? actualType, out CelError? elementError));
        Assert.False(ConversionRules.TryToResult(mixed, CelType.ListOf(CelType.Int), out IReadOnlyList<long>? _, out _, out CelError? inView));

        Assert.Null(elementError);
        Assert.Null(inView);
        Assert.Equal("list", actualType);
    }

    [Fact]
    public void ResultThatConverts_HasNoElementError()
    {
        Assert.True(ConversionRules.TryToResult(ListOf(1L, 2L), CelType.Dyn, out IReadOnlyList<long>? result, out _, out CelError? elementError));

        Assert.Equal<long>([1L, 2L], result);
        Assert.Null(elementError);
    }

    // Arguments of an overload: an error element of a marked argument is the outcome of the match, an error before a
    // mismatch of a later argument too, and it is no mismatch.
    [Fact]
    public void MatchOfAMarkedArgument_ReturnsTheErrorElement()
    {
        OverloadBinding overload = new(
            "total",
            CelOverloadDecl.Global("total_list", [CelType.ListOf(CelType.Int), CelType.String], CelType.Int),
            null,
            1,
            null,
            "application",
            isApplication: true);
        CelValue withError = ListOf(1L, First);
        CelValue text = CelValue.FromString("a");

        Assert.Same(First, overload.Matches([withError, text], [true, true], null).ElementError);
        Assert.Same(First, overload.Matches([withError, CelValue.FromInt64(1)], [true, true], null).ElementError);
        Assert.True(overload.Matches([withError, text], [false, false], null).IsFit);
        Assert.Null(overload.Matches([CelValue.FromInt64(1), text], [true, true], null).ElementError);
    }

    private static CelError Failure(string text) => new(CelErrorCode.InvalidArgument, CelErrorStage.Evaluate, text, default, null, null);

    // A list of the application whose elements are longs, strings and error values.
    private static CelValue ListOf(params object[] items) => CelValue.FromList(CelList.CreateView<object>(items, Convert));

    // A map of the application, in the order of the entries.
    private static CelValue MapOf(params (string Key, object Value)[] entries)
    {
        var source = new Dictionary<string, object>();
        foreach ((string key, object value) in entries)
        {
            source.Add(key, value);
        }

        return CelValue.FromMap(CelMap.CreateView(source, Convert, keepSourceOrder: true));
    }

    private static CelValue Convert(object item) => item switch
    {
        CelError error => CelValue.FromError(error),
        string text => CelValue.FromString(text),
        _ => CelValue.FromInt64((long)item),
    };
}
