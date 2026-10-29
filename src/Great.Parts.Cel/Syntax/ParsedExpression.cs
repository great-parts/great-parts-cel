using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A parsed expression, the <c>ParsedExpr</c> message of <c>syntax.proto</c>: the root node, the positions of the
/// nodes and entries by id and the line table of the text. It does not refer to the environment.
/// </summary>
internal sealed class ParsedExpression
{
    // The offset of the node or entry with the id i + 1 at the index i: ids run from 1 without gaps.
    private readonly ImmutableArray<int> _offsets;

    internal ParsedExpression(ExprNode root, ImmutableArray<int> offsets, LineTable lines)
    {
        Root = root;
        _offsets = offsets;
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
}
