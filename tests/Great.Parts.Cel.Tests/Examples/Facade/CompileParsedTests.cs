using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Evaluation;
using Great.Parts.Cel.Tests.Examples.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// CompileParsed and ValidateParsed: the tree of any source compiles as the text that parses into it; the places of the errors come
// from the positions of the tree; the syntax that the environment does not accept and the depth are errors of the stage Parse; the
// names that validation reports. The texts of "Equals_text" are those of the public tree, right and wrong in the environment.
[Trait("Requirement", "F-19")]
[Trait("Requirement", "F-20")]
public sealed class CompileParsedTests
{
    private static readonly CelEnvironment Env = TreeEnvironments.Env;
    private static readonly CelEnvironment EnvNoOptional = TreeEnvironments.EnvNoOptional;

    private static readonly string[] Texts =
    [
        "a + b",
        "-5",
        "2.5",
        "1u",
        "\"s\"",
        "true",
        "null",
        "m.f",
        "has(m.f)",
        "items.all(v, v > 0)",
        "items.map(v, v * 2)",
        "cel.bind(t, 1, t + 1)",
        "[1, ?d]",
        "{\"a\": 1, ?\"b\": d}",
        "m.?f",
        "s.startsWith(\"a\")",
        "a ? b : c",
        "a + name",
        "undeclared + 1",
        "items.all(v, v)",
        "name.size(1)",
        "a +",
    ];

    [Fact]
    public static void CompileParsed_OfAParsedText_GivesTheOutcomeOfCompilingTheText()
    {
        CelBindings bindings = Bindings();

        foreach (string text in Texts)
        {
            CelParseResult parsed = Env.Parse(text);
            if (!parsed.IsSuccess)
            {
                Assert.Equal(Errors(Env.Compile<CelValue>(text).Errors), Errors(parsed.Errors));
                continue;
            }

            CelCompileResult<CelValue> byText = Env.Compile<CelValue>(text);
            CelCompileResult<CelValue> byTree = Env.CompileParsed<CelValue>(parsed.GetParsedExprOrThrow());

            Assert.True(byText.IsSuccess == byTree.IsSuccess, text);
            if (!byText.IsSuccess)
            {
                Assert.Equal(Errors(byText.Errors), Errors(byTree.Errors));
                continue;
            }

            Assert.Equal(byText.GetProgramOrThrow().ResultType, byTree.GetProgramOrThrow().ResultType);
            CelEvalResult<CelValue> expected = byText.GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);
            CelEvalResult<CelValue> actual = byTree.GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);
            Assert.True(expected.IsSuccess == actual.IsSuccess, text);
            if (expected.IsSuccess)
            {
                Assert.Equal(expected.GetValueOrThrow().ToString(), actual.GetValueOrThrow().ToString());
            }
            else
            {
                Assert.Equal(Errors(expected.Errors), Errors(actual.Errors));
            }
        }
    }

    [Fact]
    public static void CompileParsed_OfAMessageLiteral_IsCheckedAsTheText()
    {
        CelEnvironment messages = MessageSamples.CreateEnvironment(MessageSamples.NewMessageType());
        const string Text = "acme.test.Message{id: 1}.id";

        CelValidationResult byTree = messages.ValidateParsed<CelValue>(messages.Parse(Text).GetParsedExprOrThrow());

        Assert.True(byTree.IsValid);
        Assert.Equal(messages.Validate<CelValue>(Text).ResultType, byTree.ResultType);
        Assert.False(messages.ValidateParsed<CelValue>(messages.Parse("acme.test.Message{nope: 1}").GetParsedExprOrThrow()).IsValid);
    }

    [Fact]
    public static void UndeclaredName_IsAnErrorAtThePositionOfTheNodeInTheSourceText()
    {
        const string Filter = "totl > 1";
        var f = new CelExprFactory(Filter);
        f.Position = 0..4;
        CelExprNode totl = f.Ident("totl");
        f.Position = null;

        CelCompileResult<bool> result = Env.CompileParsed<bool>(f.Build(f.Call("_>_", totl, f.Constant(CelValue.FromInt64(1)))));

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.UndeclaredReference, "totl"), (error.Code, error.Subject));
        Assert.Equal((0, 4), (error.Span.Start.Offset, error.Span.Length));
        Assert.EndsWith("\ntotl > 1\n^~~~", error.ToDisplayString(Filter), StringComparison.Ordinal);
    }

    [Fact]
    public static void CallWithoutAMatchingOverload_IsAnErrorAtTheNodeOfTheCall()
    {
        var f = new CelExprFactory("name > 1");
        f.Position = 0..4;
        CelExprNode name = f.Ident("name");
        f.Position = 7..8;
        CelExprNode one = f.Constant(CelValue.FromInt64(1));
        f.Position = 5..8;

        CelCompileResult<bool> result = Env.CompileParsed<bool>(f.Build(f.Call("_>_", name, one)));

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.NoMatchingOverload, 5), (error.Code, error.Location.Offset));
    }

    [Fact]
    public static void NodeWithoutAPosition_HasAnErrorWithoutAPlace()
    {
        var f = new CelExprFactory();

        CelCompileResult<bool> result = Env.CompileParsed<bool>(f.Build(f.Ident("totl")));

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.UndeclaredReference, error.Code);
        Assert.Equal(0, error.Location.Line);
        Assert.Equal(default, error.Span);
    }

    [Fact]
    public static void DecimalConstant_WithoutTheLiteralOption_IsSyntaxNotEnabledOfTheStageParse()
    {
        var f = new CelExprFactory();

        CelCompileResult<CelValue> result = Env.CompileParsed<CelValue>(f.Build(f.Constant(CelValue.FromDecimal(1.5m))));

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.SyntaxNotEnabled, CelErrorStage.Parse), (error.Code, error.Stage));
    }

    [Fact]
    public static void OptionalSyntax_WithoutTheOptionalSet_IsSyntaxNotEnabledOfTheStageParse()
    {
        var f = new CelExprFactory();
        CelExprNode tree = f.Call("_?._", f.Ident("m"), f.Constant(CelValue.FromString("f")));

        CelCompileResult<CelValue> result = EnvNoOptional.CompileParsed<CelValue>(f.Build(tree));

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.SyntaxNotEnabled, CelErrorStage.Parse), (error.Code, error.Stage));
    }

    [Fact]
    public static void TreeParsedWithTheOptionalSyntax_IsSyntaxNotEnabledInAnEnvironmentWithoutIt()
    {
        CelParsedExpr tree = Env.Parse("[1, ?d]").GetParsedExprOrThrow();

        CelCompileResult<CelValue> result = EnvNoOptional.CompileParsed<CelValue>(tree);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.SyntaxNotEnabled, CelErrorStage.Parse), (error.Code, error.Stage));
        Assert.True(Env.CompileParsed<CelValue>(tree).IsSuccess);
    }

    [Fact]
    public static void NestedNot_PastTheLimit_IsNestingTooDeepOfTheStageParse()
    {
        CelCompileResult<bool> tooDeep = Env.CompileParsed<bool>(Nested(300));

        CelError error = Assert.Single(tooDeep.Errors);
        Assert.Equal((CelErrorCode.NestingTooDeep, CelErrorStage.Parse), (error.Code, error.Stage));
        Assert.True(Env.CompileParsed<bool>(Nested(250)).IsSuccess);
    }

    [Fact]
    public static void ErrorOfTheCheck_WithASourceLocation_HasThePositionInTheDocument()
    {
        var f = new CelExprFactory("x");
        f.Position = 0..1;
        var options = new CelCompileOptions { SourceLocation = new CelSourceLocation("doc.yaml", 100, 7, 3) };

        CelCompileResult<string> result = Env.CompileParsed<string>(f.Build(f.Ident("x")), options);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.ExpectedTypeMismatch, error.Code);
        Assert.True(error.SourceLocation.HasValue);
        CelSourceLocation place = error.SourceLocation.GetValueOrDefault();
        Assert.Equal(("doc.yaml", 100, 7, 3), (place.SourceName, place.Offset, place.Line, place.Column));
    }

    [Fact]
    public static void ValidateParsed_ReportsTheNamesThatTheTreeRefersTo()
    {
        CelParsedExpr tree = Env.Parse("x > 1 && items.all(v, v > 0)").GetParsedExprOrThrow();

        CelValidationResult result = Env.ValidateParsed<bool>(tree);

        Assert.True(result.IsValid);
        Assert.Equal<string>(["items", "x"], result.ReferencedVariables);
        Assert.Equal<string>(["all"], result.ReferencedFunctions);
    }

    [Fact]
    public static void ValidateParsed_OfALoopWithoutAMacroCall_NamesNoMacro()
    {
        var f = new CelExprFactory();
        CelExprNode loop = f.Comprehension(
            "v",
            f.Ident("items"),
            "@result",
            f.Constant(CelValue.FromBoolean(true)),
            f.Call("@not_strictly_false", f.Ident("@result")),
            f.Call("_&&_", f.Ident("@result"), f.Call("_>_", f.Ident("v"), f.Constant(CelValue.FromInt64(0)))),
            f.Ident("@result"));

        CelValidationResult result = Env.ValidateParsed(f.Build(loop));

        Assert.True(result.IsValid);
        Assert.Equal<string>(["items"], result.ReferencedVariables);
        Assert.Empty(result.ReferencedFunctions);
    }

    [Fact]
    public static void NullTree_IsArgumentNullException()
    {
        Assert.Equal("expression", Assert.Throws<ArgumentNullException>(() => Env.CompileParsed<long>(null!)).ParamName);
        Assert.Equal("expression", Assert.Throws<ArgumentNullException>(() => Env.ValidateParsed(null!)).ParamName);
        Assert.Equal("expression", Assert.Throws<ArgumentNullException>(() => Env.ValidateParsed<long>(null!)).ParamName);
    }

    // What only readers of a tree need — the children of a node and the index by id — is made on the first read, not by compilation.
    [Fact]
    public static void CompileParsed_OfATreeOfTheSameEnvironment_MakesNeitherTheChildrenNorTheIndex()
    {
        // The public overload marks the tree as made by the syntax of the environment, so that the compilation skips the checks of input.
        CelParsedExpr tree = Env.Parse("items.all(v, v > 0) && {\"a\": 1}.size() == 1 && m.f == 1", (CelCompileOptions?)null).GetParsedExprOrThrow();

        Assert.True(Env.CompileParsed<bool>(tree).IsSuccess);
        Assert.True(Env.Compile<bool>("items.all(v, v > 0) && {\"a\": 1}.size() == 1 && m.f == 1").IsSuccess);

        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        Assert.Null(typeof(CelExprNode).GetField("_children", Private)!.GetValue(tree.Root));
        Assert.Null(typeof(CelParsedExpr).GetField("_index", Private)!.GetValue(tree));
    }

    // The chain of count negations of a boolean constant: a tree of count + 1 nodes in a line.
    private static CelParsedExpr Nested(int count)
    {
        var f = new CelExprFactory();
        CelExprNode node = f.Constant(CelValue.FromBoolean(true));
        for (int i = 0; i < count; i++)
        {
            node = f.Call("!_", node);
        }

        return f.Build(node);
    }

    private static CelBindings Bindings() => new CelBindings()
        .Add("x", 1L)
        .Add("a", 1L)
        .Add("b", 2L)
        .Add("c", 3L)
        .Add("items", CelValue.FromList(CelList.Create(CelValue.FromInt64(1), CelValue.FromInt64(2), CelValue.FromInt64(3))))
        .Add("name", "n")
        .Add("s", "sa")
        .Add("d", CelValue.FromInt64(5))
        .Add("m", CelValue.FromMap(CelMap.Create([KeyValuePair.Create(CelValue.FromString("f"), CelValue.FromInt64(1))])));

    // The code, the stage and the place of each error: what the text and the tree must report alike.
    private static IEnumerable<(CelErrorCode Code, CelErrorStage Stage, int Offset, int Length)> Errors(IEnumerable<CelError> errors) =>
        errors.Select(error => (error.Code, error.Stage, error.Location.Offset, error.Span.Length));
}
