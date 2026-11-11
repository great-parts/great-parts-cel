using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Library.Extensions;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.Extensions.Lists.ListsEnvironment;

namespace Great.Parts.Cel.Tests.Library.Extensions.Lists;

// The implementations of the set lists called on values, for the paths that the checks of types close to an expression: the keys of
// another size or of a kind that cannot be ordered, which a call on the values of an expression never reaches, and the units that
// flatten charges one by one, read from a meter that records each charge.
public sealed class ListsCallTests
{
    private static readonly LineTable Lines = new("0123456789");

    private static CallSite Site(string function, IEvaluationMeter? meter = null) => new(function, 4, Lines, meter);

    private static CelValue Ints(params long[] numbers) => ListOf(Array.ConvertAll(numbers, CelValue.FromInt64));

    private static CelError ErrorOf(CelValue value)
    {
        Assert.True(value.TryGetError(out CelError? error), value.Kind.ToString());
        return error;
    }

    [Fact]
    public static void SortByKeys_OfAnotherSize_NamesTheTwoSizes()
    {
        CelError error = ErrorOf(ListSort.SortByKeys([Ints(1, 2), Ints(1, 2, 3)], Site("@sortByAssociatedKeys")));

        Assert.Equal(CelErrorCode.InvalidArgument, error.Code);
        Assert.Equal(4, error.Location.Offset);
        Assert.Equal(
            "invalid argument to '@sortByAssociatedKeys': @sortByAssociatedKeys() expected a list of the same size as the associated keys list, but got 2 and 3 elements respectively",
            error.Message);
    }

    // The sizes come first, before the empty list that is returned as it is.
    [Fact]
    public static void SortByKeys_OfAnotherSizeThanAnEmptyList_IsAnError()
    {
        CelError error = ErrorOf(ListSort.SortByKeys([Ints(1), Ints()], Site("@sortByAssociatedKeys")));

        Assert.EndsWith("but got 1 and 0 elements respectively", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public static void SortOfAnEmptyList_ReturnsTheListItself()
    {
        CelValue empty = Ints();

        CelValue sorted = ListSort.Sort([empty], Site("sort"));

        Assert.True(sorted.TryGetList(out CelList? list));
        Assert.True(empty.TryGetList(out CelList? original));
        Assert.Same(original, list);
    }

    [Fact]
    public static void SortByKeys_OfKeysThatAreNoneOfTheEightKinds_IsAnError()
    {
        CelError lists = ErrorOf(ListSort.SortByKeys([Ints(1), ListOf(Ints(1))], Site("@sortByAssociatedKeys")));
        CelError nulls = ErrorOf(ListSort.SortByKeys([Ints(1), ListOf(CelValue.Null)], Site("@sortByAssociatedKeys")));

        Assert.Equal("invalid argument to '@sortByAssociatedKeys': list elements must be comparable", lists.Message);
        Assert.Equal(lists.Message, nulls.Message);
    }

    // The keys are of one kind: an int and a uint, an int and a double are never ordered together.
    [Fact]
    public static void SortByKeys_OfDifferentKinds_IsAnError()
    {
        CelValue intAndUInt = ListOf(CelValue.FromInt64(1), CelValue.FromUInt64(2));
        CelValue intAndDouble = ListOf(CelValue.FromInt64(1), CelValue.FromDouble(2.5));

        CelError first = ErrorOf(ListSort.SortByKeys([Ints(1, 2), intAndUInt], Site("@sortByAssociatedKeys")));
        CelError second = ErrorOf(ListSort.Sort([intAndDouble], Site("sort")));

        Assert.Equal("invalid argument to '@sortByAssociatedKeys': list elements must have the same type", first.Message);
        Assert.Equal(first.Message, second.Message);
    }

    // Each list that flatten descends into is charged by its size before its elements are viewed, in the order the walk meets them;
    // the receiver is charged with the call, before the function runs.
    [Fact]
    public static void Flatten_ChargesEachListItDescendsIntoBeforeViewingIt()
    {
        var meter = new RecordingMeter();
        CelValue oneLevel = ListOf(Ints(1, 2), Ints(3));
        CelValue twoLevels = ListOf(ListOf(CelValue.FromInt64(1), Ints(2, 3)), Ints(4));

        _ = ListFunctions.Flatten([oneLevel], Site("flatten", meter));

        Assert.Equal(new ulong[] { 2, 1 }, meter.Charges);

        var deeper = new RecordingMeter();
        _ = ListFunctions.Flatten([twoLevels, CelValue.FromInt64(2)], Site("flatten", deeper));

        Assert.Equal(new ulong[] { 2, 2, 1 }, deeper.Charges);
    }

    [Fact]
    public static void FlattenToTheDepthZero_ReturnsTheListWithoutACharge()
    {
        var meter = new RecordingMeter();
        CelValue list = ListOf(Ints(1, 2), Ints(3));

        CelValue flat = ListFunctions.Flatten([list, CelValue.FromInt64(0)], Site("flatten", meter));

        Assert.True(flat.TryGetList(out CelList? result));
        Assert.True(list.TryGetList(out CelList? original));
        Assert.Same(original, result);
        Assert.Empty(meter.Charges);
    }
}
