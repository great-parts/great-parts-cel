using System;
using System.Collections.Generic;
using System.Text;
using Cel.Expr;
using Great.Parts.Cel.Eng.Difftest.Compare;
using Great.Parts.Cel.Eng.Difftest.Corpus;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;
using static Great.Parts.Cel.Eng.Tests.Difftest.SignatureTests;

namespace Great.Parts.Cel.Eng.Tests.Difftest;

// The two comparisons of the exchange of a checked expression (docs/design/ast-protobuf.md, "Exchange with cel-go"), with fake sides:
// the bytes of a fake are the text of the expression, so a test can tell which text a side was asked to evaluate.
public sealed class ExchangeTests
{
    private static readonly Dictionary<string, Value> NoBindings = [];

    // ---- The exchange of one expression ----

    [Fact]
    public void TextThatDoesNotCompileOnTheEvaluator_IsNoCaseOfTheExchange()
    {
        SideOutcome refused = SideOutcome.CompileError(CompileStage.Check, "found no matching overload");
        FakeChecked maker = new(Written, (_, _) => Agreed(1));
        FakeChecked evaluator = new(Written, (_, _) => Agreed(2));
        ExchangedSide exchange = new(maker, evaluator, new FakeSide((_, _) => refused));

        SideOutcome outcome = exchange.Evaluate("i1 + b1", NoBindings);

        Assert.Same(refused, outcome);
        Assert.Empty(maker.Written);
    }

    [Fact]
    public void TextThatDoesNotCompileOnTheMaker_GivesTheReferenceOutcome()
    {
        SideOutcome reference = SideOutcome.Of(Int(7));
        FakeChecked maker = new(_ => CheckedAttempt.NotCompiled, (_, _) => Agreed(1));
        FakeChecked evaluator = new(Written, (_, _) => Agreed(2));
        ExchangedSide exchange = new(maker, evaluator, new FakeSide((_, _) => reference));

        SideOutcome outcome = exchange.Evaluate("i1", NoBindings);

        Assert.Same(reference, outcome);
        Assert.Empty(evaluator.Evaluated);
    }

    [Fact]
    public void MakerThatFails_GivesItsFailureAsTheOutcome()
    {
        SideOutcome failure = SideOutcome.CompileError(CompileStage.Check, "the library threw InvalidOperationException: too deep");
        FakeChecked maker = new(_ => CheckedAttempt.Failed(failure), (_, _) => Agreed(1));
        FakeChecked evaluator = new(Written, (_, _) => Agreed(2));
        ExchangedSide exchange = new(maker, evaluator, new FakeSide((_, _) => SideOutcome.Of(Int(7))));

        SideOutcome outcome = exchange.Evaluate("i1", NoBindings);

        Assert.Same(failure, outcome);
        Assert.Empty(evaluator.Evaluated);
    }

    [Fact]
    public void Bytes_AreEvaluatedByTheEvaluatorWithTheBindings()
    {
        Dictionary<string, Value> bindings = new() { ["i1"] = Int(2) };
        IReadOnlyDictionary<string, Value>? seen = null;
        FakeChecked maker = new(Written, (_, _) => Agreed(1));
        FakeChecked evaluator = new(
            Written,
            (bytes, given) =>
            {
                seen = given;
                return SideOutcome.Of(Str(Encoding.UTF8.GetString(bytes)));
            });
        ExchangedSide exchange = new(maker, evaluator, new FakeSide((_, _) => SideOutcome.Of(Int(7))));

        SideOutcome outcome = exchange.Evaluate("i1 + 5", bindings);

        Assert.Equal(Str("i1 + 5"), outcome.Result);
        Assert.Same(bindings, seen);
        Assert.Equal(["i1 + 5"], maker.Written);
    }

    // ---- The search ----

    [Fact]
    public void AnotherResultFromTheBytesOfTheLibrary_IsADivergenceOfItsOwnSignature()
    {
        Expr root = new Call(ProfileTables.Add, [new IntLiteral(40, IntegerForm.DecimalDigits), I1]);
        string rootText = ExprPrinter.PrintCase(root);
        (FakeSide library, FakeSide oracle) = Sides(
            (root, SideOutcome.Of(Int(42)), SideOutcome.Of(Int(42))),
            (I1, SideOutcome.Of(Int(2)), SideOutcome.Of(Int(2))));
        FakeChecked libraryChecked = new(Written, (bytes, given) => library.Evaluate(Text(bytes), given));
        FakeChecked oracleChecked = new(
            Written,
            (bytes, given) => Text(bytes) == rootText ? SideOutcome.Of(Int(43)) : oracle.Evaluate(Text(bytes), given));
        DivergenceSearch search = Search(library, oracle, libraryChecked, oracleChecked, KnownSignatures.Of());

        Divergence? divergence = search.Check(RunSeed, CaseNumber, Case(root, ("i1", Int(2))));
        IReadOnlyList<Finding> findings = PendingFile.Findings(search.NewSignatures, PendingFile.Build(search.NewSignatures));

        Assert.Equal("checked-to-cel-go: add_int64(int, int) value-mismatch", divergence?.Signature);
        FindingAssert.Exactly(
            findings,
            FindingAssert.Error(
                EngCodes.Eng820,
                PendingFile.Path,
                6,
                3,
                "new divergence signature 'checked-to-cel-go: add_int64(int, int) value-mismatch' (S1): cel-go from the text returns int 42, cel-go from our bytes returns int 43; seed 20261104, case 1207"));
    }

    [Fact]
    public void AnotherResultFromTheBytesOfCelGo_IsADivergenceOfItsOwnSignature()
    {
        Expr root = new Call(ProfileTables.Divide, [I1, new IntLiteral(2, IntegerForm.DecimalDigits)]);
        string rootText = ExprPrinter.PrintCase(root);
        (FakeSide library, FakeSide oracle) = Sides(
            (root, SideOutcome.Of(Int(1)), SideOutcome.Of(Int(1))),
            (I1, SideOutcome.Of(Int(2)), SideOutcome.Of(Int(2))));
        FakeChecked libraryChecked = new(
            Written,
            (bytes, given) => Text(bytes) == rootText ? SideOutcome.EvaluationError("no such overload") : library.Evaluate(Text(bytes), given));
        FakeChecked oracleChecked = new(Written, (bytes, given) => oracle.Evaluate(Text(bytes), given));
        DivergenceSearch search = Search(library, oracle, libraryChecked, oracleChecked, KnownSignatures.Of());

        Divergence? divergence = search.Check(RunSeed, CaseNumber, Case(root, ("i1", Int(2))));
        IReadOnlyList<Finding> findings = PendingFile.Findings(search.NewSignatures, PendingFile.Build(search.NewSignatures));

        Assert.Equal("checked-from-cel-go: divide_int64(int, int) we-error", divergence?.Signature);
        FindingAssert.Exactly(
            findings,
            FindingAssert.Error(
                EngCodes.Eng820,
                PendingFile.Path,
                6,
                3,
                "new divergence signature 'checked-from-cel-go: divide_int64(int, int) we-error' (S2): the library from the text returns int 1, the library from the bytes of cel-go returns error 'no such overload'; seed 20261104, case 1207"));
    }

    [Fact]
    public void ExchangeOfAKnownSignature_IsKnownAndWritesNoCase()
    {
        Expr root = new Call(ProfileTables.Add, [new IntLiteral(40, IntegerForm.DecimalDigits), I1]);
        string rootText = ExprPrinter.PrintCase(root);
        (FakeSide library, FakeSide oracle) = Sides(
            (root, SideOutcome.Of(Int(42)), SideOutcome.Of(Int(42))),
            (I1, SideOutcome.Of(Int(2)), SideOutcome.Of(Int(2))));
        FakeChecked libraryChecked = new(Written, (bytes, given) => library.Evaluate(Text(bytes), given));
        FakeChecked oracleChecked = new(
            Written,
            (bytes, given) => Text(bytes) == rootText ? SideOutcome.Of(Int(43)) : oracle.Evaluate(Text(bytes), given));
        DivergenceSearch search = Search(
            library,
            oracle,
            libraryChecked,
            oracleChecked,
            KnownSignatures.Of("checked-to-cel-go: add_int64(int, int) value-mismatch"));

        search.Check(RunSeed, CaseNumber, Case(root, ("i1", Int(2))));

        Assert.Empty(search.NewSignatures);
        Assert.Equal(["checked-to-cel-go: add_int64(int, int) value-mismatch"], search.KnownSignaturesFound);
    }

    [Fact]
    public void SidesThatDivergeOnTheText_AreNotRunThroughTheExchanges()
    {
        Expr root = new Call(ProfileTables.Add, [new IntLiteral(40, IntegerForm.DecimalDigits), I1]);
        (FakeSide library, FakeSide oracle) = Sides(
            (root, SideOutcome.Of(Int(43)), SideOutcome.Of(Int(42))),
            (I1, SideOutcome.Of(Int(2)), SideOutcome.Of(Int(2))));
        FakeChecked libraryChecked = new(Written, (bytes, given) => library.Evaluate(Text(bytes), given));
        FakeChecked oracleChecked = new(Written, (bytes, given) => oracle.Evaluate(Text(bytes), given));
        DivergenceSearch search = Search(library, oracle, libraryChecked, oracleChecked, KnownSignatures.Of());

        Divergence? divergence = search.Check(RunSeed, CaseNumber, Case(root, ("i1", Int(2))));

        Assert.Equal("add_int64(int, int) value-mismatch", divergence?.Signature);
        Assert.Empty(libraryChecked.Written);
        Assert.Empty(oracleChecked.Written);
    }

    [Fact]
    public void AgreeingExchanges_GiveNoDivergence()
    {
        Expr root = new Call(ProfileTables.Add, [new IntLiteral(40, IntegerForm.DecimalDigits), I1]);
        (FakeSide library, FakeSide oracle) = Sides(
            (root, SideOutcome.Of(Int(42)), SideOutcome.Of(Int(42))),
            (I1, SideOutcome.Of(Int(2)), SideOutcome.Of(Int(2))));
        FakeChecked libraryChecked = new(Written, (bytes, given) => library.Evaluate(Text(bytes), given));
        FakeChecked oracleChecked = new(Written, (bytes, given) => oracle.Evaluate(Text(bytes), given));
        DivergenceSearch search = Search(library, oracle, libraryChecked, oracleChecked, KnownSignatures.Of());

        Divergence? divergence = search.Check(RunSeed, CaseNumber, Case(root, ("i1", Int(2))));

        Assert.Null(divergence);
        Assert.Empty(search.NewSignatures);
        Assert.Equal([ExprPrinter.PrintCase(root)], libraryChecked.Written);
        Assert.Equal([ExprPrinter.PrintCase(root)], oracleChecked.Written);
    }

    [Fact]
    public void LoadStage_IsNamedInTheOutcome()
    {
        Assert.Equal("load error 'not a CheckedExpr'", SideOutcome.CompileError(CompileStage.Load, "not a CheckedExpr").Describe());
    }

    // ---- Fakes ----

    private static DivergenceSearch Search(FakeSide library, FakeSide oracle, FakeChecked libraryChecked, FakeChecked oracleChecked, KnownSignatures known)
    {
        DivergenceSearch search = new(library, oracle, new FakeRecorder(), known);
        search.AddExchanges(ExchangeChannel.Both(new ExchangeParty(library, libraryChecked), new ExchangeParty(oracle, oracleChecked)));
        return search;
    }

    private static CheckedAttempt Written(string expression) => CheckedAttempt.Made(Encoding.UTF8.GetBytes(expression));

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static SideOutcome Agreed(long value) => SideOutcome.Of(Int(value));

    // A side that writes the text as its bytes, and evaluates bytes as the test says; it keeps what it was asked.
    private sealed class FakeChecked(
        Func<string, CheckedAttempt> check,
        Func<byte[], IReadOnlyDictionary<string, Value>, SideOutcome> evaluate) : ICheckedSide
    {
        public List<string> Written { get; } = [];

        public List<string> Evaluated { get; } = [];

        public CheckedAttempt Check(string expression)
        {
            Written.Add(expression);
            return check(expression);
        }

        public SideOutcome EvaluateChecked(byte[] checkedExpr, IReadOnlyDictionary<string, Value> bindings)
        {
            Evaluated.Add(Text(checkedExpr));
            return evaluate(checkedExpr, bindings);
        }
    }
}
