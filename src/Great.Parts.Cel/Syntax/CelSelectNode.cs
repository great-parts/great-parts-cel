
namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A field selection <c>operand.field</c>, or the presence test that the macro <c>has(operand.field)</c> expands to: the
/// <c>Expr.Select</c> message of <c>syntax.proto</c>.
/// </summary>
/// <remarks>
/// <para>
/// The optional selection <c>operand.?field</c> is not a selection but the call <c>_?._</c> with the field name as a string
/// constant, as in cel-go.
/// </para>
/// <para>The node is immutable and can be read from any number of threads at once.</para>
/// </remarks>
public sealed class CelSelectNode : CelExprNode
{
    internal CelSelectNode(int id, CelExprNode operand, string field, bool isPresenceTest)
        : base(id, CelExprKind.Select)
    {
        Operand = operand;
        Field = field;
        IsPresenceTest = isPresenceTest;
    }

    /// <summary>
    /// The expression whose field is selected.
    /// </summary>
    public CelExprNode Operand { get; }

    /// <summary>
    /// The name of the field; for a field in backticks, the text between them.
    /// </summary>
    public string Field { get; }

    /// <summary>
    /// <see langword="true"/> for the presence test of <c>has</c>, which tells whether the field is set instead of reading
    /// it: the <c>test_only</c> field of <c>syntax.proto</c>.
    /// </summary>
    public bool IsPresenceTest { get; }

    private protected override CelExprNode[] CreateChildren() => [Operand];
}
