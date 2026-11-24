using System.Collections.Generic;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Filtering;

/// <summary>
/// The parts of the dotted name of a field resolved against the environment: which of them name a variable and the CEL type
/// after each part.
/// </summary>
internal sealed class ResolvedPath
{
    // The type of the variable and then of each field after it, in the order of the parts; shorter than the parts when the
    // environment does not declare the variable. A type is null from the part that no type of the environment describes.
    private readonly CelType?[] _types;

    /// <summary>
    /// Creates a resolved path.
    /// </summary>
    /// <param name="names">The names of the parts.</param>
    /// <param name="variableLength">How many parts, from the first, are the name of a variable; 0 when none is.</param>
    /// <param name="types">The type of the variable and of each field after it.</param>
    /// <param name="isIteration">Whether the variable is the variable of an <c>exists</c> loop, which is never missing.</param>
    internal ResolvedPath(IReadOnlyList<string> names, int variableLength, CelType?[] types, bool isIteration = false)
    {
        Names = names;
        VariableLength = variableLength;
        _types = types;
        IsIteration = isIteration;
    }

    /// <summary>The names of the parts.</summary>
    internal IReadOnlyList<string> Names { get; }

    /// <summary>How many parts, from the first, are the name of a variable; 0 when no variable has the name of the start.</summary>
    internal int VariableLength { get; }

    /// <summary>Whether the variable is the variable of an <c>exists</c> loop, which is never missing and needs no test.</summary>
    internal bool IsIteration { get; }

    /// <summary>The type of the whole path; <see langword="null"/> when the environment does not describe it.</summary>
    internal CelType? FinalType => _types.Length == 0 ? null : _types[^1];

    /// <summary>
    /// The type after a part.
    /// </summary>
    /// <param name="index">The index of the part, from 0.</param>
    /// <returns>The type of the value that the path has up to and including the part; <see langword="null"/> when it is unknown.</returns>
    internal CelType? TypeAfter(int index)
    {
        int at = index - (VariableLength - 1);
        return VariableLength > 0 && at >= 0 && at < _types.Length ? _types[at] : null;
    }

    /// <summary>
    /// Finds the first part, before the last, whose value is a list: a path goes through it only by an <c>exists</c> loop.
    /// </summary>
    /// <returns>The index of the part; -1 when no part before the last is a list.</returns>
    internal int ListStep()
    {
        for (int i = VariableLength - 1; i < Names.Count - 1; i++)
        {
            if (i >= 0 && TypeAfter(i) is { Kind: CelTypeKind.List })
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// The path that starts at an element of a list on this path: the variable of a loop over the list, then the parts after it.
    /// </summary>
    /// <param name="listIndex">The index of the part whose value is the list.</param>
    /// <param name="variable">The name of the variable of the loop.</param>
    /// <returns>The path of the element.</returns>
    internal ResolvedPath ElementPath(int listIndex, string variable)
    {
        int at = listIndex - (VariableLength - 1);
        List<string> names = [variable];
        List<CelType?> types = [_types[at]!.Parameters[0]];
        for (int i = listIndex + 1; i < Names.Count; i++)
        {
            names.Add(Names[i]);
            types.Add(at + (i - listIndex) < _types.Length ? _types[at + (i - listIndex)] : null);
        }

        return new ResolvedPath(names, 1, [.. types], isIteration: true);
    }
}
