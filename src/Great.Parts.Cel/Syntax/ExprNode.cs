namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A node of the syntax tree, the <c>Expr</c> message of <c>syntax.proto</c>. Nodes are immutable and are created
/// only through <see cref="NodeFactory"/>.
/// </summary>
internal abstract class ExprNode
{
    private protected ExprNode(int id)
    {
        Id = id;
    }

    /// <summary>
    /// The id of the node, unique in its tree; ids are assigned from 1 in the order in which nodes are created.
    /// </summary>
    internal int Id { get; }
}
