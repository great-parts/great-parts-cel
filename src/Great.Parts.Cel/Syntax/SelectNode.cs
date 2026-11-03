namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A field selection <c>operand.field</c>, the <c>Expr.Select</c> message of <c>syntax.proto</c>.
/// </summary>
internal sealed class SelectNode : ExprNode
{
    internal SelectNode(int id, ExprNode operand, string field, bool isPresenceTest)
        : base(id)
    {
        Operand = operand;
        Field = field;
        IsPresenceTest = isPresenceTest;
    }

    /// <summary>
    /// The expression whose field is selected.
    /// </summary>
    internal ExprNode Operand { get; }

    /// <summary>
    /// The name of the field; for an escaped field, the text between the backticks.
    /// </summary>
    internal string Field { get; }

    /// <summary>
    /// <see langword="true"/> for the presence test of the macro <c>has</c>, which asks whether the field is set
    /// instead of reading it.
    /// </summary>
    internal bool IsPresenceTest { get; }
}
