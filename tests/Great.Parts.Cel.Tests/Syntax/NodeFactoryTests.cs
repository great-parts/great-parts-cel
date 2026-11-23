using System.Collections.Immutable;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Syntax;

public sealed class NodeFactoryTests
{
    // One node of every class and an entry of each kind, each at its own offset: ids run from 1 in the order of
    // creation without gaps, and the parsed expression gives each id the offset it was created with.
    [Fact]
    public static void Nodes_GetIdsFromOneInTheOrderOfCreationAndTheirOffsets()
    {
        var nodes = new NodeFactory();

        CelConstantNode constant = nodes.Constant(CelValue.FromInt64(1), 10);
        CelIdentNode ident = nodes.Ident("x", 20);
        CelSelectNode select = nodes.Select(ident, "f", isPresenceTest: true, 30);
        CelCallNode call = nodes.Call("g", select, [constant], 40);
        CelListNode list = nodes.List([call], [0], 50);
        CelMapEntryNode mapEntry = nodes.Entry(constant, list, isOptional: true, 60);
        CelMapNode map = nodes.Map([mapEntry], 70);
        CelStructFieldNode fieldEntry = nodes.Field("f", map, isOptional: false, 80);
        CelStructNode message = nodes.Struct("a.B", [fieldEntry], 90);
        CelComprehensionNode comprehension = nodes.Comprehension("v", message, "@result", constant, ident, select, call, 100);
        CelParsedExpr parsed = nodes.Build(comprehension, new LineTable("x"));

        Assert.Equal(
            Enumerable.Range(1, 10),
            new[] { constant.Id, ident.Id, select.Id, call.Id, list.Id, mapEntry.Id, map.Id, fieldEntry.Id, message.Id, comprehension.Id });
        Assert.Equal(
            [10, 20, 30, 40, 50, 60, 70, 80, 90, 100],
            Enumerable.Range(1, 10).Select(parsed.GetOffset));
        Assert.Same(comprehension, parsed.Root);
    }

    [Fact]
    public static void Nodes_HoldTheirParts()
    {
        var nodes = new NodeFactory();
        CelConstantNode one = nodes.Constant(CelValue.FromInt64(1), 0);
        CelIdentNode x = nodes.Ident(".x", 1);

        CelSelectNode select = nodes.Select(x, "a-b", isPresenceTest: true, 2);
        CelCallNode call = nodes.Call("f", x, [one, select], 3);
        CelListNode list = nodes.List([one, x, select], [0, 2], 4);
        CelMapEntryNode mapEntry = nodes.Entry(one, x, isOptional: true, 5);
        CelStructFieldNode fieldEntry = nodes.Field("f", one, isOptional: false, 6);
        CelStructNode message = nodes.Struct(".a.B", [fieldEntry], 7);
        CelComprehensionNode comprehension = nodes.Comprehension("v", list, "@result", one, x, select, call, 8);

        Assert.Equal((".x", "a-b", true), (x.Name, select.Field, select.IsPresenceTest));
        Assert.Same(x, select.Operand);
        Assert.Equal(("f", (CelExprNode?)x), (call.Function, call.Target));
        Assert.Equal(new CelExprNode[] { one, select }, call.Arguments.AsEnumerable());
        Assert.Equal([0, 2], list.OptionalIndices.AsEnumerable());
        Assert.Equal(((CelExprNode)one, (CelExprNode)x, true), (mapEntry.Key, mapEntry.Value, mapEntry.IsOptional));
        Assert.Equal(("f", (CelExprNode)one, false), (fieldEntry.Name, fieldEntry.Value, fieldEntry.IsOptional));
        Assert.Equal(".a.B", message.TypeName);
        Assert.Same(fieldEntry, Assert.Single(message.Fields));
        Assert.Equal(
            ("v", (CelExprNode)list, "@result", (CelExprNode)one, (CelExprNode)x, (CelExprNode)select, (CelExprNode)call),
            (comprehension.IterVar, comprehension.IterRange, comprehension.AccuVar, comprehension.AccuInit, comprehension.LoopCondition, comprehension.LoopStep, comprehension.Result));
    }

    // Each factory has its own sequence of ids.
    [Fact]
    public static void Factories_CountIdsIndependently()
    {
        var first = new NodeFactory();
        var second = new NodeFactory();

        _ = first.Ident("a", 0);
        ImmutableArray<CelExprNode> none = [];
        CelCallNode call = second.Call("f", null, none, 5);

        Assert.Equal(1, call.Id);
        Assert.Equal(5, second.Build(call, new LineTable("f()")).GetOffset(1));
    }
}
