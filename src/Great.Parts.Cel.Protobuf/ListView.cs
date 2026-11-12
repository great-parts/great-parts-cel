using System.Collections;
using System.Collections.Generic;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// A read-only list of objects over the non-generic list of a repeated field: no element is copied, and an element is
/// boxed only when it is read.
/// </summary>
internal sealed class ObjectListView : IReadOnlyList<object?>
{
    private readonly IList _source;

    internal ObjectListView(IList source)
    {
        _source = source;
    }

    /// <inheritdoc/>
    public int Count => _source.Count;

    /// <inheritdoc/>
    public object? this[int index] => _source[index];

    /// <inheritdoc/>
    public IEnumerator<object?> GetEnumerator()
    {
        for (int index = 0; index < _source.Count; index++)
        {
            yield return _source[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
