using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Filtering;

/// <summary>
/// A field of a filter ready to be translated: the text it was written as, the path resolved against the environment, where its parts
/// are in the filter, and the key that each step through a map names.
/// </summary>
internal sealed class FieldRef
{
    // A key per part of the path: the constant of the key at a step through a map, null at the other steps.
    private readonly CelValue?[] _keys;

    internal FieldRef(FilterMember member, ResolvedPath path, PathPlace place, CelValue?[] keys)
    {
        Member = member;
        Path = path;
        Place = place;
        _keys = keys;
    }

    /// <summary>The field as written in the filter; the subject of its errors.</summary>
    internal FilterMember Member { get; }

    /// <summary>The path resolved against the environment.</summary>
    internal ResolvedPath Path { get; }

    /// <summary>Where the parts of the path are in the filter.</summary>
    internal PathPlace Place { get; }

    /// <summary>The key of the step through a map at a part, or <see langword="null"/> when the step is a field.</summary>
    internal CelValue? KeyAt(int index) => _keys[index];

    /// <summary>
    /// The field of the elements of a list on the path: the part of the list is replaced by the variable of a loop over it.
    /// </summary>
    /// <param name="listIndex">The index of the part whose value is the list.</param>
    /// <param name="variable">The name of the variable of the loop.</param>
    /// <returns>The field of an element.</returns>
    internal FieldRef Element(int listIndex, string variable)
    {
        CelValue?[] keys = new CelValue?[Path.Names.Count - listIndex];
        for (int i = 1; i < keys.Length; i++)
        {
            keys[i] = _keys[listIndex + i];
        }

        return new FieldRef(Member, Path.ElementPath(listIndex, variable), Place.Element(listIndex), keys);
    }
}
