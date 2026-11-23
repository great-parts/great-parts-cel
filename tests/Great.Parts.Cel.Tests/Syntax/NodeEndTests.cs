using System;
using System.Collections.Generic;
using Great.Parts.Cel.Syntax;
using Xunit;

namespace Great.Parts.Cel.Tests.Syntax;

// The end of each node that the parser records beside its place: the offset just after the node's last token, so that the fragment
// of an error is [place, end) of its node.
[Trait("Requirement", "F-11")]
public sealed class NodeEndTests
{
    private static readonly SyntaxOptions Options = SyntaxOptions.Standard with { OptionalSyntax = true };

    // One expression for each kind of node and for each way the parser creates one: operators, calls, selections, indexes,
    // literals, lists, maps, messages, conditionals, optional syntax and macros, which expand into nodes of their own.
    public static TheoryData<string> Expressions =>
    [
        "x",
        "1 + 2 * 3",
        "-x",
        "!ok",
        "a ? b : c",
        "a ? b : c ? d : e",
        "(1 + 2) * 3",
        "a || b && c",
        "a in l",
        "f()",
        "f(1, x, 'a')",
        "g(f(1))",
        "a.f(1)",
        "a.b.c",
        "a.b.c(d)",
        "a[0]",
        "a[b[c]]",
        "[1, 2, 3]",
        "[]",
        "{'a': 1, 'b': [2]}",
        "{}",
        "pkg.Message{field: 1, other: x + 1}",
        "-5",
        "1u + 2.5",
        "'x' + \"y\" + b'z'",
        "true && null == a",
        "l.all(i, i > 0)",
        "l.exists(i, i > 0) && l.filter(j, j < 3).size() == 1",
        "l.map(i, i + 1)",
        "has(a.b)",
        "a.?b",
        "[?a, 1]",
        "{?'k': v}",
        "a[?0]",
    ];

    [Theory]
    [MemberData(nameof(Expressions))]
    public static void EveryNode_StartsBeforeItsEnd_AndEndsWithinTheText_AndNotBeforeItsChildren(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        CelParseResult result = Parser.Parse(text, Options);
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        CelParsedExpr parsed = result.ParsedExpr!;

        int visited = 0;
        Walk(parsed.Root);

        Assert.True(visited > 0);

        void Walk(CelExprNode node)
        {
            visited++;
            int offset = parsed.GetOffset(node.Id);
            int end = parsed.GetEnd(node.Id);
            Assert.InRange(offset, 0, text.Length);
            Assert.InRange(end, offset, text.Length);
            foreach (CelExprNode child in Children(node))
            {
                Walk(child);
                Assert.True(end >= parsed.GetEnd(child.Id), $"the node at {offset} ends at {end}, before its child at {parsed.GetOffset(child.Id)}");
            }
        }
    }

    // The fragments that the examples of the error spans rely on, from the place to the end of the node.
    [Theory]
    [InlineData("x", 0, 1)]
    [InlineData("x + 'a'", 2, 7)]
    [InlineData("size(1, 2) == 0", 11, 15)]
    [InlineData("a.b.c", 3, 5)]
    [InlineData("[1, 2]", 0, 6)]
    [InlineData("(1 + 2) * 3", 8, 11)]
    [InlineData("-5", 0, 2)]
    [InlineData("a ? b : c", 2, 9)]
    [InlineData("a[0]", 1, 4)]
    public static void Root_HasThePlaceAndTheEndOfItsLastToken(string text, int offset, int end)
    {
        CelParsedExpr parsed = Parser.Parse(text, Options).ParsedExpr!;

        Assert.Equal((offset, end), (parsed.GetOffset(parsed.Root.Id), parsed.GetEnd(parsed.Root.Id)));
    }

    [Fact]
    public void Call_StartsAtItsParenthesis_AndEndsAtTheClosingOne()
    {
        CelParsedExpr parsed = Parser.Parse("size(1, 2) == 0", Options).ParsedExpr!;
        var comparison = (CelCallNode)parsed.Root;

        CelExprNode call = comparison.Arguments[0];

        Assert.Equal((4, 10), (parsed.GetOffset(call.Id), parsed.GetEnd(call.Id)));
    }

    // The nodes that a macro expands into take the place and the end of the call they come from, as the place is taken now.
    [Fact]
    public void ExpandedMacro_HasTheFragmentOfTheCall_AndItsArgumentsKeepTheirOwn()
    {
        CelParsedExpr parsed = Parser.Parse("l.all(i, i > 0)", Options).ParsedExpr!;
        var expansion = (CelComprehensionNode)parsed.Root;

        Assert.Equal((5, 15), (parsed.GetOffset(expansion.Id), parsed.GetEnd(expansion.Id)));
        Assert.Equal((5, 15), (parsed.GetOffset(expansion.AccuInit.Id), parsed.GetEnd(expansion.AccuInit.Id)));
        Assert.Equal((5, 15), (parsed.GetOffset(expansion.LoopCondition.Id), parsed.GetEnd(expansion.LoopCondition.Id)));

        // The comparison that the user wrote is a node inside the expansion, at its own place.
        List<CelExprNode> inside = [];
        Collect(expansion, inside);
        CelExprNode written = Assert.Single(inside, node => parsed.GetOffset(node.Id) == 11);
        Assert.Equal(14, parsed.GetEnd(written.Id));

        void Collect(CelExprNode node, List<CelExprNode> into)
        {
            into.Add(node);
            foreach (CelExprNode child in Children(node))
            {
                Collect(child, into);
            }
        }
    }

    // A parenthesis that groups an operand is no node of its own: the operand ends before the closing parenthesis, the operator
    // that takes it as its operand after it.
    [Fact]
    public void GroupedOperand_EndsBeforeItsParenthesis()
    {
        CelParsedExpr parsed = Parser.Parse("(1 + 2) * 3", Options).ParsedExpr!;
        var product = (CelCallNode)parsed.Root;

        CelExprNode sum = product.Arguments[0];

        Assert.Equal((3, 6), (parsed.GetOffset(sum.Id), parsed.GetEnd(sum.Id)));
    }

    private static IEnumerable<CelExprNode> Children(CelExprNode node)
    {
        switch (node)
        {
            case CelCallNode call:
                if (call.Target is not null)
                {
                    yield return call.Target;
                }

                foreach (CelExprNode argument in call.Arguments)
                {
                    yield return argument;
                }

                break;
            case CelSelectNode select:
                yield return select.Operand;
                break;
            case CelListNode list:
                foreach (CelExprNode element in list.Elements)
                {
                    yield return element;
                }

                break;
            case CelMapNode map:
                foreach (CelMapEntryNode entry in map.Entries)
                {
                    yield return entry.Key;
                    yield return entry.Value;
                }

                break;
            case CelStructNode message:
                foreach (CelStructFieldNode field in message.Fields)
                {
                    yield return field.Value;
                }

                break;
            case CelComprehensionNode comprehension:
                yield return comprehension.IterRange;
                yield return comprehension.AccuInit;
                yield return comprehension.LoopCondition;
                yield return comprehension.LoopStep;
                yield return comprehension.Result;
                break;
        }
    }
}
