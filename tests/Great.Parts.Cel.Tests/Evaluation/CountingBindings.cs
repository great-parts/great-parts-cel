using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Tests.Evaluation;

// Bindings that count the lookups of names.
internal sealed class CountingBindings(Dictionary<string, CelValue> values) : IReadOnlyDictionary<string, CelValue>
{
    internal int Lookups { get; private set; }

    public int Count => values.Count;

    public IEnumerable<string> Keys => values.Keys;

    public IEnumerable<CelValue> Values => values.Values;

    public CelValue this[string key]
    {
        get
        {
            Lookups++;
            return values[key];
        }
    }

    public bool ContainsKey(string key)
    {
        Lookups++;
        return values.ContainsKey(key);
    }

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out CelValue value)
    {
        Lookups++;
        return values.TryGetValue(key, out value);
    }

    public IEnumerator<KeyValuePair<string, CelValue>> GetEnumerator() => values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
