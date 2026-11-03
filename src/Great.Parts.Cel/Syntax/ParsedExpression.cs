using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A parsed expression, the <c>ParsedExpr</c> message of <c>syntax.proto</c>: the root node, the positions of the
/// nodes and entries by id, the macro calls by the id of their expansion and the line table of the text. It does not
/// refer to the environment.
/// </summary>
internal sealed class ParsedExpression
{
    // The offset of the node or entry with the id i + 1 at the index i: ids run from 1 without gaps.
    private readonly ImmutableArray<int> _offsets;

    // The ids of the expansions of macro calls in ascending order, and the call of each at the same index.
    private readonly ImmutableArray<int> _macroIds;
    private readonly ImmutableArray<MacroCall> _macroCalls;

    internal ParsedExpression(
        ExprNode root,
        ImmutableArray<int> offsets,
        ImmutableArray<int> macroIds,
        ImmutableArray<MacroCall> macroCalls,
        LineTable lines)
    {
        Root = root;
        _offsets = offsets;
        _macroIds = macroIds;
        _macroCalls = macroCalls;
        Lines = lines;
    }

    /// <summary>
    /// The root node.
    /// </summary>
    internal ExprNode Root { get; }

    /// <summary>
    /// The line table of the text, which turns the offsets of nodes into lines and columns.
    /// </summary>
    internal LineTable Lines { get; }

    /// <summary>
    /// Returns the position of a node or an entry: its offset in UTF-16 code units from the start of the text.
    /// </summary>
    /// <param name="id">The id of a node or an entry of this tree.</param>
    /// <returns>The offset.</returns>
    internal int GetOffset(int id) => _offsets[id - 1];

    /// <summary>
    /// Returns the macro call that a node is the expansion of.
    /// </summary>
    /// <param name="id">The id of a node of this tree.</param>
    /// <returns>The call as written, or <see langword="null"/> when the node is not the expansion of a macro call.</returns>
    internal MacroCall? GetMacroCall(int id)
    {
        int index = _macroIds.BinarySearch(id);
        return index >= 0 ? _macroCalls[index] : null;
    }
}
