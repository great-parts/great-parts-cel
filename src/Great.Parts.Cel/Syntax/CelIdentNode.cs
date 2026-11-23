
namespace Great.Parts.Cel.Syntax;

/// <summary>
/// An identifier: the <c>Expr.Ident</c> message of <c>syntax.proto</c>, such as <c>order</c>, <c>.order</c> or the
/// accumulator <c>@result</c> of a macro expansion.
/// </summary>
/// <remarks>
/// <para>
/// What the name refers to — a variable of the environment, a type, an enumeration constant or a variable of an enclosing
/// comprehension — is decided by type checking and is in <c>CelCheckedExpr.ReferenceMap</c>; a qualified name such as
/// <c>acme.sales.limit</c> is parsed as selections over the identifier <c>acme</c>.
/// </para>
/// <para>The node is immutable and can be read from any number of threads at once.</para>
/// </remarks>
public sealed class CelIdentNode : CelExprNode
{
    internal CelIdentNode(int id, string name)
        : base(id, CelExprKind.Ident)
    {
        Name = name;
    }

    /// <summary>
    /// The name as written, a leading dot included, as in <c>.x</c>; for a name in backticks, the text between them. Names
    /// that start with <c>@</c> are internal names of macro expansions and optimizations, which no text can write.
    /// </summary>
    public string Name { get; }
}
