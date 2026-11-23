using System;
using System.Linq;
using Great.Parts.Cel.Library.Extensions;
using Great.Parts.Cel.Syntax;
using Xunit;

namespace Great.Parts.Cel.Tests.Syntax;

// ExpandMacro for every macro of the table of the library: the tree that the factory makes from the target and the arguments of
// a call equals the tree that the parser makes from the text of the call, and the table has no macro without a sample.
[Trait("Requirement", "F-19")]
[Trait("Requirement", "F-21")]
public sealed class ExpandMacroTableTests
{
    // An environment with every set that brings a macro, the internal set block included, to parse the text of each macro.
    private static readonly CelEnvironment Env = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Optional(2))
        .AddExtension(CelExtensions.Math(3))
        .AddExtension(CelExtensions.Lists(4))
        .AddExtension(CelExtensions.Bindings(1))
        .AddExtension(CelExtensions.TwoVarComprehensions(0))
        .AddExtension(CelExtensions.Protos(0))
        .AddExtension(ExtensionCatalog.Block)
        .Build()
        .GetEnvironmentOrThrow();

    // The name that a text calls the macro by, the number of arguments and a call.
    private static readonly (string Name, int Arity, string Text)[] Samples =
    [
        ("has", 1, "has(m.f)"),
        ("all", 2, "items.all(v, v > 0)"),
        ("exists", 2, "items.exists(v, v > 0)"),
        ("exists_one", 2, "items.exists_one(v, v > 0)"),
        ("map", 2, "items.map(v, v * 2)"),
        ("map", 3, "items.map(v, v > 0, v * 2)"),
        ("filter", 2, "items.filter(v, v > 0)"),
        ("all", 3, "items.all(i, v, v > i)"),
        ("exists", 3, "items.exists(i, v, v > i)"),
        ("existsOne", 3, "items.existsOne(i, v, v > i)"),
        ("exists_one", 3, "items.exists_one(i, v, v > i)"),
        ("transformList", 3, "items.transformList(i, v, v * 2)"),
        ("transformList", 4, "items.transformList(i, v, v > 0, v * 2)"),
        ("transformMap", 3, "m.transformMap(k, v, v)"),
        ("transformMap", 4, "m.transformMap(k, v, v != null, v)"),
        ("transformMapEntry", 3, "m.transformMapEntry(k, v, {k: v})"),
        ("transformMapEntry", 4, "m.transformMapEntry(k, v, k != '', {k: v})"),
        ("math.greatest", 1, "math.greatest(1)"),
        ("math.greatest", 2, "math.greatest(1, 2)"),
        ("math.least", 1, "math.least(1)"),
        ("math.least", 3, "math.least(1, 2, 3)"),
        ("sortBy", 2, "items.sortBy(v, -v)"),
        ("cel.bind", 3, "cel.bind(t, 1, t + 1)"),
        ("proto.getExt", 2, "proto.getExt(d, pkg.ext)"),
        ("proto.hasExt", 2, "proto.hasExt(d, pkg.ext)"),
        ("optMap", 2, "d.optMap(v, v + 1)"),
        ("optFlatMap", 2, "d.optFlatMap(v, optional.of(v))"),
        ("cel.block", 2, "cel.block([1], cel.index(0))"),
        ("cel.index", 1, "cel.index(0)"),
        ("cel.iterVar", 2, "cel.iterVar(0, 1)"),
        ("cel.accuVar", 2, "cel.accuVar(0, 1)"),
    ];

    [Fact]
    public static void EveryMacroOfTheTable_HasASample()
    {
        Macro[] macros = [.. MacroTable.All.Macros];

        string[] withoutSample =
        [
            .. macros
                .Where(macro => !Samples.Any(sample => sample.Name == macro.QualifiedName && (macro.ArgumentCount == Macro.AnyArgumentCount || sample.Arity == macro.ArgumentCount)))
                .Select(macro => macro.QualifiedName + "/" + macro.ArgumentCount),
        ];

        Assert.True(withoutSample.Length == 0, "macros without a sample: " + string.Join(", ", withoutSample));
        Assert.All(Samples, sample => Assert.Contains(macros, macro => macro.QualifiedName == sample.Name));
    }

    [Fact]
    public static void ExpandMacro_OfEveryMacroOfTheTable_GivesTheTreeOfTheTextAndRecordsTheCall()
    {
        foreach ((string name, _, string text) in Samples)
        {
            CelParsedExpr parsed = Env.Parse(text).GetParsedExprOrThrow();
            CelMacroCall call = parsed.GetMacroCall(parsed.Root.Id) ?? throw new InvalidOperationException(text + ": the root is not the expansion of a macro call");
            var factory = new CelExprFactory();

            CelExprNode[] arguments = [.. call.Arguments.Select(argument => Rebuilt(factory, parsed, argument))];
            CelExprNode expanded = factory.ExpandMacro(call.Function, call.Target is null ? null : Rebuilt(factory, parsed, call.Target), arguments);
            CelParsedExpr built = factory.Build(expanded);

            Assert.Equal(name, call.Function);
            Assert.True(SyntaxTreeComparer.Difference(built.Root, parsed.Root) is null, text + ": " + SyntaxTreeComparer.Difference(built.Root, parsed.Root));
            Assert.Equal(name, built.GetMacroCall(built.Root.Id)!.Function);
            Assert.Equal(CelSyntax.Format(parsed), CelSyntax.Format(built));
        }
    }

    // A part of a macro call made again by the factory: a node of the tree is copied; a part that the expansion dropped, such as the
    // variable v of items.all(v, ...), the m.f of has(m.f) or the number of cel.index(0), is made from its kind.
    private static CelExprNode Rebuilt(CelExprFactory factory, CelParsedExpr tree, CelExprNode node) =>
        ReferenceEquals(tree.FindNode(node.Id), node)
            ? factory.Copy(tree, node)
            : node switch
            {
                CelIdentNode identifier => factory.Ident(identifier.Name),
                CelConstantNode constant => factory.Constant(constant.Value),
                CelSelectNode selection => factory.Select(Rebuilt(factory, tree, selection.Operand), selection.Field),
                _ => throw new InvalidOperationException("a dropped part of the kind " + node.Kind),
            };
}
