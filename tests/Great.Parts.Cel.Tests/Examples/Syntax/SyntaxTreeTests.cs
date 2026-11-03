using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Syntax;

// The syntax tree of an expression, the positions of its nodes and the expansion of macros, as in cel-go
// v0.32.0. Each expected tree was printed in the same notation by a probe over the cel-go parser
// (parser.NewParser with the standard macros, EnableHiddenAccumulatorName and, for "opt", EnableOptionalSyntax).
//
// The notation, one node after another, each followed by "@" and the offset of the node:
// - an identifier is its name; a constant is its value, uint with "u", a string in double quotes;
// - a selection is "operand.field", a presence test "operand.field~test-only";
// - a call is "function(arguments)", a call with a target "target.function(arguments)";
// - a list is "[elements]", "?" before an optional element;
// - a comprehension is "comprehension(IterVar, IterRange, AccuVar, AccuInit, LoopCondition, LoopStep, Result)".
//
// The first column of a table names the syntax options: "std" those of the environment, "opt" with the
// optional syntax, "none" without macros. "\n" in an expression is a line break.
public sealed class SyntaxTreeTests
{
    private const string GrammarCases = """
        std|a.b => a@0.b@1
        std|x.y\n.z => x@0.y@1.z@4
        std|a.f(1) => a@0.f(1@4)@3
        std|f(1) => f(1@2)@1
        std|a[0] => _[_](a@0, 0@2)@1
        std|.x => .x@1
        std|a.`b-c` => a@0.b-c@1
        std|a.if => a@0.if@1
        std|a.package() => a@0.package()@9
        std|a in b => @in(a@0, b@5)@2
        std|a - b - c => _-_(_-_(a@0, b@4)@2, c@8)@6
        std|a + b * c => _+_(a@0, _*_(b@4, c@8)@6)@2
        std|(a + b) * c => _*_(_+_(a@1, b@5)@3, c@10)@8
        std|1 +\n  (2 *\n   3.b(4)) => _+_(1@0, _*_(2@7, 3@14.b(4@18)@17)@9)@2
        std|x // c\n+ 1 => _+_(x@0, 1@9)@7
        std|a ? b : c ? d : e => _?_:_(a@0, b@4, _?_:_(c@8, d@12, e@16)@10)@2
        std|a || b || c || d || e => _||_(_||_(_||_(a@0, b@5)@2, c@10)@7, _||_(d@15, e@20)@17)@12
        std|a && b && c && d => _&&_(_&&_(a@0, b@5)@2, _&&_(c@10, d@15)@12)@7
        std|! !!x => !_(x@4)@0
        std|--x => x@2
        std|-x => -_(x@1)@0
        std|x + -1 => _+_(x@0, -1@4)@2
        std|-1.f => -1@0.f@2
        std|!-1 => !_(-1@1)@0
        std|--1 => 1@2
        std|---1 => -_(1@3)@0
        std|-(-1) => -_(-1@2)@0
        std|[1, 2] => [1@1, 2@4]@0
        std|[1,] => [1@1]@0
        opt|a.?b => _?._(a@0, "b"@3)@1
        opt|a\n.?b => _?._(a@0, "b"@4)@2
        opt|a. ?b => _?._(a@0, "b"@4)@1
        opt|a[?0] => _[?_](a@0, 0@3)@1
        opt|[?a, b] => [?a@2, b@5]@0
        """;

    // The seven standard macros; calls that do not match a macro by name, argument count and style; the
    // same calls without macros.
    private const string MacroCases = """
        std|has(a.b) => a@4.b~test-only@3
        std|has(a.b).c => a@4.b~test-only@3.c@8
        std|[1, 2].all(x, x > 0) => comprehension(x, [1@1, 2@4]@0, @result, true@10, @not_strictly_false(@result@10)@10, _&&_(@result@10, _>_(x@14, 0@18)@16)@10, @result@10)@10
        std|[1, 2].exists(x, x > 0) => comprehension(x, [1@1, 2@4]@0, @result, false@13, @not_strictly_false(!_(@result@13)@13)@13, _||_(@result@13, _>_(x@17, 0@21)@19)@13, @result@13)@13
        std|[1, 2].exists_one(x, x > 0) => comprehension(x, [1@1, 2@4]@0, @result, 0@17, true@17, _?_:_(_>_(x@21, 0@25)@23, _+_(@result@17, 1@17)@17, @result@17)@17, _==_(@result@17, 1@17)@17)@17
        std|[1, 2].map(x, x * 2) => comprehension(x, [1@1, 2@4]@0, @result, []@10, true@10, _+_(@result@10, [_*_(x@14, 2@18)@16]@10)@10, @result@10)@10
        std|[1, 2].map(x, x > 0, x * 2) => comprehension(x, [1@1, 2@4]@0, @result, []@10, true@10, _?_:_(_>_(x@14, 0@18)@16, _+_(@result@10, [_*_(x@21, 2@25)@23]@10)@10, @result@10)@10, @result@10)@10
        std|[1, 2].filter(x, x > 0) => comprehension(x, [1@1, 2@4]@0, @result, []@13, true@13, _?_:_(_>_(x@17, 0@21)@19, _+_(@result@13, [x@14]@13)@13, @result@13)@13, @result@13)@13
        std|m.all(k, m[k].exists(v, v == k)) => comprehension(k, m@0, @result, true@5, @not_strictly_false(@result@5)@5, _&&_(@result@5, comprehension(v, _[_](m@9, k@11)@10, @result, false@20, @not_strictly_false(!_(@result@20)@20)@20, _||_(@result@20, _==_(v@24, k@29)@26)@20, @result@20)@20)@5, @result@5)@5
        std|[1].all(x) => [1@1]@0.all(x@8)@7
        std|[1].map(x, y, z, w) => [1@1]@0.map(x@8, y@11, z@14, w@17)@7
        std|all([1], x, true) => all([1@5]@4, x@9, true@12)@3
        std|has(a.b, c) => has(a@4.b@5, c@9)@3
        std|a.has(b.c) => a@0.has(b@6.c@7)@5
        none|has(a.b) => has(a@4.b@5)@3
        none|[1, 2].all(x, x > 0) => [1@1, 2@4]@0.all(x@11, _>_(x@14, 0@18)@16)@10
        """;

    // Pairs of texts that parse into equal or into different trees by SyntaxTreeComparer: "===" or "!==".
    private const string EqualityCases = """
        std|a.`b` === a.b
        std|x{`f`: 1} === x{f: 1}
        std|a . f ( 1 ) === a.f(1)
        std|(((x))) === x
        std|!!!x === !x
        std|---1 === -(1)
        std|[1, 2,] === [1, 2]
        std|x{f: 1,} === x{f: 1}
        std|a || b || c || d === (a || b) || (c || d)
        std|a && b && c === (a && b) && c
        std|a ? b : c ? d : e === a ? b : (c ? d : e)
        std|a - b - c !== a - (b - c)
        std|a || b || c || d !== ((a || b) || c) || d
        std|-(1) !== -1
        std|has(a.b) !== a.b
        opt|[?a] !== [a]
        opt|{?1: 2} !== {1: 2}
        opt|x{?f: 1} !== x{f: 1}
        """;

    private readonly CelEnvironment environment =
        new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    public static TheoryData<string, string, string> Grammar => Cases(GrammarCases, " => ");

    public static TheoryData<string, string, string> Macros => Cases(MacroCases, " => ");

    public static TheoryData<string, string, string, string> Equalities
    {
        get
        {
            var data = new TheoryData<string, string, string, string>();
            foreach (string line in Lines(EqualityCases))
            {
                string relation = line.Contains(" === ", StringComparison.Ordinal) ? "===" : "!==";
                int bar = line.IndexOf('|', StringComparison.Ordinal);
                string[] texts = line[(bar + 1)..].Split(" " + relation + " ");
                data.Add(line[..bar], texts[0], relation, texts[1]);
            }

            return data;
        }
    }

    [Theory]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "F-4")]
    [MemberData(nameof(Grammar))]
    public void Expression_ParsesIntoTheTreeOfCelGo(string syntax, string expression, string tree)
    {
        ArgumentNullException.ThrowIfNull(expression);

        Assert.Equal(tree, Notation(Parse(syntax, expression)));
    }

    // A macro call is replaced by its expansion; every node the expansion creates is at the "(" of the call,
    // the nodes of the target and the arguments keep their places.
    [Theory]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "F-4")]
    [MemberData(nameof(Macros))]
    public void MacroCall_ExpandsAsInCelGo(string syntax, string expression, string tree)
    {
        ArgumentNullException.ThrowIfNull(expression);

        Assert.Equal(tree, Notation(Parse(syntax, expression)));
    }

    [Theory]
    [Trait("Requirement", "F-1")]
    [MemberData(nameof(Equalities))]
    public void TwoTexts_ParseIntoEqualOrDifferentTrees(string syntax, string left, string relation, string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        string? difference = SyntaxTreeComparer.Difference(Parse(syntax, left).Root, Parse(syntax, right).Root);

        Assert.Equal(relation == "===", difference is null);
    }

    // A map is a struct node without a message name. The node is at its "{", each entry at its ":", as in cel-go.
    [Theory]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "F-4")]
    [InlineData("std", "{1: 2, 3: 4}", "", "0 2 8")]
    [InlineData("std", "a.b{f: 1, g: 2}", "a.b", "3 5 11")]
    [InlineData("std", ".a.b{f: 1}", ".a.b", "4 6")]
    [InlineData("std", "x{`f.g`: 1}", "x", "1 7")]
    [InlineData("std", "x{}", "x", "1")]
    [InlineData("opt", "{?1: 2}", "", "0 3")]
    [InlineData("opt", "a.b{?f: 1}", "a.b", "3 6")]
    public void MapOrMessage_IsAtItsBraceAndEntriesAtTheirColons(string syntax, string expression, string messageName, string offsets)
    {
        ArgumentNullException.ThrowIfNull(expression);

        ParsedExpression parsed = Parse(syntax, expression);
        StructNode node = Assert.IsType<StructNode>(parsed.Root);

        Assert.Equal(messageName, node.MessageName);
        Assert.Equal(
            offsets,
            string.Join(' ', node.Entries.Select(entry => entry.Id).Prepend(node.Id).Select(id => Offset(parsed, id))));
    }

    private ParsedExpression Parse(string syntax, string expression)
    {
        ParseResult result = environment.Parse(expression.Replace("\\n", "\n", StringComparison.Ordinal), Options(syntax));

        Assert.True(result.IsSuccess, expression + ": " + string.Join("; ", result.Errors.Select(error => error.Message)));
        Assert.NotNull(result.Expression);
        return result.Expression;
    }

    private static SyntaxOptions? Options(string syntax) => syntax switch
    {
        "std" => null,
        "opt" => SyntaxOptions.Standard with { OptionalSyntax = true },
        "none" => new SyntaxOptions(OptionalSyntax: false, Macros: MacroTable.Empty),
        _ => throw new UnreachableException(syntax),
    };

    private static TheoryData<string, string, string> Cases(string table, string separator)
    {
        var data = new TheoryData<string, string, string>();
        foreach (string line in Lines(table))
        {
            int bar = line.IndexOf('|', StringComparison.Ordinal);
            int arrow = line.IndexOf(separator, StringComparison.Ordinal);
            data.Add(line[..bar], line[(bar + 1)..arrow], line[(arrow + separator.Length)..]);
        }

        return data;
    }

    private static string[] Lines(string table) => table.ReplaceLineEndings("\n").Split('\n');

    private static string Notation(ParsedExpression parsed) => Notation(parsed, parsed.Root);

    private static string Notation(ParsedExpression parsed, ExprNode node)
    {
        string text = node switch
        {
            IdentNode ident => ident.Name,
            ConstantNode constant => Constant(constant.Value),
            SelectNode select => Notation(parsed, select.Operand) + "." + select.Field + (select.IsPresenceTest ? "~test-only" : ""),
            CallNode { Target: { } target } call => Notation(parsed, target) + "." + call.Function + "(" + Join(parsed, call.Arguments) + ")",
            CallNode call => call.Function + "(" + Join(parsed, call.Arguments) + ")",
            ListNode list => "[" + string.Join(", ", list.Elements.Select((element, index) =>
                (list.OptionalIndices.Contains(index) ? "?" : "") + Notation(parsed, element))) + "]",
            ComprehensionNode loop => "comprehension(" + string.Join(
                ", ",
                loop.IterVar,
                Notation(parsed, loop.IterRange),
                loop.AccuVar,
                Notation(parsed, loop.AccuInit),
                Notation(parsed, loop.LoopCondition),
                Notation(parsed, loop.LoopStep),
                Notation(parsed, loop.Result)) + ")",
            _ => throw new UnreachableException(),
        };
        return text + "@" + Offset(parsed, node.Id);
    }

    private static string Join(ParsedExpression parsed, ImmutableArray<ExprNode> nodes) =>
        string.Join(", ", nodes.Select(node => Notation(parsed, node)));

    private static string Constant(CelValue value) => value.Kind switch
    {
        CelValueKind.Int when value.TryGetInt64(out long number) => number.ToString(CultureInfo.InvariantCulture),
        CelValueKind.UInt when value.TryGetUInt64(out ulong number) => number.ToString(CultureInfo.InvariantCulture) + "u",
        CelValueKind.Bool when value.TryGetBoolean(out bool flag) => flag ? "true" : "false",
        CelValueKind.String when value.TryGetString(out string? text) => "\"" + text + "\"",
        CelValueKind.Null => "null",
        _ => throw new UnreachableException(),
    };

    private static string Offset(ParsedExpression parsed, int id) =>
        parsed.GetOffset(id).ToString(CultureInfo.InvariantCulture);
}
