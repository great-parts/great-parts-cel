namespace Great.Parts.Cel.Values;

/// <summary>
/// A <see cref="CelList"/> over an array of values that only it holds: a list literal and
/// <see cref="CelList.Create"/> fill the array once, and nothing changes it afterwards.
/// </summary>
internal sealed class ValueArrayList : CelList
{
    private readonly CelValue[] _items;

    /// <summary>
    /// Creates a list that takes over <paramref name="items"/>; the caller does not change the array afterwards.
    /// </summary>
    /// <param name="items">The elements in order, none of them an error value.</param>
    internal ValueArrayList(CelValue[] items)
    {
        _items = items;
    }

    private protected override int CountCore => _items.Length;

    private protected override CelValue GetCore(int index) => _items[index];
}
