using System.Diagnostics.CodeAnalysis;

namespace Great.Parts.Cel.Syntax;

// The form in which a tree is written as an Expr message. The parsed form is the tree as it is. The checked form is the one that type
// checking gives in cel-go, where a name that resolves to a declaration is an identifier of the full name, a call of a qualified
// function name is a global call of the full name, and a message literal has the full name of its type; the checked form comes from
// the layer of type checking, which knows the references.
internal abstract class ExprForm
{
    internal static ExprForm Parsed { get; } = new ParsedForm();

    /// <summary>
    /// Tells whether the node is written as an identifier, whatever its kind, and gives its name; the operand of a selection is not written.
    /// </summary>
    /// <param name="node">The node of the tree.</param>
    /// <param name="name">The name of the identifier.</param>
    /// <returns><see langword="true"/> when the node is written as an identifier.</returns>
    internal abstract bool TryIdentifier(CelExprNode node, [NotNullWhen(true)] out string? name);

    /// <summary>
    /// Gives the function a call is written with, and whether its target is dropped because the name of the function has it.
    /// </summary>
    /// <param name="call">The call of the tree.</param>
    /// <param name="function">The function to write.</param>
    /// <param name="dropTarget"><see langword="true"/> to write a global call.</param>
    /// <returns><see langword="true"/> when the form changes the call.</returns>
    internal abstract bool TryCall(CelCallNode call, [NotNullWhen(true)] out string? function, out bool dropTarget);

    /// <summary>
    /// Gives the type name a message literal is written with.
    /// </summary>
    /// <param name="message">The message literal of the tree.</param>
    /// <returns>The type name.</returns>
    internal abstract string MessageName(CelStructNode message);

    private sealed class ParsedForm : ExprForm
    {
        internal override bool TryIdentifier(CelExprNode node, [NotNullWhen(true)] out string? name)
        {
            name = null;
            return false;
        }

        internal override bool TryCall(CelCallNode call, [NotNullWhen(true)] out string? function, out bool dropTarget)
        {
            function = null;
            dropTarget = false;
            return false;
        }

        internal override string MessageName(CelStructNode message) => message.TypeName;
    }
}
