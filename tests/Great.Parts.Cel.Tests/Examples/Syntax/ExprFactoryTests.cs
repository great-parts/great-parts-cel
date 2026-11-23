using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Syntax;

// CelExprFactory: the tree of a factory equals the tree of the text that parses into it; the positions of a factory with a source
// text; the checks of its arguments; ExpandMacro for the macros of the library; Copy with replacement and with a map of the
// offsets; Build.
[Trait("Requirement", "F-19")]
[Trait("Requirement", "F-21")]
public sealed class ExprFactoryTests
{
    private static readonly CelEnvironment Env = TreeEnvironments.Env;

    // An environment with every set that brings a macro, to parse the text of each macro of the library.
    private static readonly CelEnvironment EnvAll = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Optional(2))
        .AddExtension(CelExtensions.Math(3))
        .AddExtension(CelExtensions.Lists(4))
        .AddExtension(CelExtensions.Bindings(1))
        .AddExtension(CelExtensions.TwoVarComprehensions(0))
        .AddExtension(CelExtensions.Protos(0))
        .Build()
        .GetEnvironmentOrThrow();

    // The text of a call of each macro of the library and the way to build the same tree: the expansion of the call, with its
    // target and arguments created by the factory.
    private static readonly (string Text, Func<CelExprFactory, CelExprNode> Build)[] Macros =
    [
        ("has(m.f)", f => f.ExpandMacro("has", null, f.Select(f.Ident("m"), "f"))),
        ("items.all(v, v > 0)", f => f.ExpandMacro("all", f.Ident("items"), f.Ident("v"), Positive(f, "v"))),
        ("items.exists(v, v > 0)", f => f.ExpandMacro("exists", f.Ident("items"), f.Ident("v"), Positive(f, "v"))),
        ("items.exists_one(v, v > 0)", f => f.ExpandMacro("exists_one", f.Ident("items"), f.Ident("v"), Positive(f, "v"))),
        ("items.map(v, v * 2)", f => f.ExpandMacro("map", f.Ident("items"), f.Ident("v"), Twice(f, "v"))),
        ("items.map(v, v > 0, v * 2)", f => f.ExpandMacro("map", f.Ident("items"), f.Ident("v"), Positive(f, "v"), Twice(f, "v"))),
        ("items.filter(v, v > 0)", f => f.ExpandMacro("filter", f.Ident("items"), f.Ident("v"), Positive(f, "v"))),
        ("items.all(i, v, v > i)", f => f.ExpandMacro("all", f.Ident("items"), f.Ident("i"), f.Ident("v"), f.Call("_>_", f.Ident("v"), f.Ident("i")))),
        ("items.exists(i, v, v > i)", f => f.ExpandMacro("exists", f.Ident("items"), f.Ident("i"), f.Ident("v"), f.Call("_>_", f.Ident("v"), f.Ident("i")))),
        ("items.existsOne(i, v, v > i)", f => f.ExpandMacro("existsOne", f.Ident("items"), f.Ident("i"), f.Ident("v"), f.Call("_>_", f.Ident("v"), f.Ident("i")))),
        ("items.transformList(i, v, v * 2)", f => f.ExpandMacro("transformList", f.Ident("items"), f.Ident("i"), f.Ident("v"), Twice(f, "v"))),
        ("items.transformList(i, v, v > 0, v * 2)", f => f.ExpandMacro("transformList", f.Ident("items"), f.Ident("i"), f.Ident("v"), Positive(f, "v"), Twice(f, "v"))),
        ("m.transformMap(k, v, v)", f => f.ExpandMacro("transformMap", f.Ident("m"), f.Ident("k"), f.Ident("v"), f.Ident("v"))),
        ("m.transformMapEntry(k, v, {k: v})", f => f.ExpandMacro("transformMapEntry", f.Ident("m"), f.Ident("k"), f.Ident("v"), f.Map([f.MapEntry(f.Ident("k"), f.Ident("v"))]))),
        ("cel.bind(t, 1, t + 1)", f => f.ExpandMacro("cel.bind", null, f.Ident("t"), One(f), f.Call("_+_", f.Ident("t"), One(f)))),
        ("math.greatest(1, 1, 1)", f => f.ExpandMacro("math.greatest", null, One(f), One(f), One(f))),
        ("math.least(1, 1)", f => f.ExpandMacro("math.least", null, One(f), One(f))),
        ("items.sortBy(v, -v)", f => f.ExpandMacro("sortBy", f.Ident("items"), f.Ident("v"), f.Call("-_", f.Ident("v")))),
        ("d.optMap(v, v + 1)", f => f.ExpandMacro("optMap", f.Ident("d"), f.Ident("v"), f.Call("_+_", f.Ident("v"), One(f)))),
        ("d.optFlatMap(v, optional.of(v))", f => f.ExpandMacro("optFlatMap", f.Ident("d"), f.Ident("v"), f.MemberCall("of", f.Ident("optional"), f.Ident("v")))),
        ("proto.getExt(d, pkg.ext)", f => f.ExpandMacro("proto.getExt", null, f.Ident("d"), f.Select(f.Ident("pkg"), "ext"))),
        ("proto.hasExt(d, pkg.ext)", f => f.ExpandMacro("proto.hasExt", null, f.Ident("d"), f.Select(f.Ident("pkg"), "ext"))),
    ];

    [Fact]
    public static void ExpandMacro_GivesTheTreeOfTheTextAndRecordsTheCall()
    {
        var f = new CelExprFactory();
        CelExprNode tree = f.ExpandMacro("exists", f.Ident("items"), f.Ident("v"), f.Call("_>_", f.Ident("v"), f.Constant(CelValue.FromInt64(0))));

        CelParsedExpr built = f.Build(tree);

        Assert.Null(SyntaxTreeComparer.Difference(built.Root, Env.Parse("items.exists(v, v > 0)").GetParsedExprOrThrow().Root));
        Assert.Equal("exists", built.GetMacroCall(built.Root.Id)!.Function);
        Assert.Equal("items.exists(v, v > 0)", CelSyntax.Format(built));
        Assert.Equal(default, built.GetSpan(built.Root.Id));
    }

    [Fact]
    public static void ExpandMacro_OfEveryMacro_GivesTheTreeOfTheText()
    {
        foreach ((string text, Func<CelExprFactory, CelExprNode> build) in Macros)
        {
            var f = new CelExprFactory();
            CelParsedExpr built = f.Build(build(f));
            CelParsedExpr parsed = EnvAll.Parse(text).GetParsedExprOrThrow();

            Assert.True(SyntaxTreeComparer.Difference(built.Root, parsed.Root) is null, text + ": " + SyntaxTreeComparer.Difference(built.Root, parsed.Root));
            Assert.Equal(parsed.GetMacroCall(parsed.Root.Id)!.Function, built.GetMacroCall(built.Root.Id)!.Function);
            Assert.Equal(text, CelSyntax.Format(built));
        }
    }

    [Fact]
    public static void Position_GivesTheRangeOfTheSourceTextToTheNodesCreatedWhileItIsSet()
    {
        const string Filter = "total > 100";
        var g = new CelExprFactory(Filter);
        g.Position = 0..5;
        CelExprNode total = g.Ident("total");
        g.Position = 6..7;

        CelParsedExpr p = g.Build(g.Call("_>_", total, g.Constant(CelValue.FromInt64(100))));

        CelSourceSpan span = p.GetSpan(p.Root.Id);
        Assert.Equal((6, 1, 1, 7), (span.Start.Offset, span.Length, span.Start.Line, span.Start.Column));
        Assert.Equal((0, 5), (p.GetSpan(total.Id).Start.Offset, p.GetSpan(total.Id).Length));
        Assert.Equal((Range?)(0..5), new CelExprFactory(Filter) { Position = 0..5 }.Position);
    }

    [Fact]
    public static void Position_FromTheEnd_IsResolvedAgainstTheSourceText()
    {
        var f = new CelExprFactory("abc") { Position = ^2.. };
        CelIdentNode x = f.Ident("x");

        CelSourceSpan span = f.Build(x).GetSpan(x.Id);

        Assert.Equal((1, 2), (span.Start.Offset, span.Length));
    }

    [Fact]
    public static void Position_WithoutASourceText_IsInvalidOperation()
    {
        var f = new CelExprFactory();

        Assert.Throws<InvalidOperationException>(() => f.Position = 0..1);
        f.Position = null;
        Assert.Null(f.Position);
    }

    [Fact]
    public static void Position_OutsideTheSourceText_IsArgumentOutOfRange()
    {
        var f = new CelExprFactory("ab");

        Assert.Throws<ArgumentOutOfRangeException>(() => f.Position = 0..3);
        Assert.Equal("ab", f.SourceText);
    }

    [Fact]
    public static void Node_WithoutAPosition_HasNoSpan()
    {
        var f = new CelExprFactory("abc");
        CelIdentNode x = f.Ident("x");

        Assert.Equal(default, f.Build(x).GetSpan(x.Id));
    }

    [Fact]
    public static void EmptyNames_AreArgumentException()
    {
        var f = new CelExprFactory();
        CelIdentNode x = f.Ident("x");

        Assert.Throws<ArgumentException>(() => f.Ident(string.Empty));
        Assert.Throws<ArgumentException>(() => f.Select(x, string.Empty));
        Assert.Throws<ArgumentException>(() => f.Call(string.Empty));
        Assert.Throws<ArgumentException>(() => f.Struct(string.Empty, []));
    }

    [Fact]
    public static void NullName_IsArgumentNullException()
    {
        var f = new CelExprFactory();

        Assert.Throws<ArgumentNullException>(() => f.Ident(null!));
    }

    [Fact]
    public static void Constant_OfAKindWithoutALiteral_IsArgumentException()
    {
        var f = new CelExprFactory();

        Assert.Throws<ArgumentException>(() => f.Constant(CelValue.FromList(CelList.Create())));
        Assert.Throws<ArgumentException>(() => f.Constant(CelValue.FromTimestamp(new CelTimestamp(0, 0))));
    }

    [Fact]
    public static void Constant_OfTheKindsOfALiteral_IsCreated()
    {
        var f = new CelExprFactory();
        CelValue[] values =
        [
            CelValue.Null,
            CelValue.FromBoolean(true),
            CelValue.FromInt64(1),
            CelValue.FromUInt64(1),
            CelValue.FromDouble(1.5),
            CelValue.FromString("s"),
            CelValue.FromBytes(ReadOnlyMemory<byte>.Empty),
            CelValue.FromDecimal(1.5m),
        ];

        Assert.All(values, value => Assert.Equal(value.Kind, f.Constant(value).Value.Kind));
    }

    [Theory]
    [InlineData("@index0")]
    [InlineData("@result")]
    [InlineData(".x")]
    public static void Ident_OfAnInternalNameOrALeadingDot_IsCreatedAsGiven(string name)
    {
        Assert.Equal(name, new CelExprFactory().Ident(name).Name);
    }

    [Fact]
    public static void List_WithAnOptionalIndexOutsideOrRepeated_IsArgumentOutOfRange()
    {
        var f = new CelExprFactory();
        CelIdentNode a = f.Ident("a");
        CelIdentNode b = f.Ident("b");

        Assert.Throws<ArgumentOutOfRangeException>(() => f.List([a, b], [2]));
        Assert.Throws<ArgumentOutOfRangeException>(() => f.List([a, b], [1, 1]));
        Assert.Equal<int>([0, 1], f.List([a, b], [1, 0]).OptionalIndices);
    }

    [Fact]
    public static void NodeOfAnotherFactory_IsArgumentException()
    {
        var f1 = new CelExprFactory();
        var f2 = new CelExprFactory();
        CelIdentNode n = f1.Ident("n");

        Assert.Throws<ArgumentException>(() => f2.Call("f", n));
        Assert.Throws<ArgumentException>(() => f2.Select(n, "f"));
        Assert.Throws<ArgumentException>(() => f2.MapEntry(n, f2.Ident("v")));
    }

    [Fact]
    public static void Build_OfANodeUsedTwice_IsArgumentException()
    {
        var f = new CelExprFactory();
        CelIdentNode n = f.Ident("n");

        Assert.Throws<ArgumentException>(() => f.Build(f.Call("_+_", n, n)));
    }

    [Fact]
    public static void Build_OfTheSubtreeOfARoot_KeepsTheIdsOfItsNodes()
    {
        var f = new CelExprFactory();
        CelIdentNode unused = f.Ident("u");
        CelIdentNode x = f.Ident("x");
        CelCallNode root = f.Call("!_", x);

        CelParsedExpr tree = f.Build(root);

        Assert.Same(x, tree.FindNode(x.Id));
        Assert.Null(tree.FindNode(unused.Id));
        Assert.Equal("!x", CelSyntax.Format(tree));
    }

    [Fact]
    public static void ExpandMacro_WithAnArgumentThatIsNotAName_IsArgumentExceptionWithTheTextOfTheCode()
    {
        var f = new CelExprFactory();
        CelExprNode items = f.Ident("items");

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => f.ExpandMacro("all", items, f.Constant(CelValue.FromInt64(1)), f.Constant(CelValue.FromBoolean(true))));

        Assert.Contains(nameof(CelErrorCode.InvalidMacroCall), exception.Message, StringComparison.Ordinal);
        Assert.Contains("argument must be a simple name", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public static void ExpandMacro_OfAnUnknownMacroOrOfTheWrongStyle_IsArgumentException()
    {
        var f = new CelExprFactory();
        CelIdentNode x = f.Ident("x");
        CelIdentNode v = f.Ident("v");
        CelIdentNode p = f.Ident("p");

        Assert.Throws<ArgumentException>(() => f.ExpandMacro("nope", null, x));
        Assert.Throws<ArgumentException>(() => f.ExpandMacro("all", null, v, p));
        Assert.Throws<ArgumentException>(() => f.ExpandMacro("has", x, v));
    }

    [Fact]
    public static void ExpandMacro_Has_IsAPresenceTestThatPrintsAsHas()
    {
        var f = new CelExprFactory();

        CelParsedExpr tree = f.Build(f.ExpandMacro("has", null, f.Select(f.Ident("m"), "f")));

        Assert.True(Assert.IsType<CelSelectNode>(tree.Root).IsPresenceTest);
        Assert.Equal("has(m.f)", CelSyntax.Format(tree));
    }

    [Fact]
    public static void Copy_WithoutReplacement_GivesAnEqualTreeWithItsMacroCalls()
    {
        CelParsedExpr source = Env.Parse("x + items.all(v, v > x) + (has(m.f) ? 1 : 2)").GetParsedExprOrThrow();
        var f = new CelExprFactory();

        CelExprNode copy = f.Copy(source, source.Root);
        CelParsedExpr built = f.Build(copy);

        Assert.NotSame(source.Root, copy);
        Assert.Null(SyntaxTreeComparer.Difference(built.Root, source.Root));
        Assert.Equal(CelSyntax.Format(source), CelSyntax.Format(built));
    }

    [Fact]
    public static void Copy_WithAReplacement_PutsItInTheTreeAndInTheMacroCalls()
    {
        CelParsedExpr source = Env.Parse("x + items.all(v, v > x)").GetParsedExprOrThrow();
        var f = new CelExprFactory();

        CelExprNode copy = f.Copy(source, source.Root, node => node is CelIdentNode { Name: "x" } ? f.Constant(CelValue.FromInt64(5)) : null);

        Assert.Equal("5 + items.all(v, v > 5)", CelSyntax.Format(f.Build(copy)));
    }

    [Fact]
    public static void Copy_WithAReplacementOfALoop_DropsItsMacroCall()
    {
        CelParsedExpr source = Env.Parse("x + items.all(v, v > x)").GetParsedExprOrThrow();
        var f = new CelExprFactory();

        CelExprNode copy = f.Copy(source, source.Root, node => node is CelComprehensionNode ? f.Constant(CelValue.FromBoolean(true)) : null);
        CelParsedExpr built = f.Build(copy);

        Assert.Equal("x + true", CelSyntax.Format(built));
        Assert.All(built.Root.DescendantsAndSelf(), node => Assert.Null(built.GetMacroCall(node.Id)));
    }

    [Fact]
    public static void Copy_WithAMapOfOffsets_MovesTheStartsOfTheCopies()
    {
        const string Text = "a + b * c";
        CelParsedExpr source = Env.Parse(Text).GetParsedExprOrThrow();
        var f = new CelExprFactory("0123456789" + Text);

        CelExprNode copy = f.Copy(source, source.Root, null, offset => offset + 10);
        CelParsedExpr built = f.Build(copy);

        CelExprNode[] before = [.. source.Root.DescendantsAndSelf()];
        CelExprNode[] after = [.. built.Root.DescendantsAndSelf()];
        Assert.Equal(before.Length, after.Length);
        for (int i = 0; i < before.Length; i++)
        {
            Assert.Equal(source.GetSpan(before[i].Id).Start.Offset + 10, built.GetSpan(after[i].Id).Start.Offset);
            Assert.Equal(source.GetSpan(before[i].Id).Length, built.GetSpan(after[i].Id).Length);
        }
    }

    [Fact]
    public static void Copy_WithAMapThatLeavesTheSourceText_IsArgumentException()
    {
        CelParsedExpr source = Env.Parse("a + b").GetParsedExprOrThrow();
        var f = new CelExprFactory("a + b");

        Assert.Throws<ArgumentException>(() => f.Copy(source, source.Root, null, offset => offset + 10));
    }

    [Fact]
    public static void Copy_OfANodeOfAnotherTree_IsArgumentException()
    {
        CelParsedExpr source = Env.Parse("a + b").GetParsedExprOrThrow();
        CelParsedExpr other = Env.Parse("a + b").GetParsedExprOrThrow();
        var f = new CelExprFactory();

        Assert.Throws<ArgumentException>(() => f.Copy(source, other.Root));
    }

    [Fact]
    public static void Copy_WithAReplacementOfAnotherFactory_IsArgumentException()
    {
        CelParsedExpr source = Env.Parse("a + b").GetParsedExprOrThrow();
        var f = new CelExprFactory();
        var other = new CelExprFactory();

        Assert.Throws<ArgumentException>(() => f.Copy(source, source.Root, node => node is CelIdentNode ? other.Ident("z") : null));
    }

    [Fact]
    public static void Comprehension_WithEqualNamesOrAForeignNode_IsArgumentException()
    {
        var f = new CelExprFactory();
        var other = new CelExprFactory();
        CelIdentNode range = f.Ident("items");
        CelConstantNode truth = f.Constant(CelValue.FromBoolean(true));

        Assert.Throws<ArgumentException>(() => f.Comprehension("v", range, "v", truth, truth, truth, truth));
        Assert.Throws<ArgumentException>(() => f.Comprehension("v", range, "@result", truth, truth, truth, other.Ident("r")));
        Assert.Throws<ArgumentException>(() => f.Comprehension("i", "i", range, "@result", truth, truth, truth, truth));
    }

    private static CelCallNode Positive(CelExprFactory f, string name) => f.Call("_>_", f.Ident(name), One(f, 0));

    private static CelCallNode Twice(CelExprFactory f, string name) => f.Call("_*_", f.Ident(name), One(f, 2));

    private static CelConstantNode One(CelExprFactory f, long value = 1) => f.Constant(CelValue.FromInt64(value));
}
