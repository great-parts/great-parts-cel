using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A chain of attributes as the planner of a partial plan sees it in the checked tree: a variable and the selections, optional
// selections, indexes, optional indexes and presence tests applied to it, as far as each applies to the result of the one before. The
// variable is a declared variable, also one resolved as the qualified name of a selection chain, or a field of the input of the
// environment; a variable of a comprehension, a slot of a block and any other expression start no chain. A conditional expression
// starts a chain too, when a qualifier is applied to it, (c ? a : b).f, as cel-go applies the qualifiers to both its branches.
internal sealed class AttributeShape
{
    private const string IndexFunction = "_[_]";
    private const string OptionalIndexFunction = "_[?_]";
    private const string OptionalSelectFunction = "_?._";
    private const string ConditionalFunction = "_?_:_";

    private AttributeShape(
        string variable,
        CelCallNode? conditional,
        ImmutableArray<CelExprNode> spine,
        ImmutableArray<AttributeQualifier> qualifiers)
    {
        Variable = variable;
        Conditional = conditional;
        Spine = spine;
        Qualifiers = qualifiers;
    }

    // The full name of the variable, as the bindings hold it; empty for a chain that starts at a conditional expression.
    internal string Variable { get; }

    // The conditional expression the chain starts at, or null when it starts at a variable.
    internal CelCallNode? Conditional { get; }

    // The nodes of the chain, the outermost first and the node of the variable, or of the conditional expression, last.
    internal ImmutableArray<CelExprNode> Spine { get; }

    // The qualifiers in the order of the access.
    internal ImmutableArray<AttributeQualifier> Qualifiers { get; }

    // The id of the syntax node at each length of the access: the variable first, then after each qualifier.
    internal ImmutableArray<int> Ids()
    {
        ImmutableArray<int>.Builder ids = ImmutableArray.CreateBuilder<int>(Spine.Length);
        for (int i = Spine.Length - 1; i >= 0; i--)
        {
            ids.Add(Spine[i].Id);
        }

        return ids.MoveToImmutable();
    }

    // The chain whose outermost node is the node, or null when the node is not the end of a chain. isSlot tells an identifier that
    // names a slot of a block being planned, which is not a variable. A conditional expression with no qualifier applied to it is a
    // chain only when it is a branch of the conditional expression at the base of another chain: bare is true then.
    internal static AttributeShape? TryCreate(
        CelExprNode node,
        CheckedExpression checkedExpression,
        Func<CelIdentNode, bool> isSlot,
        bool bare)
    {
        var spine = new List<CelExprNode>();
        var qualifiers = new List<AttributeQualifier>();
        CelExprNode current = node;
        while (true)
        {
            spine.Add(current);
            if (current is CelIdentNode identifier)
            {
                return !isSlot(identifier)
                    && checkedExpression.References.TryGetValue(identifier.Id, out Reference? read)
                    && read.Variable is { } variable
                        ? Create(variable.Name, null, spine, qualifiers)
                        : null;
            }

            CelExprNode? operand = null;
            if (current is CelSelectNode select)
            {
                if (checkedExpression.References.TryGetValue(select.Id, out Reference? reference))
                {
                    if (reference.Variable is { } qualified)
                    {
                        return Create(qualified.Name, null, spine, qualifiers);
                    }

                    if (reference.ObjectType is null)
                    {
                        return null;
                    }
                }

                qualifiers.Add(new AttributeQualifier(CelValue.FromString(select.Field), null));
                operand = select.Operand;
            }
            else if (current is CelCallNode call)
            {
                operand = TryQualify(call, qualifiers);
            }

            if (operand is null)
            {
                return TryStartAtConditional(current, bare, spine, qualifiers);
            }

            current = operand;
        }
    }

    private static AttributeShape Create(string variable, CelCallNode? conditional, List<CelExprNode> spine, List<AttributeQualifier> qualifiers)
    {
        qualifiers.Reverse();
        return new AttributeShape(variable, conditional, [.. spine], [.. qualifiers]);
    }

    // The chain that starts at the node when it is a conditional expression with a qualifier applied to it, or bare; else null.
    private static AttributeShape? TryStartAtConditional(CelExprNode node, bool bare, List<CelExprNode> spine, List<AttributeQualifier> qualifiers) =>
        node is CelCallNode { Target: null, Arguments.Length: 3 } call
        && string.Equals(call.Function, ConditionalFunction, StringComparison.Ordinal)
        && (bare || qualifiers.Count > 0)
            ? Create(string.Empty, call, spine, qualifiers)
            : null;

    // The operand of an index or an optional selection, with its qualifier added; null for any other call.
    private static CelExprNode? TryQualify(CelCallNode call, List<AttributeQualifier> qualifiers)
    {
        if (call.Target is not null || call.Arguments.Length != 2)
        {
            return null;
        }

        CelExprNode key = call.Arguments[1];
        if (string.Equals(call.Function, OptionalSelectFunction, StringComparison.Ordinal))
        {
            if (key is not CelConstantNode { Value: var name } || !name.TryGetString(out _))
            {
                return null;
            }

            qualifiers.Add(new AttributeQualifier(name, null));
            return call.Arguments[0];
        }

        if (!string.Equals(call.Function, IndexFunction, StringComparison.Ordinal)
            && !string.Equals(call.Function, OptionalIndexFunction, StringComparison.Ordinal))
        {
            return null;
        }

        qualifiers.Add(key is CelConstantNode constant ? new AttributeQualifier(constant.Value, null) : new AttributeQualifier(default, key));
        return call.Arguments[0];
    }
}
