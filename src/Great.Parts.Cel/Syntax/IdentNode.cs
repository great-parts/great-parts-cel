namespace Great.Parts.Cel.Syntax;

/// <summary>
/// An identifier, the <c>Expr.Ident</c> message of <c>syntax.proto</c>.
/// </summary>
internal sealed class IdentNode : ExprNode
{
    internal IdentNode(int id, string name)
        : base(id)
    {
        Name = name;
    }

    /// <summary>
    /// The name as written in the text, a leading dot included, as in <c>.x</c>.
    /// </summary>
    internal string Name { get; }
}
