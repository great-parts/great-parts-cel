using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Syntax;

// The public tree of a parsed expression (CelParsedExpr and the node classes): the shape that parsing gives for a text, the kinds,
// the children, the walk, the search by id, the positions and the macro calls, and Parse as the parse step of Compile. The shape is
// that of the parser of cel-go; ids and positions are asserted only where the table says.
[Trait("Requirement", "F-19")]
[Trait("Requirement", "F-21")]
public sealed class PublicTreeTests
{
    private static readonly CelEnvironment Env = TreeEnvironments.Env;
    private static readonly CelEnvironment EnvNoOptional = TreeEnvironments.EnvNoOptional;

    [Fact]
    public static void BinaryCall_HasItsArgumentsAndTheSpansOfItsNodes()
    {
        CelParsedExpr tree = Parse("a + b");

        CelCallNode root = Assert.IsType<CelCallNode>(tree.Root);
        Assert.Equal(CelExprKind.Call, root.Kind);
        Assert.Equal("_+_", root.Function);
        Assert.Null(root.Target);
        CelIdentNode a = Assert.IsType<CelIdentNode>(root.Arguments[0]);
        CelIdentNode b = Assert.IsType<CelIdentNode>(root.Arguments[1]);
        Assert.Equal(("a", "b"), (a.Name, b.Name));
        Assert.Equal<CelExprNode>([a, b], root.Children);
        Assert.Equal((2, 3), (tree.GetSpan(root.Id).Start.Offset, tree.GetSpan(root.Id).Length));
        Assert.Equal((0, 1), (tree.GetSpan(a.Id).Start.Offset, tree.GetSpan(a.Id).Length));
        int[] ids = [root.Id, a.Id, b.Id];
        Assert.All(ids, id => Assert.True(id > 0));
        Assert.Equal(3, ids.Distinct().Count());
    }

    [Fact]
    public static void NegativeNumber_IsOneConstant()
    {
        CelConstantNode constant = Assert.IsType<CelConstantNode>(Parse("-5").Root);

        Assert.Equal(CelExprKind.Constant, constant.Kind);
        Assert.True(constant.Value.TryGetInt64(out long number));
        Assert.Equal(-5L, number);
    }

    [Theory]
    [InlineData("2.5", CelValueKind.Double)]
    [InlineData("1u", CelValueKind.UInt)]
    [InlineData("\"s\"", CelValueKind.String)]
    [InlineData("b\"\\x00\"", CelValueKind.Bytes)]
    [InlineData("true", CelValueKind.Bool)]
    [InlineData("null", CelValueKind.Null)]
    public static void Literal_IsAConstantOfItsKind(string text, CelValueKind kind)
    {
        ArgumentNullException.ThrowIfNull(text);

        CelConstantNode constant = Assert.IsType<CelConstantNode>(Parse(text).Root);

        Assert.Equal(kind, constant.Value.Kind);
    }

    [Fact]
    public static void NameWithALeadingDot_KeepsTheDot()
    {
        CelIdentNode ident = Assert.IsType<CelIdentNode>(Parse(".x").Root);

        Assert.Equal((CelExprKind.Ident, ".x"), (ident.Kind, ident.Name));
    }

    [Fact]
    public static void FieldInBackticks_IsASelectionOfTheTextBetweenThem()
    {
        CelSelectNode select = Assert.IsType<CelSelectNode>(Parse("m.`a-b`").Root);

        Assert.Equal(CelExprKind.Select, select.Kind);
        Assert.Equal(("a-b", false), (select.Field, select.IsPresenceTest));
    }

    [Fact]
    public static void Has_IsAPresenceTestAndItsCallIsRecorded()
    {
        CelParsedExpr tree = Parse("has(m.f)");

        CelSelectNode select = Assert.IsType<CelSelectNode>(tree.Root);
        Assert.True(select.IsPresenceTest);
        Assert.Equal("m", Assert.IsType<CelIdentNode>(select.Operand).Name);
        Assert.Equal("f", select.Field);
        CelMacroCall call = Assert.IsType<CelMacroCall>(tree.GetMacroCall(select.Id));
        Assert.Equal("has", call.Function);
        Assert.Null(call.Target);
        Assert.Single(call.Arguments);
    }

    [Fact]
    public static void All_IsAComprehensionAndItsCallIsRecorded()
    {
        CelParsedExpr tree = Parse("items.all(v, v > 0)");

        CelComprehensionNode loop = Assert.IsType<CelComprehensionNode>(tree.Root);
        Assert.Equal(CelExprKind.Comprehension, loop.Kind);
        Assert.Equal("v", loop.IterVar);
        Assert.Null(loop.IterVar2);
        Assert.Equal("@result", loop.AccuVar);
        Assert.Equal("items", Assert.IsType<CelIdentNode>(loop.IterRange).Name);
        Assert.True(Assert.IsType<CelConstantNode>(loop.AccuInit).Value.TryGetBoolean(out bool init) && init);
        CelCallNode condition = Assert.IsType<CelCallNode>(loop.LoopCondition);
        Assert.Equal("@not_strictly_false", condition.Function);
        Assert.Equal("@result", Assert.IsType<CelIdentNode>(Assert.Single(condition.Arguments)).Name);
        CelCallNode step = Assert.IsType<CelCallNode>(loop.LoopStep);
        Assert.Equal("_&&_", step.Function);
        Assert.Equal("@result", Assert.IsType<CelIdentNode>(step.Arguments[0]).Name);
        Assert.Equal("_>_", Assert.IsType<CelCallNode>(step.Arguments[1]).Function);
        Assert.Equal("@result", Assert.IsType<CelIdentNode>(loop.Result).Name);

        CelMacroCall call = Assert.IsType<CelMacroCall>(tree.GetMacroCall(loop.Id));
        Assert.Equal("all", call.Function);
        Assert.Same(loop.IterRange, call.Target);
        Assert.Equal(2, call.Arguments.Length);
        Assert.Equal("v", Assert.IsType<CelIdentNode>(call.Arguments[0]).Name);
        Assert.Same(step.Arguments[1], call.Arguments[1]);
    }

    [Fact]
    public static void CelBind_IsAComprehensionWithAQualifiedMacroNameAndNoTarget()
    {
        CelParsedExpr tree = Parse("cel.bind(t, 1, t + 1)");

        Assert.IsType<CelComprehensionNode>(tree.Root);
        CelMacroCall call = Assert.IsType<CelMacroCall>(tree.GetMacroCall(tree.Root.Id));
        Assert.Equal("cel.bind", call.Function);
        Assert.Null(call.Target);
    }

    [Fact]
    public static void ListWithAnOptionalElement_HasItsIndex()
    {
        CelListNode list = Assert.IsType<CelListNode>(Parse("[1, ?d]").Root);

        Assert.Equal(CelExprKind.List, list.Kind);
        Assert.Equal<int>([1], list.OptionalIndices);
        Assert.Equal(2, list.Elements.Length);
    }

    [Fact]
    public static void Map_HasEntriesWithTheirOwnIdsAndTheColonAsPosition()
    {
        const string Text = "{\"a\": 1, ?\"b\": d}";
        CelParsedExpr tree = Parse(Text);

        CelMapNode map = Assert.IsType<CelMapNode>(tree.Root);

        Assert.Equal(CelExprKind.Map, map.Kind);
        Assert.Equal(2, map.Entries.Length);
        Assert.False(map.Entries[0].IsOptional);
        Assert.True(map.Entries[1].IsOptional);
        int[] nodeIds = [.. map.DescendantsAndSelf().Select(node => node.Id)];
        Assert.All(map.Entries, entry => Assert.DoesNotContain(entry.Id, nodeIds));
        Assert.Equal(':', Text[tree.GetSpan(map.Entries[0].Id).Start.Offset]);
        Assert.Equal<CelExprNode>([map.Entries[0].Key, map.Entries[0].Value, map.Entries[1].Key, map.Entries[1].Value], map.Children);
    }

    [Fact]
    public static void MessageLiteral_HasItsTypeNameAndFields()
    {
        CelStructNode message = Assert.IsType<CelStructNode>(Parse("acme.T{f: 1}").Root);

        Assert.Equal(CelExprKind.Struct, message.Kind);
        Assert.Equal("acme.T", message.TypeName);
        CelStructFieldNode field = Assert.Single(message.Fields);
        Assert.Equal(("f", false), (field.Name, field.IsOptional));
        Assert.Equal<CelExprNode>([field.Value], message.Children);
    }

    [Fact]
    public static void OptionalSelection_IsACallWithTheFieldAsAString()
    {
        CelCallNode call = Assert.IsType<CelCallNode>(Parse("m.?f").Root);

        Assert.Equal("_?._", call.Function);
        Assert.True(Assert.IsType<CelConstantNode>(call.Arguments[1]).Value.TryGetString(out string? field));
        Assert.Equal("f", field);
    }

    [Fact]
    public static void CallInTheMemberForm_HasATarget()
    {
        CelCallNode call = Assert.IsType<CelCallNode>(Parse("s.startsWith(\"a\")").Root);

        Assert.Equal("s", Assert.IsType<CelIdentNode>(call.Target).Name);
        Assert.IsType<CelConstantNode>(Assert.Single(call.Arguments));
        Assert.Equal(2, call.Children.Length);
        Assert.Same(call.Target, call.Children[0]);
    }

    [Fact]
    public static void DescendantsAndSelf_IsInPreOrder()
    {
        CelParsedExpr tree = Parse("a + b * c");

        string[] names = [.. tree.Root.DescendantsAndSelf().Select(NameOf)];

        Assert.Equal(["_+_", "a", "_*_", "b", "c"], names);
    }

    [Fact]
    public static void FindNodeAndFindParent_SearchById()
    {
        CelParsedExpr tree = Parse("a + b * c");
        CelCallNode root = Assert.IsType<CelCallNode>(tree.Root);
        CelCallNode product = Assert.IsType<CelCallNode>(root.Arguments[1]);
        CelExprNode b = product.Arguments[0];

        Assert.Same(product, tree.FindNode(product.Id));
        Assert.Same(product, tree.FindParent(b.Id));
        Assert.Null(tree.FindParent(root.Id));
        Assert.Null(tree.FindNode(1000));
        Assert.Null(tree.FindParent(1000));
    }

    [Fact]
    public static void FindParent_OfAnEntry_IsTheMap()
    {
        CelParsedExpr tree = Parse("{\"a\": 1}");
        CelMapNode map = Assert.IsType<CelMapNode>(tree.Root);

        Assert.Same(map, tree.FindParent(map.Entries[0].Id));
        Assert.Null(tree.FindNode(map.Entries[0].Id));
    }

    [Fact]
    public static void GetSpan_OfAnIdOutsideTheTree_IsArgumentOutOfRange()
    {
        CelParsedExpr tree = Parse("a + b");

        Assert.Throws<ArgumentOutOfRangeException>(() => tree.GetSpan(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.GetSpan(1000));
    }

    [Fact]
    public static void DeepTree_IsWalkedWithoutRunningOutOfStack()
    {
        var factory = new CelExprFactory();
        CelExprNode node = factory.Ident("x");
        for (int i = 0; i < 10_000; i++)
        {
            node = factory.Call("!_", node);
        }

        CelParsedExpr tree = factory.Build(node);

        Assert.Equal(10_001, tree.Root.DescendantsAndSelf().Count());
        Assert.NotNull(tree.FindNode(1));
    }

    [Fact]
    public static void OneTree_IsReadFromManyThreads()
    {
        CelParsedExpr tree = Parse("[1, 2, 3].map(v, v * 2).filter(w, w > 2) + [4, 5].size() + {\"k\": a}.size()");
        int expected = tree.Root.DescendantsAndSelf().Count();
        int[] counts = new int[64];

        Parallel.For(
            0,
            counts.Length,
            new ParallelOptions { CancellationToken = TestContext.Current.CancellationToken },
            thread =>
            {
                CelExprNode[] nodes = [.. tree.Root.DescendantsAndSelf()];
                counts[thread] = nodes.Count(node => ReferenceEquals(tree.FindNode(node.Id), node) && node.Children.Length >= 0);
            });

        Assert.All(counts, count => Assert.Equal(expected, count));
    }

    [Fact]
    public static void Parse_OfAWrongText_HasTheErrorsOfCompileAndNoExpression()
    {
        CelParseResult result = Env.Parse("x +");
        CelCompileResult<long> compiled = Env.Compile<long>("x +");

        Assert.False(result.IsSuccess);
        Assert.Equal(
            compiled.Errors.Select(error => (error.Code, error.Stage, error.Location.Offset, error.Span.Length)),
            result.Errors.Select(error => (error.Code, error.Stage, error.Location.Offset, error.Span.Length)));
        Assert.NotEmpty(result.Errors);
        Assert.Throws<CelException>(() => result.ParsedExpr);
        Assert.Throws<CelException>(result.GetParsedExprOrThrow);
    }

    [Fact]
    public static void Parse_WithoutTheOptionalSet_IsSyntaxNotEnabledAtTheQuestionMark()
    {
        CelParseResult result = EnvNoOptional.Parse("m.?f");

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.SyntaxNotEnabled, 2), (error.Code, error.Location.Offset));
    }

    [Fact]
    public static void Parse_OfNull_IsArgumentNullException()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Env.Parse(null!));

        Assert.Equal("expression", exception.ParamName);
    }

    [Fact]
    public static void Parse_WithASourceLocation_NamesTheSourceOfTheTree()
    {
        var options = new CelCompileOptions { SourceLocation = new CelSourceLocation("rules.json", 10, 2, 5) };

        CelParsedExpr tree = Env.Parse("x", options).GetParsedExprOrThrow();

        Assert.Equal("rules.json", tree.SourceName);
        Assert.Null(Parse("x").SourceName);
    }

    [Fact]
    public static void Parse_WithASourceLocation_ReportsTheErrorsInTheDocument()
    {
        var options = new CelCompileOptions { SourceLocation = new CelSourceLocation("rules.json", 10, 2, 5) };

        CelError error = Assert.Single(Env.Parse("x +", options).Errors);

        Assert.True(error.SourceLocation.HasValue);
        CelSourceLocation place = error.SourceLocation.GetValueOrDefault();
        Assert.Equal("rules.json", place.SourceName);
        Assert.Equal(10 + error.Location.Offset, place.Offset);
    }

    [Fact]
    public static void Children_AreTheSameArrayOnEveryRead()
    {
        CelCallNode root = Assert.IsType<CelCallNode>(Parse("a + b").Root);

        ImmutableArray<CelExprNode> first = root.Children;
        ImmutableArray<CelExprNode> second = root.Children;

        Assert.True(first == second);
    }

    // The public overload: a call with the text only binds to the internal one in this assembly.
    private static CelParsedExpr Parse(string text) => Env.Parse(text, (CelCompileOptions?)null).GetParsedExprOrThrow();

    private static string NameOf(CelExprNode node) => node switch
    {
        CelCallNode call => call.Function,
        CelIdentNode ident => ident.Name,
        _ => node.Kind.ToString(),
    };
}
