using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// The adapters of the collections of the application: CelList.CreateView, CreateCopy and CreateSorted and
// CelMap.CreateView, read through the public API and through expressions.
public sealed class ApplicationCollectionTests
{
    private const string Identifier = "6f2c1a9e-0b3d-4c5e-8f7a-1d2e3f4a5b6c";

    private static readonly string[] SortedStrings = ["a", "", "\U0001F600"];

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<CelList>("l")
        .AddVariable<CelMap>("m")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void ListView_ConvertsAnElementAtEachAccessAndReflectsItsSource()
    {
        var source = new List<long> { 10, 20, 30 };
        int calls = 0;

        CelList view = CelList.CreateView(source, value =>
        {
            calls++;
            return CelValue.FromInt64(value * 2);
        });

        Assert.Equal(3, view.Count);
        Assert.Equal(0, calls);
        Assert.Equal(CelValue.FromInt64(40), view[1]);
        Assert.Equal(1, calls);
        source[1] = 5;
        Assert.Equal(CelValue.FromInt64(10), view[1]);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Views_RefuseNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => CelList.CreateView<long>(null!, CelValue.FromInt64));
        Assert.Throws<ArgumentNullException>(() => CelList.CreateView(new List<long>(), null!));
        Assert.Throws<ArgumentNullException>(() => CelList.CreateCopy<long>(null!, CelValue.FromInt64));
        Assert.Throws<ArgumentNullException>(() => CelList.CreateSorted(new HashSet<long>(), null!));
        Assert.Throws<ArgumentNullException>(() => CelMap.CreateView<string, long>(null!, CelValue.FromInt64, false));
        Assert.Throws<ArgumentNullException>(() => CelMap.CreateView(new Dictionary<string, long>(), null!, false));
    }

    // An exception of the converter is the element; an operation that does not read it does not fail, and the one
    // that reads it fails at its own node, with the exception kept.
    [Theory]
    [InlineData("l[1]", 1)]
    [InlineData("l == [1, 2, 3]", 2)]
    [InlineData("2 in l", 2)]
    public void ExceptionOfTheConverter_FailsWhereTheElementIsRead(string expression, int offset)
    {
        CelBindings bindings = new CelBindings().Add("l", FailingAtTwo());

        CelEvalResult<CelValue> result = environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.ApplicationException, error.Code);
        Assert.Equal("application code threw System.InvalidOperationException in a list of the application", error.Message);
        Assert.Equal(offset, error.Location.Offset);
        Assert.IsType<InvalidOperationException>(error.Exception);
        Assert.True(environment.Compile<bool>("l[0] == 1 && size(l) == 3 && 1 in l").GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    [Fact]
    public void ExceptionOfTheSourceOfAListView_IsTheElement()
    {
        CelList view = CelList.CreateView(new ThrowingList(), CelValue.FromInt64);

        Assert.Equal(2, view.Count);
        Assert.Equal(CelValue.FromInt64(7), view[0]);
        Assert.True(view[1].TryGetError(out CelError? error));
        Assert.Equal(CelErrorCode.ApplicationException, error.Code);
        Assert.IsType<NotSupportedException>(error.Exception);
    }

    [Fact]
    public void Copy_ConvertsNowInTheOrderOfEnumerationAndKeepsFailedElements()
    {
        var source = new Queue<long>([5, 3, 4]);

        CelList copy = CelList.CreateCopy(source, value => value == 3 ? throw new InvalidOperationException() : CelValue.FromInt64(value));
        source.Enqueue(9);

        Assert.Equal(3, copy.Count);
        Assert.Equal(CelValue.FromInt64(5), copy[0]);
        Assert.True(copy[1].TryGetError(out CelError? error));
        Assert.Equal(CelErrorCode.ApplicationException, error.Code);
        Assert.Equal(CelValue.FromInt64(4), copy[2]);
    }

    [Fact]
    public void Sorted_IsAscendingByTheOrderOfCelAndStringsByCodePoints()
    {
        CelList numbers = CelList.CreateSorted(new HashSet<double> { 2.5, -4, 0.25 }, CelValue.FromDouble);
        CelList strings = CelList.CreateSorted(new HashSet<string> { "\U0001F600", "a", "" }, CelValue.FromString);
        CelList empty = CelList.CreateSorted(new HashSet<long>(), CelValue.FromInt64);

        Assert.Equal(new[] { CelValue.FromDouble(-4), CelValue.FromDouble(0.25), CelValue.FromDouble(2.5) }, numbers);
        Assert.Equal(SortedStrings, strings.Select(value => value.TryGetString(out string? text) ? text : null));
        Assert.Empty(empty);
    }

    [Fact]
    public void Sorted_RefusesElementsThatCelCannotPutInOneOrder()
    {
        CelValue[][] refused =
        [
            [CelValue.FromInt64(1), CelValue.Null],
            [CelValue.FromInt64(1), CelValue.FromUInt64(1)],
            [CelValue.FromDouble(double.NaN)],
            [CelValue.FromString("a"), CelValue.FromString("a")],
            [CelValue.FromDouble(0.0), CelValue.FromDouble(-0.0)],
            [CelValue.FromMap(CelMap.Create([]))],
            [CelValue.Null],
        ];

        Assert.All(refused, values => Assert.Throws<ArgumentException>(() => CelList.CreateSorted(values, value => value)));
        Assert.Throws<InvalidOperationException>(() => CelList.CreateSorted(new long[] { 1 }, _ => throw new InvalidOperationException()));
    }

    [Fact]
    public void MapView_EnumeratesInSourceOrderOrAscendingByKey()
    {
        var dictionary = new Dictionary<string, long> { ["q"] = 1, ["c"] = 2, ["m"] = 3 };
        var wrapped = new ReadOnlyDictionary<string, long>(dictionary);
        var sorted = new SortedDictionary<string, long>(dictionary, StringComparer.Ordinal);

        Assert.Equal(["q", "c", "m"], KeysOf(CelMap.CreateView(dictionary, CelValue.FromInt64, keepSourceOrder: false)));
        Assert.Equal(["c", "m", "q"], KeysOf(CelMap.CreateView(wrapped, CelValue.FromInt64, keepSourceOrder: false)));
        Assert.Equal(["q", "c", "m"], KeysOf(CelMap.CreateView(wrapped, CelValue.FromInt64, keepSourceOrder: true)));
        Assert.Equal(["c", "m", "q"], KeysOf(CelMap.CreateView(dictionary.ToImmutableDictionary(), CelValue.FromInt64, keepSourceOrder: false)));
        Assert.Equal(["c", "m", "q"], KeysOf(CelMap.CreateView(sorted, CelValue.FromInt64, keepSourceOrder: false)));
    }

    [Fact]
    public void MapView_SortsByCodePointsWhenTheSourceHasNoOrder()
    {
        var wrapped = new ReadOnlyDictionary<string, long>(new Dictionary<string, long> { ["\U0001F600"] = 1, [""] = 2, ["a"] = 3 });

        Assert.Equal(SortedStrings, KeysOf(CelMap.CreateView(wrapped, CelValue.FromInt64, keepSourceOrder: false)));
    }

    [Theory]
    [InlineData("m[3] == 'c' && m[3u] == 'c' && m[3.0] == 'c'", true)]
    [InlineData("'3' in m", false)]
    [InlineData("4294967299 in m", false)]
    [InlineData("3.5 in m", false)]
    [InlineData("true in m", false)]
    [InlineData("m.map(k, k) == [3, 4]", true)]
    public void MapViewOverIntKeys_FindsKeysOnTheNumberLine(string expression, bool expected)
    {
        var source = new Dictionary<int, string> { [3] = "c", [4] = "d" };

        Assert.Equal(expected, Evaluate<bool>(expression, new CelBindings().Add("m", CelMap.CreateView(source, CelValue.FromString, keepSourceOrder: false))));
    }

    [Fact]
    public void MapView_ConvertsTheKeyBackWithinTheRangeOfTheKeyType()
    {
        CelMap bytes = CelMap.CreateView(new Dictionary<byte, long> { [200] = 1 }, CelValue.FromInt64, keepSourceOrder: false);
        CelMap signed = CelMap.CreateView(new Dictionary<sbyte, long> { [-5] = 1 }, CelValue.FromInt64, keepSourceOrder: false);
        CelMap wide = CelMap.CreateView(new Dictionary<ulong, long> { [ulong.MaxValue] = 1 }, CelValue.FromInt64, keepSourceOrder: false);
        CelMap low = CelMap.CreateView(new Dictionary<long, long> { [long.MinValue] = 1 }, CelValue.FromInt64, keepSourceOrder: false);
        CelMap days = CelMap.CreateView(new Dictionary<DayOfWeek, long> { [DayOfWeek.Friday] = 1 }, CelValue.FromInt64, keepSourceOrder: false);

        Assert.True(bytes.TryGetValue(CelValue.FromInt64(200), out _));
        Assert.False(bytes.TryGetValue(CelValue.FromInt64(300), out _));
        Assert.False(bytes.TryGetValue(CelValue.FromInt64(-1), out _));
        Assert.True(signed.TryGetValue(CelValue.FromInt64(-5), out _));
        Assert.False(signed.TryGetValue(CelValue.FromUInt64(200), out _));
        Assert.True(wide.TryGetValue(CelValue.FromUInt64(ulong.MaxValue), out _));
        Assert.False(wide.TryGetValue(CelValue.FromInt64(-1), out _));
        Assert.True(low.TryGetValue(CelValue.FromInt64(long.MinValue), out _));
        Assert.True(days.TryGetValue(CelValue.FromInt64(5), out _));
        Assert.False(days.TryGetValue(CelValue.FromString("Friday"), out _));
    }

    [Fact]
    public void MapView_RefusesAKeyTypeThatIsNoMapKey()
    {
        Assert.Throws<ArgumentException>(() => CelMap.CreateView(new Dictionary<double, long>(), CelValue.FromInt64, keepSourceOrder: false));
        Assert.Throws<ArgumentException>(() => CelMap.CreateView(new Dictionary<char, long>(), CelValue.FromInt64, keepSourceOrder: false));
        Assert.Throws<ArgumentException>(() => CelMap.CreateView(new Dictionary<DateTime, long>(), CelValue.FromInt64, keepSourceOrder: false));
    }

    [Fact]
    public void MapView_GivesAnEnumerationKeyAboveTheRangeAsAnErrorKey()
    {
        CelMap view = CelMap.CreateView(new Dictionary<Wide, long> { [Wide.One] = 1, [Wide.Top] = 2 }, CelValue.FromInt64, keepSourceOrder: false);

        KeyValuePair<CelValue, CelValue>[] entries = [.. view];

        Assert.Equal(CelValueKind.Int, entries[0].Key.Kind);
        Assert.True(entries[1].Key.TryGetError(out CelError? error));
        Assert.Equal(CelErrorCode.ConversionRange, error.Code);
        Assert.Equal("18446744073709551615 is out of the range of int", error.Message);
    }

    [Fact]
    public void MapView_ConvertsAValueAtEachReadAndTurnsAnExceptionIntoTheValue()
    {
        var source = new Dictionary<string, long> { ["k"] = 1 };
        CelMap view = CelMap.CreateView(source, _ => throw new InvalidOperationException(), keepSourceOrder: false);

        CelEvalResult<CelValue> result = environment.Compile<CelValue>("m['k']").GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("m", view), cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.ApplicationException, error.Code);
        Assert.Equal("application code threw System.InvalidOperationException in a map of the application", error.Message);
        Assert.Equal(1, error.Location.Offset);
        Assert.True(Evaluate<bool>("size(m) == 1 && 'k' in m", new CelBindings().Add("m", view)));
    }

    // A Guid key is the string of format D in lower case, found by that string only.
    [Theory]
    [InlineData(Identifier, true)]
    [InlineData("6F2C1A9E-0B3D-4C5E-8F7A-1D2E3F4A5B6C", false)]
    [InlineData("6f2c1a9e-0b3d-4c5e-8f7a-1d2e3f4a5b6C", false)]
    [InlineData("{" + Identifier + "}", false)]
    [InlineData("6f2c1a9e0b3d4c5e8f7a1d2e3f4a5b6c", false)]
    [InlineData(" " + Identifier, false)]
    [InlineData(Identifier + " ", false)]
    public static void MapViewOverGuidKeys_FindsOnlyTheCanonicalString(string key, bool found)
    {
        CelMap view = CelMap.CreateView(new Dictionary<Guid, long> { [Guid.Parse(Identifier)] = 1 }, CelValue.FromInt64, keepSourceOrder: false);

        Assert.Equal(found, view.TryGetValue(CelValue.FromString(key), out _));
        Assert.Equal(Identifier, Assert.Single(view).Key.TryGetString(out string? text) ? text : null);
    }

    private static CelList FailingAtTwo() => CelList.CreateView(
        new List<long> { 1, 2, 3 },
        value => value == 2 ? throw new InvalidOperationException() : CelValue.FromInt64(value));

    private static string?[] KeysOf(CelMap map) => [.. map.Select(entry => entry.Key.TryGetString(out string? key) ? key : null)];

    private T Evaluate<T>(string expression, CelBindings bindings) =>
        environment.Compile<T>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();

    private enum Wide : ulong
    {
        One = 1,
        Top = ulong.MaxValue,
    }

    private sealed class ThrowingList : IReadOnlyList<long>
    {
        public int Count => 2;

        public long this[int index] => index == 0 ? 7 : throw new NotSupportedException();

        public IEnumerator<long> GetEnumerator() => throw new NotSupportedException();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
