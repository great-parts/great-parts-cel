using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Great.Parts.Cel.Generator.Samples.Rows;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Generator.Samples.Families;

// The collection rows of the type mapping ("Objects and collections"), each as a member, and the two classes that implement only the writable
// interface: a member declared IList<T> or IDictionary<K,V> is wrapped at run time only when the instance is not read-only
// (an instance of List<T> or Dictionary<K,V> is passed as it is).

public sealed class Line
{
    public string Sku { get; set; } = "";

    public long Qty { get; set; }
}

public sealed class OnlyList<T>(params T[] items) : IList<T>
{
    private readonly List<T> inner = [.. items];

    public T this[int index]
    {
        get => inner[index];
        set => inner[index] = value;
    }

    public int Count => inner.Count;

    public bool IsReadOnly => false;

    public void Add(T item) => inner.Add(item);

    public void Clear() => inner.Clear();

    public bool Contains(T item) => inner.Contains(item);

    public void CopyTo(T[] array, int arrayIndex) => inner.CopyTo(array, arrayIndex);

    public IEnumerator<T> GetEnumerator() => inner.GetEnumerator();

    public int IndexOf(T item) => inner.IndexOf(item);

    public void Insert(int index, T item) => inner.Insert(index, item);

    public bool Remove(T item) => inner.Remove(item);

    public void RemoveAt(int index) => inner.RemoveAt(index);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class OnlyDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> inner = [];

    public TValue this[TKey key]
    {
        get => inner[key];
        set => inner[key] = value;
    }

    public ICollection<TKey> Keys => inner.Keys;

    public ICollection<TValue> Values => inner.Values;

    public int Count => inner.Count;

    public bool IsReadOnly => false;

    public void Add(TKey key, TValue value) => inner.Add(key, value);

    public void Add(KeyValuePair<TKey, TValue> item) => ((IDictionary<TKey, TValue>)inner).Add(item);

    public void Clear() => inner.Clear();

    public bool Contains(KeyValuePair<TKey, TValue> item) => ((IDictionary<TKey, TValue>)inner).Contains(item);

    public bool ContainsKey(TKey key) => inner.ContainsKey(key);

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((IDictionary<TKey, TValue>)inner).CopyTo(array, arrayIndex);

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => inner.GetEnumerator();

    public bool Remove(TKey key) => inner.Remove(key);

    public bool Remove(KeyValuePair<TKey, TValue> item) => ((IDictionary<TKey, TValue>)inner).Remove(item);

    public bool TryGetValue(TKey key, out TValue value) => inner.TryGetValue(key, out value!);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public readonly struct Triple : IReadOnlyList<long>
{
    private readonly long first;
    private readonly long second;
    private readonly long third;

    public Triple(long first, long second, long third)
    {
        this.first = first;
        this.second = second;
        this.third = third;
    }

    public int Count => 3;

    public long this[int index] => index switch
    {
        0 => first,
        1 => second,
        2 => third,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    public IEnumerator<long> GetEnumerator()
    {
        yield return first;
        yield return second;
        yield return third;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class Collections
{
    // Lists: a view over the source, no copy.
    public long[] Array { get; set; } = [];

    public IReadOnlyList<string> ReadOnly { get; set; } = [];

    public List<long> Mutable { get; set; } = [];

    // ImmutableArray<T> is read without a copy and without a box. A default one has no array: it reads as UnexpectedNull and has() is false;
    // ImmutableArray<T>? follows the row of a Nullable<T> that has no wrapper: present only with a value that is not default.
    public ImmutableArray<long> Immutable { get; set; } = [];

    public ImmutableArray<long> NoArray { get; set; }

    public ImmutableArray<long>? OptArray { get; set; }

    // A structure that implements IReadOnlyList<T> and is not ImmutableArray<T> is boxed, and the view is made over the box.
    public Triple Fixed { get; set; } = new(1, 2, 3);

    public ImmutableList<long> ImmutableL { get; set; } = [];

    public ReadOnlyCollection<long> ReadOnlyColl { get; set; } = new List<long>().AsReadOnly();

    public IList<long> AsIList { get; set; } = new List<long>();

    // Sets: a sorted copy at every read, ascending in CEL order.
    public HashSet<long> Hash { get; set; } = [];

    public SortedSet<string> Sorted { get; set; } = [];

    public ISet<string> AsSet { get; set; } = new HashSet<string>();

    public IReadOnlySet<long> ReadOnlySet { get; set; } = new HashSet<long>();

    public ImmutableHashSet<long> ImmutableSet { get; set; } = [];

    // Elements that the type of the set accepts and CreateSorted refuses at run time: NaN, null, two equal elements.
    public HashSet<double> Doubles { get; set; } = [];

    public HashSet<string?> Texts { get; set; } = [];

    // Other collections with a size: a copy in the order of enumeration.
    public Queue<long> Queue { get; set; } = new();

    public Stack<string> Stack { get; set; } = new();

    public LinkedList<long> Linked { get; set; } = new();

    // Dictionaries: a view over the source; Dictionary keeps the order of its source, an unknown class is by ascending key.
    public Dictionary<string, long> Index { get; set; } = [];

    public SortedDictionary<string, long> SortedIndex { get; set; } = [];

    public IReadOnlyDictionary<string, long> ReadOnlyIndex { get; set; } = new Dictionary<string, long>();

    public IDictionary<string, long> AsIDictionary { get; set; } = new Dictionary<string, long>();

    public Dictionary<long, string> ByNumber { get; set; } = [];

    public Dictionary<Guid, long> ByGuid { get; set; } = [];

    public Dictionary<Status, long> ByStatus { get; set; } = [];

    // Elements and values at every depth.
    public List<List<long>> Nested { get; set; } = [];

    public Dictionary<string, List<Line>> Groups { get; set; } = [];

    public List<Line> Lines { get; set; } = [];

    public List<string?> NullableElements { get; set; } = [];

    public List<string> NotNullElements { get; set; } = [];

    public List<long?> OptElements { get; set; } = [];

    // A collection that is null is UnexpectedNull whatever its annotation (the CEL types list and map take no null), and has() is false;
    // the same for an element of a collection of collections, whether its position is annotated ? or not.
    public List<long>? NoList { get; set; }

    public List<long> MustList { get; set; } = null!;

    public List<List<long>?> HoleyNested { get; set; } = [];

    public Dictionary<string, List<long>> HoleyValues { get; set; } = [];
}

[CelObject(typeof(Collections), Name = "samples.Collections")]
[CelObject(typeof(Line), Name = "samples.Line")]
public sealed partial class CollectionContext : CelTypeContext
{
}
