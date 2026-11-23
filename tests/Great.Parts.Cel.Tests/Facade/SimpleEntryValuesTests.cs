using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Orders;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// The closed list of the values of the simple entry, row by row, on the rows that the examples do not use: what each row reads and
// what each value outside the list is. The values go through a dictionary of names and values.
[Trait("Requirement", "U-7")]
public sealed class SimpleEntryValuesTests
{
    private static T Eval<T>(string text, string name, object? value) =>
        CelExpression.Evaluate<T>(text, new Dictionary<string, object?> { [name] = value }, cancellationToken: CancellationToken.None);

    private static void AssertNotDeclared(object? value, string typeName)
    {
        CelException exception = Assert.Throws<CelException>(() => Eval<bool>("true", "v", value));
        CelError error = Assert.Single(exception.Errors);

        Assert.Equal(CelErrorCode.TypeNotDeclared, error.Code);
        Assert.Equal(typeName, error.Subject);
        Assert.Contains("'v'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Integers_AreIntAndUint()
    {
        Assert.Equal(6L, Eval<long>("v + 1", "v", (short)5));
        Assert.Equal(6L, Eval<long>("v + 1", "v", (sbyte)5));
        Assert.Equal(6L, Eval<long>("v + 1", "v", 5));
        Assert.Equal(6UL, Eval<ulong>("v + 1u", "v", (byte)5));
        Assert.Equal(6UL, Eval<ulong>("v + 1u", "v", (ushort)5));
        Assert.Equal(6UL, Eval<ulong>("v + 1u", "v", 5U));
        Assert.Equal(6UL, Eval<ulong>("v + 1u", "v", 5UL));
    }

    [Fact]
    public void Nullable_IsItsValueOrNull()
    {
        long? present = 5;
        long? absent = null;

        Assert.Equal(6L, Eval<long>("v + 1", "v", present));
        Assert.True(Eval<bool>("v == null", "v", absent));
    }

    [Fact]
    public void Guid_IsAString()
    {
        var guid = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");

        Assert.Equal("0f8fad5b-d9cb-469f-a165-70867728950e", Eval<string>("v", "v", guid));
    }

    [Fact]
    public void Bytes_AreBytesNotAList()
    {
        Assert.Equal(3L, Eval<long>("size(v)", "v", new byte[] { 1, 2, 3 }));
        Assert.Equal(2L, Eval<long>("size(v)", "v", new ReadOnlyMemory<byte>([1, 2])));
        Assert.True(Eval<bool>("v == b'\\x01\\x02'", "v", new byte[] { 1, 2 }));
    }

    [Fact]
    public void Time_IsATimestampAndADuration()
    {
        Assert.Equal(2026L, Eval<long>("v.getFullYear()", "v", new DateTime(2026, 5, 6, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(2026L, Eval<long>("v.getFullYear()", "v", new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero)));
        Assert.True(Eval<bool>("v == duration('90m')", "v", TimeSpan.FromMinutes(90)));
    }

    [Fact]
    public void JsonElementAndCelValue_AreDyn()
    {
        using JsonDocument document = JsonDocument.Parse("{\"s\":\"x\"}");

        Assert.True(Eval<bool>("v.s == 'x'", "v", document.RootElement));
        Assert.Equal(6L, Eval<long>("v + 1", "v", CelValue.FromInt64(5)));
        Assert.Equal("a", Eval<string>("v", "v", CelValue.FromString("a")));
    }

    [Fact]
    public void CelListAndCelMap_AreTakenAsTheyAre()
    {
        CelList list = CelList.Create(CelValue.FromInt64(1), CelValue.FromString("a"));
        CelMap map = CelMap.Create([new KeyValuePair<CelValue, CelValue>(CelValue.FromString("k"), CelValue.FromInt64(7))]);

        Assert.Equal(2L, Eval<long>("size(v)", "v", list));
        Assert.True(Eval<bool>("v[1] == 'a'", "v", list));
        Assert.Equal(7L, Eval<long>("v.k", "v", map));
    }

    [Fact]
    public void Lists_AreTypedByTheirElements()
    {
        Assert.Equal(6L, Eval<long>("v[0] + v[1] + v[2]", "v", new List<long> { 1, 2, 3 }));
        Assert.Equal(6L, Eval<long>("v[0] + v[1] + v[2]", "v", new long[] { 1, 2, 3 }));
        Assert.Equal("ab", Eval<string>("v[0] + v[1]", "v", ImmutableArray.Create("a", "b")));
        Assert.Equal(2.5, Eval<double>("v[0] + v[1]", "v", (IReadOnlyList<double>)[1.0, 1.5]));
        Assert.Equal(2L, Eval<long>("size(v)", "v", new List<long?> { 1, null }));
        Assert.Equal(2L, Eval<long>("size(v)", "v", new Guid[2]));
    }

    [Fact]
    public void ListOfObjects_IsListOfDynOnlyWhenTheElementsAreExactlyObject()
    {
        Assert.Equal(2L, Eval<long>("size(v)", "v", new object[] { 1L, "a" }));
        Assert.True(Eval<bool>("v[0] == 1 && v[1] == 'a'", "v", new List<object> { 1L, "a" }));
        Assert.Equal(2L, Eval<long>("size(v)", "v", new Collection<object> { 1L, "a" }));

        // An IList<object> that is not a list of the rows and not an IReadOnlyList<object>, and the lists that fit
        // IReadOnlyList<object> only by covariance, are outside the list.
        AssertNotDeclared(new ObjectOnlyList(), typeof(ObjectOnlyList).FullName!);
        AssertNotDeclared(new List<Order>(), typeof(List<Order>).FullName!);
        AssertNotDeclared(Array.Empty<Order>(), typeof(Order[]).FullName!);
    }

    [Fact]
    public void ObjectOfTheApplicationInAnObjectList_IsTypeNotAccessibleWhenRead()
    {
        var list = new object[] { new Order { Id = "ORD-1" } };

        Assert.Equal(1L, Eval<long>("size(v)", "v", list));
        CelException exception = Assert.Throws<CelException>(() => Eval<string>("v[0].id", "v", list));
        Assert.Equal(CelErrorCode.TypeNotAccessible, Assert.Single(exception.Errors).Code);
    }

    [Fact]
    public void Dictionaries_AreTypedByTheirValues()
    {
        Assert.Equal(7L, Eval<long>("v.k + 1", "v", new Dictionary<string, long> { ["k"] = 6 }));
        Assert.Equal(7L, Eval<long>("v.k + 1", "v", ImmutableDictionary<string, long>.Empty.Add("k", 6)));
        Assert.Equal("ab", Eval<string>("v.x + v.y", "v", new Dictionary<string, string> { ["x"] = "a", ["y"] = "b" }));
        Assert.True(Eval<bool>("v.k == 1 && v.s == 'a'", "v", new Dictionary<string, object> { ["k"] = 1L, ["s"] = "a" }));
    }

    [Fact]
    public void TypedDictionaryAsData_GivesOneTypeToAllVariables()
    {
        var typed = new Dictionary<string, double> { ["a"] = 5.0, ["b"] = 2.0 };

#pragma warning disable GPCEL010 // the dictionary passed as object is the subject of the test
        Assert.Equal(2.5, CelExpression.Evaluate<double>("a / b", (object)typed, cancellationToken: CancellationToken.None));
        Assert.Equal(3L, CelExpression.Evaluate<long>("a + b", (object)ImmutableDictionary<string, long>.Empty.Add("a", 1).Add("b", 2), cancellationToken: CancellationToken.None));
#pragma warning restore GPCEL010
    }

    [Fact]
    public void ValuesOutsideTheList_AreTypeNotDeclaredWithTheNameOfTheType()
    {
        AssertNotDeclared(1.5f, typeof(float).FullName!);
        AssertNotDeclared((Half)1, typeof(Half).FullName!);
        AssertNotDeclared(DayOfWeek.Monday, typeof(DayOfWeek).FullName!);
        AssertNotDeclared((nint)1, typeof(nint).FullName!);
        AssertNotDeclared(CelTimestamp.FromDateTimeOffset(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)), typeof(CelTimestamp).FullName!);
        AssertNotDeclared(new CelDuration(1, 0), typeof(CelDuration).FullName!);
        AssertNotDeclared(new short[] { 1 }, typeof(short[]).FullName!);
        AssertNotDeclared(new HashSet<long> { 1 }, typeof(HashSet<long>).FullName!);
        AssertNotDeclared(new Dictionary<string, short> { ["a"] = 1 }, typeof(Dictionary<string, short>).FullName!);
        AssertNotDeclared(new Dictionary<int, long> { [1] = 1 }, typeof(Dictionary<int, long>).FullName!);
        AssertNotDeclared(new Customer(), typeof(Customer).FullName!);
        AssertNotDeclared(1.5m, typeof(decimal).FullName!);
    }

    [Fact]
    public void Decimal_IsAdvisedToTheExtensionSet()
    {
        CelError error = Assert.Single(Assert.Throws<CelException>(() => Eval<bool>("true", "price", 1.5m)).Errors);

        Assert.Contains("decimal", error.Message, StringComparison.Ordinal);
        Assert.Contains("CelEnvironment", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryValueOutsideTheList_IsAnErrorOfItsOwn()
    {
        var variables = new Dictionary<string, object?> { ["b"] = 1.5f, ["a"] = (Half)1, ["ok"] = 1L };

        CelException exception = Assert.Throws<CelException>(() => CelExpression.Evaluate<bool>("true", variables, cancellationToken: CancellationToken.None));

        Assert.Equal(2, exception.Errors.Length);
        Assert.Contains("'a'", exception.Errors[0].Message, StringComparison.Ordinal);
        Assert.Contains("'b'", exception.Errors[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ArgumentErrors_NameTheirParameter()
    {
        var empty = new Dictionary<string, object?> { [""] = 1L };
        var local = new Dictionary<string, object?> { ["t"] = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified) };
        var undefined = new Dictionary<string, object?> { ["j"] = default(JsonElement) };

        Assert.Equal("variables", Assert.Throws<ArgumentException>(() => CelExpression.Evaluate<long>("1", empty, cancellationToken: CancellationToken.None)).ParamName);
        Assert.Equal("variables", Assert.Throws<ArgumentException>(() => CelExpression.Evaluate<long>("1", local, cancellationToken: CancellationToken.None)).ParamName);
        Assert.Equal("variables", Assert.Throws<ArgumentException>(() => CelExpression.Evaluate<long>("1", undefined, cancellationToken: CancellationToken.None)).ParamName);
#pragma warning disable GPCEL010 // the dictionary passed as object is the subject of the test
        Assert.Equal("data", Assert.Throws<ArgumentException>(() => CelExpression.Evaluate<long>("1", (object)local, cancellationToken: CancellationToken.None)).ParamName);
#pragma warning restore GPCEL010
    }

    [Fact]
    public void ResultTypeOutsideTheMapping_IsAnArgumentException()
    {
        Assert.Throws<ArgumentException>(() => CelExpression.Evaluate<short>("1", new Dictionary<string, object?>(), cancellationToken: CancellationToken.None));
    }

    [Fact]
    public void ExpressionOfTheOtherType_IsExpectedTypeMismatch()
    {
        CelException exception = Assert.Throws<CelException>(() => CelExpression.Evaluate<string>("1", new Dictionary<string, object?>(), cancellationToken: CancellationToken.None));

        Assert.Equal(CelErrorCode.ExpectedTypeMismatch, Assert.Single(exception.Errors).Code);
    }

    [Fact]
    public void ObjectResult_IsTheValueOfTheResultRow()
    {
        Assert.Equal(3L, CelExpression.Evaluate<object>("1 + 2", new Dictionary<string, object?>(), cancellationToken: CancellationToken.None));
    }

    // IList<object> without IReadOnlyList<object>: the registry of rows does not know it.
    private sealed class ObjectOnlyList : IList<object>
    {
        private readonly List<object> _items = [];

        public object this[int index]
        {
            get => _items[index];
            set => _items[index] = value;
        }

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        public void Add(object item) => _items.Add(item);

        public void Clear() => _items.Clear();

        public bool Contains(object item) => _items.Contains(item);

        public void CopyTo(object[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

        public IEnumerator<object> GetEnumerator() => _items.GetEnumerator();

        public int IndexOf(object item) => _items.IndexOf(item);

        public void Insert(int index, object item) => _items.Insert(index, item);

        public bool Remove(object item) => _items.Remove(item);

        public void RemoveAt(int index) => _items.RemoveAt(index);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
