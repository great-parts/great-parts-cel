using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// The checks of a tree that comes from another source than the parser of the environment: the limit of the depth of the
/// tree and the syntax that the environment does not accept, which the parser reports for a text. The errors have the stage
/// <see cref="CelErrorStage.Parse"/> and the places of the nodes in the tree.
/// </summary>
internal static class TreeInput
{
    // The depth of a parsed tree is limited as in the parser: a node is at most this many levels below the root.
    private const int MaxDepth = 250;

    // The number of errors after which the check stops, as the parser stops.
    private const int MaxErrors = 100;

    private const string DepthExceeded = "syntax tree depth exceeds the limit of 250";

    /// <summary>
    /// Checks a tree in one pass over its nodes, without recursion.
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="syntax">The syntax of the environment that compiles the tree.</param>
    /// <returns>The errors ordered by offset, in the order of the walk at one offset; empty when the tree passes.</returns>
    internal static ImmutableArray<CelError> Check(CelParsedExpr tree, SyntaxOptions syntax)
    {
        List<(int Offset, int Sequence, CelError Error)> errors = [];
        bool deepReported = false;
        Stack<(CelExprNode Node, int Depth)> pending = new();
        pending.Push((tree.Root, 0));
        while (pending.Count > 0 && errors.Count < MaxErrors)
        {
            (CelExprNode node, int depth) = pending.Pop();
            if (depth > MaxDepth && !deepReported)
            {
                deepReported = true;
                Add(errors, tree, node.Id, CelErrorCode.NestingTooDeep, DepthExceeded);
            }

            CheckSyntax(errors, tree, syntax, node);
            ImmutableArray<CelExprNode> children = node.Children;
            for (int i = children.Length - 1; i >= 0; i--)
            {
                pending.Push((children[i], depth + 1));
            }
        }

        if (errors.Count == 0)
        {
            return [];
        }

        // Stable by offset: the order of the walk breaks the ties.
        errors.Sort(static (left, right) => left.Offset != right.Offset ? left.Offset.CompareTo(right.Offset) : left.Sequence.CompareTo(right.Sequence));
        ImmutableArray<CelError>.Builder ordered = ImmutableArray.CreateBuilder<CelError>(errors.Count);
        foreach ((int _, int _, CelError error) in errors)
        {
            ordered.Add(error);
        }

        return ordered.MoveToImmutable();
    }

    // The constants and the constructs that need an option of the environment.
    private static void CheckSyntax(List<(int Offset, int Sequence, CelError Error)> errors, CelParsedExpr tree, SyntaxOptions syntax, CelExprNode node)
    {
        switch (node)
        {
            case CelConstantNode { Value.Kind: CelValueKind.Decimal } when !syntax.DecimalLiterals:
                Add(errors, tree, node.Id, CelErrorCode.SyntaxNotEnabled, Parser.DecimalLiteralNotEnabled);
                break;
            case CelCallNode { Function: "_?._" or "_[?_]" } call when !syntax.OptionalSyntax:
                Add(errors, tree, call.Id, CelErrorCode.SyntaxNotEnabled, (call.Function == "_?._" ? "'.?'" : "'[?'") + Parser.OptionalSyntaxHint);
                break;
            case CelListNode { OptionalIndices.IsEmpty: false } list when !syntax.OptionalSyntax:
                foreach (int index in list.OptionalIndices)
                {
                    Add(errors, tree, list.Elements[index].Id, CelErrorCode.SyntaxNotEnabled, "'?'" + Parser.OptionalSyntaxHint);
                }

                break;
            case CelMapNode map when !syntax.OptionalSyntax:
                foreach (CelMapEntryNode entry in map.Entries)
                {
                    if (entry.IsOptional)
                    {
                        Add(errors, tree, entry.Id, CelErrorCode.SyntaxNotEnabled, "'?'" + Parser.OptionalSyntaxHint);
                    }
                }

                break;
            case CelStructNode message when !syntax.OptionalSyntax:
                foreach (CelStructFieldNode field in message.Fields)
                {
                    if (field.IsOptional)
                    {
                        Add(errors, tree, field.Id, CelErrorCode.SyntaxNotEnabled, "'?'" + Parser.OptionalSyntaxHint);
                    }
                }

                break;
            default:
                break;
        }
    }

    // An error at the node or the entry with an id: its place from the position table of the tree; a node without a position
    // has an error without a place. The error beyond the limit is dropped.
    private static void Add(List<(int Offset, int Sequence, CelError Error)> errors, CelParsedExpr tree, int id, CelErrorCode code, string message)
    {
        if (errors.Count >= MaxErrors)
        {
            return;
        }

        int offset = tree.GetOffset(id);
        CelSourceLocation location = offset < 0 ? default : tree.Lines.GetLocation(offset);
        int length = offset < 0 ? 0 : tree.GetEnd(id) - offset;
        errors.Add((offset, errors.Count, new CelError(code, CelErrorStage.Parse, message, location, sourceLocation: null, exception: null, length)));
    }
}
