using System;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Examples.Evaluation.PartialTestSupport;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// EvaluatePartial: the result of a program compiled with PartialEvaluation — the value, the errors or the unknown parts —, the
// arguments, the cost, and the place of the unknown parts in the tree of the program.
public sealed class PartialEvaluationTests
{
    [Fact]
    [Trait("Requirement", "F-22")]
    public void ProgramCompiledWithoutTheFlag_RefusesPartialEvaluation()
    {
        CelProgram<bool> program = Env.Compile<bool>("x > 0").GetProgramOrThrow();

        _ = Assert.Throws<InvalidOperationException>(() => program.EvaluatePartial(Bindings(), [], null, CancellationToken.None));
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void NullArguments_AreRefused()
    {
        CelProgram<bool> program = Env.Compile<bool>("x > 0", Partial).GetProgramOrThrow();
        CelBindings bindings = Bindings();
        CelAttributePattern[] withNull = new CelAttributePattern[1];

        _ = Assert.Throws<ArgumentNullException>(() => program.EvaluatePartial(null!, [], null, CancellationToken.None));
        _ = Assert.Throws<ArgumentNullException>(() => program.EvaluatePartial(bindings, null!, null, CancellationToken.None));
        _ = Assert.Throws<ArgumentNullException>(() => program.EvaluatePartial(bindings, withNull, null, CancellationToken.None));
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void NoPatterns_GiveTheValue()
    {
        CelProgram<bool> program = Env.Compile<bool>("x > 0", Partial).GetProgramOrThrow();

        CelPartialEvalResult<bool> result = program.EvaluatePartial(Bindings(), [], null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.True(result.GetValueOrThrow());
        Assert.False(result.IsUnknown);
        Assert.Empty(result.Unknowns);
        Assert.Empty(result.Errors);
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void UnknownResult_HasNoValue()
    {
        CelProgram<bool> program = Env.Compile<bool>("x > 0", Partial).GetProgramOrThrow();

        CelPartialEvalResult<bool> result = program.EvaluatePartial(Bindings(), [P("x")], null, CancellationToken.None);

        Assert.True(result.IsUnknown);
        Assert.False(result.IsSuccess);
        Assert.Empty(result.Errors);
        _ = Assert.Throws<InvalidOperationException>(() => { _ = result.Value; });
        _ = Assert.Throws<InvalidOperationException>(() => result.GetValueOrThrow());
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void ErrorResult_HasTheErrorAndNoUnknowns()
    {
        CelProgram<long> program = Env.Compile<long>("1 / x", Partial).GetProgramOrThrow();
        CelBindings bindings = new CelBindings().Add("x", 0L);

        CelPartialEvalResult<long> result = program.EvaluatePartial(bindings, [], null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsUnknown);
        Assert.Equal(CelErrorCode.DivisionByZero, Assert.Single(result.Errors).Code);
        _ = Assert.Throws<CelException>(() => { _ = result.Value; });
        _ = Assert.Throws<CelException>(() => result.GetValueOrThrow());
        _ = Assert.Throws<CelException>(() => result.GetResidual());
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void Cost_IsReportedOnlyWithALimit()
    {
        CelProgram<bool> program = Env.Compile<bool>("x > 0", Partial).GetProgramOrThrow();
        var options = new CelEvalOptions { CostLimit = 1_000 };

        CelPartialEvalResult<bool> limited = program.EvaluatePartial(Bindings(), [P("x")], options, CancellationToken.None);
        CelPartialEvalResult<bool> unlimited = program.EvaluatePartial(Bindings(), [P("x")], null, CancellationToken.None);

        Assert.NotNull(limited.Cost);
        Assert.Null(unlimited.Cost);
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void Evaluate_OfAPartialProgram_GivesTheValueOfAProgramWithoutTheFlag()
    {
        CelEvalResult<bool> partial = Env.Compile<bool>("x > 0", Partial).GetProgramOrThrow().Evaluate(Bindings(), cancellationToken: CancellationToken.None);
        CelEvalResult<bool> plain = Env.Compile<bool>("x > 0").GetProgramOrThrow().Evaluate(Bindings(), cancellationToken: CancellationToken.None);

        Assert.True(partial.GetValueOrThrow());
        Assert.Equal(plain.GetValueOrThrow(), partial.GetValueOrThrow());
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void CompiledTree_GivesTheIdsOfItsOwnNodes()
    {
        var factory = new CelExprFactory();
        CelIdentNode x = factory.Ident("x");
        CelParsedExpr tree = factory.Build(factory.Call("_>_", x, factory.Constant(CelValue.FromInt64(0))));
        CelProgram<bool> program = Env.CompileParsed<bool>(tree, Partial).GetProgramOrThrow();

        CelPartialEvalResult<bool> result = program.EvaluatePartial(Bindings(), [P("x")], null, CancellationToken.None);

        Assert.Equal(x.Id, Assert.Single(result.Unknowns).ExprId);
    }

    // The residual of a program compiled from a tree of the factory is built from the ids of that tree, not of a parse of a text.
    [Fact]
    [Trait("Requirement", "F-22")]
    public void CompiledTree_GivesTheResidualOfTheSameTextCompiled()
    {
        var factory = new CelExprFactory();
        CelCallNode decided = factory.Call("_>_", factory.Ident("x"), factory.Constant(CelValue.FromInt64(0)));
        CelCallNode open = factory.Call("_>_", factory.Ident("y"), factory.Constant(CelValue.FromInt64(1)));
        CelParsedExpr tree = factory.Build(factory.Call("_&&_", decided, open));

        CelPartialEvalResult<bool> fromTree = Env.CompileParsed<bool>(tree, Partial).GetProgramOrThrow().EvaluatePartial(Bindings(), [P("y")], null, CancellationToken.None);
        CelPartialEvalResult<bool> fromText = Env.Compile<bool>("x > 0 && y > 1", Partial).GetProgramOrThrow().EvaluatePartial(Bindings(), [P("y")], null, CancellationToken.None);

        Assert.Equal("y > 1", CelSyntax.Format(fromTree.GetResidual()));
        Assert.Equal(CelSyntax.Format(fromText.GetResidual()), CelSyntax.Format(fromTree.GetResidual()));
    }

    // The residual of a program compiled from a tree that was loaded from the protobuf bytes of a parse is that of the text compiled.
    [Fact]
    [Trait("Requirement", "F-22")]
    public void CompiledTreeFromProtobuf_GivesTheResidualOfTheSameTextCompiled()
    {
        const string Text = "x > 0 && y > 1";
        byte[] bytes = Env.Parse(Text).GetParsedExprOrThrow().ToProtobuf();
        CelParsedExpr loaded = CelParsedExpr.FromProtobuf(bytes, Text).GetParsedExprOrThrow();

        CelPartialEvalResult<bool> fromBytes = Env.CompileParsed<bool>(loaded, Partial).GetProgramOrThrow().EvaluatePartial(Bindings(), [P("y")], null, CancellationToken.None);
        CelPartialEvalResult<bool> fromText = Env.Compile<bool>(Text, Partial).GetProgramOrThrow().EvaluatePartial(Bindings(), [P("y")], null, CancellationToken.None);

        Assert.Equal("y > 1", CelSyntax.Format(fromBytes.GetResidual()));
        Assert.Equal(CelSyntax.Format(fromText.GetResidual()), CelSyntax.Format(fromBytes.GetResidual()));
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void CompiledText_GivesTheIdsOfTheParsedExpression()
    {
        const string Text = "y > x";
        var root = Assert.IsType<CelCallNode>(Env.Parse(Text).GetParsedExprOrThrow().Root);
        CelProgram<bool> program = Env.Compile<bool>(Text, Partial).GetProgramOrThrow();

        CelPartialEvalResult<bool> result = program.EvaluatePartial(Bindings(), [P("x")], null, CancellationToken.None);

        CelUnknown unknown = Assert.Single(result.Unknowns);
        Assert.Equal(root.Arguments[1].Id, unknown.ExprId);
        Assert.Equal(4, unknown.Span.Start.Offset);
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void CompilationCache_KeepsTheFlagInTheKey()
    {
        var cache = new CelCompilationCache(Env);

        CelProgram<bool> partial = cache.GetOrCompile<bool>("x > 0", Partial).GetProgramOrThrow();
        CelProgram<bool> plain = cache.GetOrCompile<bool>("x > 0").GetProgramOrThrow();

        Assert.NotSame(partial, plain);
        Assert.Equal(2, cache.Count);
        Assert.Same(partial, cache.GetOrCompile<bool>("x > 0", Partial).GetProgramOrThrow());
        Assert.True(partial.EvaluatePartial(Bindings(), [P("x")], null, CancellationToken.None).IsUnknown);
        _ = Assert.Throws<InvalidOperationException>(() => plain.EvaluatePartial(Bindings(), [], null, CancellationToken.None));
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void PatternWithAField_MatchesTheSelectionOfThatField()
    {
        CelProgram<bool> program = Env.Compile<bool>("resource.owner == request.user", Partial).GetProgramOrThrow();
        CelBindings bindings = new CelBindings()
            .Add("request", Map(("user", Str("alice"))))
            .Add("resource", Map(("owner", Str("alice"))));

        CelPartialEvalResult<bool> unknown = program.EvaluatePartial(bindings, [P("resource", "owner")], null, CancellationToken.None);
        CelPartialEvalResult<bool> known = program.EvaluatePartial(bindings, [P("resource", "other")], null, CancellationToken.None);

        Assert.Equal("resource.owner", string.Join(",", unknown.Unknowns.Select(item => item.Attribute!.ToString())));
        Assert.True(known.Value);
    }
}
