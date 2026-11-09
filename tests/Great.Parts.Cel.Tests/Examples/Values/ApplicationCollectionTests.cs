using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Values;

// The collections of the application (the type mapping page, rows of lists, sets, collections without an
// index and dictionaries): CelList.CreateView reads an element at each access and copies nothing;
// CreateCopy copies in the order of enumeration; CreateSorted sorts in the order of CEL; CelMap.CreateView
// converts a CEL key back to the key of the dictionary and makes one lookup. An exception of the source or
// of a converter in a view is an error element: the node that reads it fails with ApplicationException,
// and an operation that does not read it does not. lv, cv and mv are a list, a list and a map of dyn.
public sealed class ApplicationCollectionTests
{
    private const string CanonicalGuid = "0f8fad5b-d9cb-469f-a165-70867728950e";

    private static readonly string[] Unsorted = ["b", "\U0001F600", "a", ""];

    private static readonly string[] ByCodePoints = ["a", "b", "", "\U0001F600"];

    private static readonly long[] One = [1];

    private static readonly string[] SourceOrder = ["b", "a", "c"];

    private static readonly string[] Ascending = ["a", "b", "c"];

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<CelList>("lv")
        .AddVariable<CelList>("cv")
        .AddVariable<CelMap>("mv")
        .Build()
        .GetEnvironmentOrThrow();

    // The view has the count of its source, converts an element at each read and reflects the source.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "P-2")]
    public void ListView_ConvertsAnElementAtEachRead()
    {
        var source = new List<long> { 1, 2, 3 };
        int conversions = 0;

        CelList view = CelList.CreateView(source, value =>
        {
            conversions++;
            return CelValue.FromInt64(value);
        });

        Assert.Equal(0, conversions);
        Assert.Equal(3, view.Count);
        Assert.Equal(CelValue.FromInt64(2), view[1]);
        Assert.Equal(1, conversions);
        source[1] = 20;
        Assert.Equal(CelValue.FromInt64(20), view[1]);
    }

    // An exception of the converter is the element: the index that reads it fails, size does not read it,
    // and equality and in fail when they reach it. The original exception is in CelError.Exception.
    [Theory]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "+R-9")]
    [InlineData("lv[1]", 2)]
    [InlineData("lv == [1, 2, 3]", 3)]
    [InlineData("2 in lv", 2)]
    public void ExceptionOfAViewElement_IsApplicationExceptionWhereTheElementIsRead(string expression, int offset)
    {
        CelBindings bindings = new CelBindings().Add("lv", ThrowingAt(1));

        CelError error = SingleError(expression, bindings);

        Assert.Equal(CelErrorCode.ApplicationException, error.Code);
        Assert.Equal("application code threw System.InvalidOperationException in a list of the application", error.Message);
        Assert.Equal((offset, 1, offset + 1), (error.Location.Offset, error.Location.Line, error.Location.Column));
        Assert.IsType<InvalidOperationException>(error.Exception);
        Assert.True(Evaluate<bool>("lv[0] == 1 && size(lv) == 3 && 1 in lv", bindings));
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "+R-9")]
    public void ExceptionOfAViewElement_IsAnErrorElementOfTheList()
    {
        Assert.True(ThrowingAt(1)[1].TryGetError(out CelError? error));
        Assert.Equal(CelErrorCode.ApplicationException, error.Code);
        Assert.IsType<InvalidOperationException>(error.Exception);
    }

    // CreateCopy converts now, in the order of enumeration of the source; a later change of the source is
    // not seen, and an exception of the converter is kept as an element.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void Copy_TakesTheOrderOfEnumerationAndKeepsErrorElements()
    {
        var source = new Queue<long>([3, 1, 2]);

        CelList copy = CelList.CreateCopy(source, CelValue.FromInt64);
        source.Enqueue(4);
        CelList withError = CelList.CreateCopy(new Queue<long>([1, 2]), value => value == 2 ? throw new InvalidOperationException() : CelValue.FromInt64(value));

        Assert.Equal(new[] { CelValue.FromInt64(3), CelValue.FromInt64(1), CelValue.FromInt64(2) }, copy);
        Assert.Equal(2, withError.Count);
        Assert.True(withError[1].TryGetError(out CelError? error));
        Assert.Equal(CelErrorCode.ApplicationException, error.Code);
        CelError read = SingleError("cv[1]", new CelBindings().Add("cv", withError));
        Assert.Equal((CelErrorCode.ApplicationException, 2), (read.Code, read.Location.Offset));
    }

    // CreateSorted sorts in the order of CEL, strings by code points: U+E000 comes before U+1F600, whose
    // UTF-16 form starts with a surrogate that an ordinal comparison of code units would put first.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "R-5")]
    public void Sorted_IsAscendingInTheOrderOfCel()
    {
        CelList numbers = CelList.CreateSorted(new HashSet<long> { 3, 1, 2 }, CelValue.FromInt64);
        CelList strings = CelList.CreateSorted(Unsorted, CelValue.FromString);

        Assert.Equal(new[] { CelValue.FromInt64(1), CelValue.FromInt64(2), CelValue.FromInt64(3) }, numbers);
        Assert.Equal(ByCodePoints, strings.Select(value => value.TryGetString(out string? text) ? text : null));
    }

    // Elements that CEL cannot sort into one order are refused; an exception of the converter passes to the
    // caller.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "R-5")]
    public void SortedWithElementsThatCelCannotSort_IsArgumentException()
    {
        var refused = new[]
        {
            new[] { CelValue.FromInt64(1), CelValue.Null },
            new[] { CelValue.FromInt64(1), CelValue.FromString("a") },
            new[] { CelValue.FromDouble(double.NaN) },
            new[] { CelValue.FromInt64(1), CelValue.FromInt64(1) },
            new[] { CelValue.FromDouble(0.0), CelValue.FromDouble(-0.0) },
            new[] { CelValue.FromMap(CelMap.Create([])) },
        };

        Assert.All(refused, values => Assert.Throws<ArgumentException>(() => CelList.CreateSorted(values, value => value)));
        Assert.Throws<InvalidOperationException>(() => CelList.CreateSorted(One, _ => throw new InvalidOperationException()));
    }

    // The order of a map view: the order of a Dictionary, or of any source with keepSourceOrder; otherwise
    // ascending CEL keys. A ReadOnlyDictionary is not one of the ordered dictionaries.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "R-5")]
    public void MapView_EnumeratesInSourceOrderOrByAscendingKey()
    {
        var source = new Dictionary<string, long> { ["b"] = 2, ["a"] = 1, ["c"] = 3 };
        var wrapped = new ReadOnlyDictionary<string, long>(source);

        Assert.Equal(SourceOrder, Keys(CelMap.CreateView(source, CelValue.FromInt64, keepSourceOrder: false)));
        Assert.Equal(Ascending, Keys(CelMap.CreateView(wrapped, CelValue.FromInt64, keepSourceOrder: false)));
        Assert.Equal(SourceOrder, Keys(CelMap.CreateView(wrapped, CelValue.FromInt64, keepSourceOrder: true)));
        Assert.Equal(Ascending, Keys(CelMap.CreateView(source.ToImmutableDictionary(), CelValue.FromInt64, keepSourceOrder: false)));
    }

    // A CEL key converts back to the key of the dictionary: numeric keys meet on one number line, an
    // enumeration key is its underlying value, and a key of another kind or out of the range of the key type
    // finds nothing, without an error.
    [Theory]
    [Trait("Requirement", "F-2")]
    [InlineData("mv[1] == 'a' && mv[1u] == 'a' && mv[1.0] == 'a'", true)]
    [InlineData("'1' in mv", false)]
    [InlineData("5000000000 in mv", false)]
    [InlineData("1.5 in mv", false)]
    [InlineData("mv.map(k, k) == [1, 2]", true)]
    public void MapViewOverIntKeys_FindsKeysOnOneNumberLine(string expression, bool expected)
    {
        var source = new Dictionary<int, string> { [1] = "a", [2] = "b" };

        Assert.Equal(expected, Evaluate<bool>(expression, new CelBindings().Add("mv", CelMap.CreateView(source, CelValue.FromString, keepSourceOrder: false))));
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void MapViewKeys_AreIntUIntBoolOrString()
    {
        CelMap days = CelMap.CreateView(new Dictionary<DayOfWeek, long> { [DayOfWeek.Friday] = 5 }, CelValue.FromInt64, keepSourceOrder: false);
        CelMap unsigned = CelMap.CreateView(new Dictionary<uint, long> { [1] = 1 }, CelValue.FromInt64, keepSourceOrder: false);
        CelMap flags = CelMap.CreateView(new Dictionary<bool, long> { [true] = 1 }, CelValue.FromInt64, keepSourceOrder: false);

        Assert.True(Evaluate<bool>("mv[5] == 5", new CelBindings().Add("mv", days)));
        Assert.True(Evaluate<bool>("mv[1] == 1 && mv[1u] == 1 && !(-1 in mv)", new CelBindings().Add("mv", unsigned)));
        Assert.True(Evaluate<bool>("mv[true] == 1 && !(1 in mv)", new CelBindings().Add("mv", flags)));
        Assert.Throws<ArgumentException>(() => CelMap.CreateView(new Dictionary<double, long>(), CelValue.FromInt64, keepSourceOrder: false));
        Assert.Throws<ArgumentException>(() => CelMap.CreateView(new Dictionary<char, long>(), CelValue.FromInt64, keepSourceOrder: false));
        Assert.Throws<ArgumentException>(() => CelMap.CreateView(new Dictionary<DateTime, long>(), CelValue.FromInt64, keepSourceOrder: false));
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "+R-9")]
    public void ExceptionOfAMapViewValue_IsApplicationExceptionAtTheLookup()
    {
        CelMap view = CelMap.CreateView(
            new Dictionary<string, long> { ["k"] = 1 },
            _ => throw new InvalidOperationException(),
            keepSourceOrder: false);

        CelError error = SingleError("mv['k']", new CelBindings().Add("mv", view));

        Assert.Equal(CelErrorCode.ApplicationException, error.Code);
        Assert.Equal("application code threw System.InvalidOperationException in a map of the application", error.Message);
        Assert.Equal(2, error.Location.Offset);
        Assert.True(Evaluate<bool>("size(mv) == 1 && 'k' in mv", new CelBindings().Add("mv", view)));
    }

    // A Guid key is the string of format D in lower case, and only that string finds it: upper case, braces,
    // parentheses, the format N and a leading space do not, so a map of Guid keys behaves as a map of its
    // canonical strings. The same holds for a dictionary bound by its static type.
    [Theory]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "R-5")]
    [InlineData("mv['" + CanonicalGuid + "'] == 1", true)]
    [InlineData("'" + CanonicalGuid + "' in mv", true)]
    [InlineData("mv.map(k, k) == ['" + CanonicalGuid + "']", true)]
    [InlineData("mv == {'" + CanonicalGuid + "': 1}", true)]
    [InlineData("'0F8FAD5B-D9CB-469F-A165-70867728950E' in mv", false)]
    [InlineData("'0f8fad5b-d9cb-469f-a165-70867728950E' in mv", false)]
    [InlineData("'{" + CanonicalGuid + "}' in mv", false)]
    [InlineData("'(" + CanonicalGuid + ")' in mv", false)]
    [InlineData("'0f8fad5bd9cb469fa16570867728950e' in mv", false)]
    [InlineData("' " + CanonicalGuid + "' in mv", false)]
    public void MapViewOverGuidKeys_FindsOnlyTheCanonicalString(string expression, bool expected)
    {
        Assert.Equal(expected, Evaluate<bool>(expression, new CelBindings().Add("mv", GuidMap())));
        Assert.Equal(expected, Evaluate<bool>(expression, new CelBindings().Add("mv", (IReadOnlyDictionary<Guid, long>)GuidSource())));
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "R-5")]
    public void MapViewOverGuidKeys_LooksUpOnlyTheCanonicalStringThroughTheApi()
    {
        CelMap map = GuidMap();

        Assert.True(map.TryGetValue(CelValue.FromString(CanonicalGuid), out CelValue value));
        Assert.Equal(CelValue.FromInt64(1), value);
        Assert.False(map.TryGetValue(CelValue.FromString(CanonicalGuid.ToUpperInvariant()), out _));
        Assert.Equal(CelValue.FromString(CanonicalGuid), Assert.Single(map).Key);

        CelError error = SingleError("mv['0F8FAD5B-D9CB-469F-A165-70867728950E']", new CelBindings().Add("mv", map));
        Assert.Equal((CelErrorCode.NoSuchKey, 2), (error.Code, error.Location.Offset));
        Assert.Equal("no such key: \"0F8FAD5B-D9CB-469F-A165-70867728950E\"", error.Message);
    }

    private static Dictionary<Guid, long> GuidSource() => new() { [Guid.Parse(CanonicalGuid)] = 1 };

    private static CelMap GuidMap() => CelMap.CreateView(GuidSource(), CelValue.FromInt64, keepSourceOrder: false);

    private static CelList ThrowingAt(int index)
    {
        var source = new List<long> { 1, 2, 3 };
        return CelList.CreateView(source, value => value == source[index] ? throw new InvalidOperationException() : CelValue.FromInt64(value));
    }

    private static string?[] Keys(CelMap map) =>
        map.Select(entry => entry.Key.TryGetString(out string? key) ? key : null).ToArray();

    private T Evaluate<T>(string expression, CelBindings bindings) =>
        environment.Compile<T>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();

    private CelError SingleError(string expression, CelBindings bindings)
    {
        CelEvalResult<CelValue> result = environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorStage.Evaluate, error.Stage);
        return error;
    }
}
