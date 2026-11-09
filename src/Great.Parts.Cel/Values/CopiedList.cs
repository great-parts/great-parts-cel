using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

/// <summary>
/// A <see cref="CelList"/> over an array of values that the input rows copied from a collection of the application: a set
/// sorted by the order of CEL, or a collection without an index. It records the CEL type of the row, so that the check against a
/// declared type does not descend into a copy that fits by construction.
/// </summary>
/// <param name="items">The elements in order; the list takes over the array and the caller does not change it afterwards.</param>
/// <param name="recordedType">The CEL type that the input rows gave the .NET type of the collection.</param>
internal sealed class CopiedList(CelValue[] items, CelType recordedType) : CelList
{
    internal override CelType? RecordedType => recordedType;

    private protected override int CountCore => items.Length;

    private protected override CelValue GetCore(int index) => items[index];
}
