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
// environment; a variable of a comprehension, a slot of a block and any other expression start no chain.
internal sealed class AttributeShape
{
    private const string IndexFunction = "_[_]";
    private const string OptionalIndexFunction = "_[?_]";
    private const string OptionalSelectFunction = "_?._";

    private AttributeShape(string variable, ImmutableArray<CelExprNode> spine, ImmutableArray<AttributeQualifier> qualifiers)
    {
        Variable = variable;
        Spine = spine;
        Qualifiers = qualifiers;
    }

    // The full name of the variable, as the bindings hold it.
    internal string Variable { get; }

    // The nodes of the chain, the outermost first and the node of the variable last.
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
    // names a slot of a block being planned, which is not a variable.
    internal static AttributeShape? TryCreate(CelExprNode node, CheckedExpression checkedExpression, Func<CelIdentNode, bool> isSlot)
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
                        ? Create(variable.Name, spine, qualifiers)
                        : null;
            }

            CelExprNode? operand = null;
            if (current is CelSelectNode select)
            {
                if (checkedExpression.References.TryGetValue(select.Id, out Reference? reference))
                {
                    if (reference.Variable is { } qualified)
                    {
                        return Create(qualified.Name, spine, qualifiers);
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
                return null;
            }

            current = operand;
        }
    }

    private static AttributeShape Create(string variable, List<CelExprNode> spine, List<AttributeQualifier> qualifiers)
    {
        qualifiers.Reverse();
        return new AttributeShape(variable, [.. spine], [.. qualifiers]);
    }

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
