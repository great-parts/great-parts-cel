using System;
using System.Collections.Generic;
using System.Globalization;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;

namespace Great.Parts.Cel.Policy;

// Tells which slot a variable of a policy has at the place where an expression is copied.
internal interface ISlotLookup
{
    // The slot of the variable that the name stands for there, the last declared of that name that is visible; false for none.
    bool TryGetSlot(string name, out int slot);
}

// Copies the tree of an expression into a tree of the policy, in which each reference to a variable of the policy, variables.name, is
// the slot of that variable: the identifier @index<N>. A reference that a variable of a comprehension named "variables" binds, and
// the test has(variables.name), stay as they are, as cel-go leaves them: type checking resolves neither to a variable of the policy.
internal sealed class VariableRewriter
{
    // The name under which the expressions of a policy see its variables.
    internal const string Prefix = "variables";

    // The prefix of the names of the slots of cel.@block.
    internal const string SlotPrefix = "@index";

    private readonly CelExprFactory _factory;
    private readonly CelParsedExpr _source;
    private readonly SegmentMap _map;
    private readonly ISlotLookup _lookup;
    private readonly HashSet<int>? _bound;

    internal VariableRewriter(CelExprFactory factory, CompiledExpression expression, ISlotLookup lookup)
    {
        _factory = factory;
        _source = expression.Parsed!;
        _map = expression.Map;
        _lookup = lookup;
        _bound = BoundReferences(_source.Root);
    }

    // The copy of the whole tree of the expression, with its positions in the document.
    internal CelExprNode Copy() => _factory.Copy(_source, _source.Root, Replace, _map.ToDocument);

    private CelExprNode? Replace(CelExprNode node)
    {
        if (node is not CelSelectNode { IsPresenceTest: false, Operand: CelIdentNode { Name: Prefix } } select)
        {
            return null;
        }

        if ((_bound is not null && _bound.Contains(select.Id)) || !_lookup.TryGetSlot(select.Field, out int slot))
        {
            return null;
        }

        // The slot stands where the reference stood.
        OffsetsOf(select, out int start, out int end);
        _factory.Position = start < 0 ? null : new Range(start, end);
        CelIdentNode identifier = _factory.Ident(SlotName(slot));
        _factory.Position = null;
        return identifier;
    }

    internal static string SlotName(int slot) => SlotPrefix + slot.ToString(CultureInfo.InvariantCulture);

    // The start and the end of a node in the document; -1 for a node without a position.
    private void OffsetsOf(CelExprNode node, out int start, out int end)
    {
        CelSourceSpan span = _source.GetSpan(node.Id);
        if (span.Start.Line == 0)
        {
            start = -1;
            end = -1;
            return;
        }

        start = _map.ToDocument(span.Start.Offset);
        end = _map.ToDocument(span.Start.Offset + span.Length);
        if (end < start)
        {
            end = start;
        }
    }

    // The ids of the selections variables.name that are inside a comprehension which binds the name "variables": the iteration
    // variables are visible in its condition and its step, the accumulator also in its result. Null when there are none.
    private static HashSet<int>? BoundReferences(CelExprNode root)
    {
        HashSet<int>? bound = null;
        Stack<(CelExprNode Node, bool Bound)> pending = new();
        pending.Push((root, false));
        while (pending.Count > 0)
        {
            (CelExprNode node, bool isBound) = pending.Pop();
            if (node is CelComprehensionNode comprehension)
            {
                PushComprehension(pending, comprehension, isBound);
                continue;
            }

            if (isBound && node is CelSelectNode { Operand: CelIdentNode { Name: Prefix } })
            {
                (bound ??= []).Add(node.Id);
            }

            foreach (CelExprNode child in node.Children)
            {
                pending.Push((child, isBound));
            }
        }

        return bound;
    }

    private static void PushComprehension(Stack<(CelExprNode Node, bool Bound)> pending, CelComprehensionNode comprehension, bool isBound)
    {
        bool bindsIteration = comprehension.IterVar == Prefix || comprehension.IterVar2 == Prefix;
        bool bindsAccumulator = comprehension.AccuVar == Prefix;
        pending.Push((comprehension.IterRange, isBound));
        pending.Push((comprehension.AccuInit, isBound));
        pending.Push((comprehension.LoopCondition, isBound || bindsIteration || bindsAccumulator));
        pending.Push((comprehension.LoopStep, isBound || bindsIteration || bindsAccumulator));
        pending.Push((comprehension.Result, isBound || bindsAccumulator));
    }
}
