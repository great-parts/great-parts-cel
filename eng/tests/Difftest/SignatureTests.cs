using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Cel.Expr;
using Google.Protobuf;
using NullValue = Google.Protobuf.WellKnownTypes.NullValue;
using Great.Parts.Cel.Eng.Difftest.Compare;
using Great.Parts.Cel.Eng.Difftest.Corpus;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Difftest;

// Outcomes, signatures, minimization and pending.textproto of eng/difftest (docs/design/eng-difftest.md, "Outcomes and
// signatures", "Known signatures and the corpus", "Findings and output"), with fake sides that give fixed outcomes per
// expression text.
public sealed class SignatureTests
{
    private const ulong RunSeed = 20261104;
    private const ulong CaseNumber = 1207;

    // The outcome of both fake sides for an expression that a test does not name.
    private static readonly SideOutcome Agreed = SideOutcome.Of(Int(0));

    private static readonly Expr I1 = new Ident("i1");

    // ---- ENG820 and the kinds ----

    [Fact]
    public void AnotherValueFromTheOracle_GivesEng820WithTheSignatureAtTheTestTokenOfItsCase()
    {
        Expr root = new Call(ProfileTables.Add, [new IntLiteral(40, IntegerForm.DecimalDigits), I1]);
        (FakeSide library, FakeSide oracle) = Sides(
            (root, SideOutcome.Of(Int(43)), SideOutcome.Of(Int(42))),
            (I1, SideOutcome.Of(Int(2)), SideOutcome.Of(Int(2))));

        IReadOnlyList<Finding> findings = Run(library, oracle, KnownSignatures.Of(), Case(root, ("i1", Int(2))));

        FindingAssert.Exactly(
            findings,
            FindingAssert.Error(
                EngCodes.Eng820,
                PendingFile.Path,
                6,
                3,
                "new divergence signature 'add_int64(int, int) value-mismatch' (S1): cel-go returns int 42, we return int 43; seed 20261104, case 1207"));
    }

    [Theory]
    [InlineData("value-mismatch", "add_int64(int, int) value-mismatch' (S1): cel-go returns int 42, we return int 43")]
    [InlineData("we-value", "divide_int64(int, int) we-value' (S1): cel-go returns error 'division by zero', we return int 0")]
    [InlineData("we-error", "logical_or(bool, bool) we-error' (S2): cel-go returns bool true, we return error 'no such overload'")]
    [InlineData("stage-mismatch", "negate_int64(int) stage-mismatch' (S2): cel-go returns error 'integer overflow', we return check error 'found no matching overload'")]
    public static void OneCasePerKind_GivesItsSignatureAndSeverity(string kind, string expected)
    {
        Expr b1 = new Ident("b1");
        Expr b2 = new Ident("b2");
        Expr zero = new IntLiteral(0, IntegerForm.DecimalDigits);
        Expr forty = new IntLiteral(40, IntegerForm.DecimalDigits);
        (Expr root, SideOutcome library, SideOutcome oracle) = kind switch
        {
            "value-mismatch" => (new Call(ProfileTables.Add, [forty, I1]), SideOutcome.Of(Int(43)), SideOutcome.Of(Int(42))),
            "we-value" => (new Call(ProfileTables.Divide, [I1, zero]), SideOutcome.Of(Int(0)), SideOutcome.EvaluationError("division by zero")),
            "we-error" => (new Call(ProfileTables.LogicalOr, [b1, b2]), SideOutcome.EvaluationError("no such overload"), SideOutcome.Of(Bool(true))),
            _ => (new Call(ProfileTables.Negate, [I1]), SideOutcome.CompileError(CompileStage.Check, "found no matching overload\n | -i1\n | ^"), SideOutcome.EvaluationError("integer overflow")),
        };
        (FakeSide librarySide, FakeSide oracleSide) = Sides(
            (root, library, oracle),
            (I1, SideOutcome.Of(Int(2)), SideOutcome.Of(Int(2))),
            (zero, SideOutcome.Of(Int(0)), SideOutcome.Of(Int(0))),
            (forty, SideOutcome.Of(Int(40)), SideOutcome.Of(Int(40))),
            (b1, SideOutcome.Of(Bool(true)), SideOutcome.Of(Bool(true))),
            (b2, SideOutcome.Of(Bool(false)), SideOutcome.Of(Bool(false))));
        DivergenceSearch search = new(librarySide, oracleSide, new FakeRecorder(), KnownSignatures.Of());

        Divergence? divergence = search.Check(RunSeed, CaseNumber, Case(root, ("i1", Int(2)), ("b1", Bool(true)), ("b2", Bool(false))));
        IReadOnlyList<Finding> findings = PendingFile.Findings(search.NewSignatures, PendingFile.Build(search.NewSignatures));

        Assert.Equal(kind, divergence?.Kind.Name());
        FindingAssert.Exactly(
            findings,
            FindingAssert.Error(EngCodes.Eng820, PendingFile.Path, 6, 3, $"new divergence signature '{expected}; seed 20261104, case 1207"));
    }

    [Fact]
    public void Outcomes_AreComparedByClassAndValue()
    {
        Value nan = new() { DoubleValue = double.NaN };
        Value list = new() { ListValue = new ListValue { Values = { Int(1), Int(2) } } };
        Value reversed = new() { ListValue = new ListValue { Values = { Int(2), Int(1) } } };
        Value map = Map((Bool(true), Int(1)), (Str("a"), Int(2)));
        Value reorderedMap = Map((Str("a"), Int(2)), (Bool(true), Int(1)));

        Assert.Null(DivergenceKinds.Compare(SideOutcome.Of(nan), SideOutcome.Of(nan)));
        Assert.Null(DivergenceKinds.Compare(SideOutcome.Of(new Value { DoubleValue = 0.0 }), SideOutcome.Of(new Value { DoubleValue = -0.0 })));
        Assert.Null(DivergenceKinds.Compare(SideOutcome.Of(map), SideOutcome.Of(reorderedMap)));
        Assert.Null(DivergenceKinds.Compare(SideOutcome.EvaluationError("a"), SideOutcome.EvaluationError("b")));
        Assert.Null(DivergenceKinds.Compare(SideOutcome.CompileError(CompileStage.Parse, "a"), SideOutcome.CompileError(CompileStage.Program, "b")));
        Assert.Equal(DivergenceKind.ValueMismatch, DivergenceKinds.Compare(SideOutcome.Of(Int(1)), SideOutcome.Of(new Value { Uint64Value = 1 })));
        Assert.Equal(DivergenceKind.ValueMismatch, DivergenceKinds.Compare(SideOutcome.Of(list), SideOutcome.Of(reversed)));
        Assert.Equal(DivergenceKind.ValueMismatch, DivergenceKinds.Compare(SideOutcome.Of(map), SideOutcome.Of(Map((Bool(true), Int(1))))));
        Assert.Equal(DivergenceKind.StageMismatch, DivergenceKinds.Compare(SideOutcome.EvaluationError("a"), SideOutcome.CompileError(CompileStage.Program, "b")));
        Assert.Equal([DivergenceSeverity.S1, DivergenceSeverity.S1, DivergenceSeverity.S2, DivergenceSeverity.S2], Enum.GetValues<DivergenceKind>().Select(kind => kind.Severity()));
    }

    [Fact]
    public void SignatureOfEachRoot_HasTheFormOfTheRegistry()
    {
        Expr one = new IntLiteral(1, IntegerForm.DecimalDigits);
        Expr s1 = new Ident("s1");
        Expr x1 = new Ident("x1");
        Expr yes = new BoolLiteral(true);
        Expr k = new StringLiteral("k", new TextForm(Quote.Apostrophe, null, [Escape.Literal]));
        Expr divideByZero = new Call(ProfileTables.Divide, [one, new IntLiteral(0, IntegerForm.DecimalDigits)]);
        Expr list = new ListLiteral([one], TrailingComma: false);
        Expr map = new MapLiteral([new MapEntry(k, one)], TrailingComma: false);
        Expr half = new DoubleLiteral(0.5, DoubleForm.Plain);
        SideOutcome we = SideOutcome.Of(Int(7));
        SideOutcome go = SideOutcome.EvaluationError("no such overload");
        (Expr Root, string Signature)[] roots =
        [
            (new ListLiteral([one, s1], TrailingComma: false), "list(int, string) we-value"),
            (new MapLiteral([new MapEntry(yes, one), new MapEntry(k, s1)], TrailingComma: false), "map(bool, int, string, string) we-value"),
            (new UintLiteral(7, IntegerForm.DecimalDigits, 'u'), "literal(uint) we-value"),
            (new NullLiteral(), "literal(null_type) we-value"),
            (x1, "ident(string) we-value"),
            (new Ident("x2"), "ident(error) we-value"),
            (new Call(ProfileTables.Add, [x1, one]), "_+_(string, int) we-value"),
            (new Call(ProfileTables.Add, [divideByZero, one]), "_+_(error, int) we-value"),
            (new Select(x1, "a"), "select(string) we-value"),
            (new PresenceTest(map, "a"), "has(map) we-value"),
            (new Comprehension(ProfileTables.All, list, "v", [yes]), "comprehension(list) we-value"),
            (new Call(ProfileTables.Size, [s1]) { Receiver = true }, "string_size(string) we-value"),
            (new Call(ProfileTables.Size, [s1]), "size_string(string) we-value"),
            (new Call(ProfileTables.Equal, [one, x1]), "equals(int, string) we-value"),
            (new Call(ProfileTables.Index, [list, one]), "index_list(list, int) we-value"),
            (new Call(ProfileTables.Less, [one, half]), "less_int64_double(int, double) we-value"),
            (new Call(ProfileTables.Conditional, [yes, one, s1]), "conditional(bool, int, string) we-value"),
            (new Call(ProfileTables.In, [divideByZero, list]), "@in(error, list) we-value"),
            (new Ident("int"), "ident(type) we-value"),
        ];
        (Expr, SideOutcome, SideOutcome)[] children =
        [
            (one, SideOutcome.Of(Int(1)), SideOutcome.Of(Int(1))),
            (yes, SideOutcome.Of(Bool(true)), SideOutcome.Of(Bool(true))),
            (k, SideOutcome.Of(Str("k")), SideOutcome.Of(Str("k"))),
            (s1, SideOutcome.Of(Str("a")), SideOutcome.Of(Str("a"))),
            (x1, SideOutcome.Of(Str("b")), SideOutcome.Of(Str("b"))),
            (divideByZero, SideOutcome.EvaluationError("division by zero"), SideOutcome.EvaluationError("division by zero")),
            (list, SideOutcome.Of(ListOf(Int(1))), SideOutcome.Of(ListOf(Int(1)))),
            (map, SideOutcome.Of(Map((Str("k"), Int(1)))), SideOutcome.Of(Map((Str("k"), Int(1))))),
            (half, SideOutcome.Of(new Value { DoubleValue = 0.5 }), SideOutcome.Of(new Value { DoubleValue = 0.5 })),
        ];

        foreach ((Expr root, string signature) in roots)
        {
            // The root diverges; its children agree, so the case is minimal.
            (FakeSide library, FakeSide oracle) = root == x1
                ? Sides((x1, we, go))
                : Sides([(root, we, go), .. children]);
            DivergenceSearch search = new(library, oracle, new FakeRecorder(), KnownSignatures.Of());

            Divergence? divergence = search.Check(RunSeed, CaseNumber, Case(root, ("s1", Str("a")), ("x1", Str("b"))));

            Assert.Equal(signature, divergence?.Signature);
        }
    }

    [Fact]
    public void SideThatFailsToParse_GivesTheNodeParseWithoutTypes()
    {
        Expr root = new Call(ProfileTables.Add, [I1, I1]);
        (FakeSide library, FakeSide oracle) = Sides(
            (root, SideOutcome.CompileError(CompileStage.Parse, "Syntax error: mismatched input"), SideOutcome.Of(Int(4))),
            (I1, SideOutcome.Of(Int(2)), SideOutcome.Of(Int(2))));
        DivergenceSearch search = new(library, oracle, new FakeRecorder(), KnownSignatures.Of());

        Divergence? divergence = search.Check(RunSeed, CaseNumber, Case(root, ("i1", Int(2))));

        Assert.Equal("parse() we-error", divergence?.Signature);
    }

    // ---- Known signatures and agreeing outcomes ----

    [Fact]
    public void KnownSignature_GivesNoFindingAndNoCase()
    {
        Expr root = new Call(ProfileTables.Add, [new IntLiteral(40, IntegerForm.DecimalDigits), I1]);
        (FakeSide library, FakeSide oracle) = Sides(
            (root, SideOutcome.Of(Int(43)), SideOutcome.Of(Int(42))),
            (I1, SideOutcome.Of(Int(2)), SideOutcome.Of(Int(2))));
        FakeRecorder recorder = new();
        DivergenceSearch search = new(library, oracle, recorder, KnownSignatures.Of("add_int64(int, int) value-mismatch"));

        Divergence? divergence = search.Check(RunSeed, CaseNumber, Case(root, ("i1", Int(2))));
        PendingText pending = PendingFile.Build(search.NewSignatures);

        Assert.Equal("add_int64(int, int) value-mismatch", divergence?.Signature);
        Assert.Equal(["add_int64(int, int) value-mismatch"], search.KnownSignaturesFound);
        Assert.Empty(recorder.Names);
        Assert.Equal(PendingFile.EmptyForm, pending.Text);
        FindingAssert.None(PendingFile.Findings(search.NewSignatures, pending));
    }

    // The two known signatures of matches (the records of the registry of the class cel-go-bug): the expressions that the registry
    // names give them through the descent of every other case, so the profile needs no code of its own for them.
    [Theory]
    [InlineData("abc", "(", "check", "evaluation", "matches(string, string) stage-mismatch")]
    [InlineData("(", "a", "value", "compile", "matches(string, string) we-value")]
    public static void MatchesWithAnInvalidLiteral_GivesAKnownSignatureAndNoNewOne(string subject, string pattern, string library, string oracle, string signature)
    {
        Expr subjectLiteral = new StringLiteral(subject, new TextForm(Quote.Apostrophe, null, [.. subject.Select(_ => Escape.Literal)]));
        Expr patternLiteral = new StringLiteral(pattern, new TextForm(Quote.Apostrophe, null, [.. pattern.Select(_ => Escape.Literal)]));
        Expr root = new Call(ProfileTables.Matches, [subjectLiteral, patternLiteral]);
        SideOutcome Outcome(string kind) => kind switch
        {
            "check" => SideOutcome.CompileError(CompileStage.Check, "invalid regular expression at offset 0: missing closing )"),
            "compile" => SideOutcome.CompileError(CompileStage.Program, "invalid regular expression"),
            "evaluation" => SideOutcome.EvaluationError("error parsing regexp: missing closing ): `(`"),
            _ => SideOutcome.Of(Bool(false)),
        };
        (FakeSide librarySide, FakeSide oracleSide) = Sides(
            (root, Outcome(library), Outcome(oracle)),
            (subjectLiteral, SideOutcome.Of(Str(subject)), SideOutcome.Of(Str(subject))),
            (patternLiteral, SideOutcome.Of(Str(pattern)), SideOutcome.Of(Str(pattern))));
        DivergenceSearch search = new(librarySide, oracleSide, new FakeRecorder(), KnownSignatures.Of(signature));

        Divergence? divergence = search.Check(RunSeed, CaseNumber, Case(root));

        Assert.Equal(signature, divergence?.Signature);
        Assert.Equal([signature], search.KnownSignaturesFound);
        Assert.Empty(search.NewSignatures);
        FindingAssert.None(PendingFile.Findings(search.NewSignatures, PendingFile.Build(search.NewSignatures)));
    }

    [Fact]
    public void MatchesFromAReceiverWithAnInvalidLiteral_IsACompileErrorOnBothSidesAndAgrees()
    {
        Expr subject = new StringLiteral("abc", new TextForm(Quote.Apostrophe, null, [Escape.Literal, Escape.Literal, Escape.Literal]));
        Expr pattern = new StringLiteral("(", new TextForm(Quote.Apostrophe, null, [Escape.Literal]));
        Expr root = new Call(ProfileTables.Matches, [subject, pattern]) { Receiver = true };
        SideOutcome refused = SideOutcome.CompileError(CompileStage.Check, "invalid regular expression");
        (FakeSide library, FakeSide oracle) = Sides((root, refused, SideOutcome.CompileError(CompileStage.Program, "invalid regular expression")));
        DivergenceSearch search = new(library, oracle, new FakeRecorder(), KnownSignatures.Of());

        Divergence? divergence = search.Check(RunSeed, CaseNumber, Case(root));

        Assert.Null(divergence);
        Assert.Empty(search.NewSignatures);
        Assert.Equal("'abc'.matches('(')", ExprPrinter.Print(root));
    }

    [Fact]
    public void AgreeingOutcomes_GiveNoDivergence()
    {
        Expr root = new Call(ProfileTables.Add, [I1, I1]);
        (FakeSide library, FakeSide oracle) = Sides((root, SideOutcome.Of(Int(4)), SideOutcome.Of(Int(4))));

        IReadOnlyList<Finding> findings = Run(library, oracle, KnownSignatures.Of(), Case(root, ("i1", Int(2))));

        FindingAssert.None(findings);
    }

    [Fact]
    public void KnownSignatures_AreTheSignatureColumnsOfTheRegistryAndTheCorpusSkipList()
    {
        using TempDirectory root = new();
        root.Write(KnownSignatures.RegistryPath, """
            # Known differences from cel-go

            | Signature | Class | Reference |
            |---|---|---|
            | add_int64(int, int) we-value | cel-go-bug | case |
            """);
        root.Write(KnownSignatures.SkipListPath, """
            # Regression corpus skip list

            | Case | Scope | Signature | Verdict | Severity | Reason | Issue |
            |---|---|---|---|---|---|---|
            | pending/new/1-2 | all | map(int, int, uint, int) we-value | ours | S1 | reason | - |
            """);

        KnownSignatures known = KnownSignatures.Read(root.Path);

        Assert.True(known.Contains("add_int64(int, int) we-value"));
        Assert.True(known.Contains("map(int, int, uint, int) we-value"));
        Assert.False(known.Contains("cel-go-bug"));
        Assert.False(known.Contains("pending/new/1-2"));
        Assert.False(KnownSignatures.Read(RepositoryRoot.Find()).Contains("add_int64(int, int) we-value"));
    }

    // ---- pending.textproto ----

    [Fact]
    public void PendingWithoutCases_IsTheCommittedEmptyFormByteForByte()
    {
        using TempDirectory root = new();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root.Path, PendingFile.Path))!);

        PendingFile.Write(root.Path, PendingFile.Build([]));

        Assert.Equal(
            File.ReadAllBytes(Path.Combine(RepositoryRoot.Find(), PendingFile.Path)),
            File.ReadAllBytes(Path.Combine(root.Path, PendingFile.Path)));
    }

    [Fact]
    public void PendingWithCases_CopiesEachCaseUnchangedAndKeepsTheFirstCaseOfASignature()
    {
        Expr first = new Call(ProfileTables.Add, [I1, I1]);
        Expr second = new Call(ProfileTables.Negate, [I1]);
        Expr third = new Call(ProfileTables.Add, [I1, new IntLiteral(1, IntegerForm.DecimalDigits)]);
        (FakeSide library, FakeSide oracle) = Sides(
            (first, SideOutcome.Of(Int(5)), SideOutcome.Of(Int(4))),
            (second, SideOutcome.EvaluationError("overflow"), SideOutcome.Of(Int(-2))),
            (third, SideOutcome.Of(Int(9)), SideOutcome.Of(Int(3))),
            (I1, SideOutcome.Of(Int(2)), SideOutcome.Of(Int(2))),
            (new IntLiteral(1, IntegerForm.DecimalDigits), SideOutcome.Of(Int(1)), SideOutcome.Of(Int(1))));
        FakeRecorder recorder = new();
        DivergenceSearch search = new(library, oracle, recorder, KnownSignatures.Of());

        _ = search.Check(RunSeed, 3, Case(first, ("i1", Int(2))));
        _ = search.Check(RunSeed, 8, Case(second, ("i1", Int(2))));
        _ = search.Check(RunSeed, 9, Case(third, ("i1", Int(2))));
        PendingText pending = PendingFile.Build(search.NewSignatures);

        Assert.Equal(["20261104-3", "20261104-8"], recorder.Names);
        Assert.Equal(
            PendingFile.EmptyForm
            + "section {\n"
            + "  name: \"new\"\n"
            + "  test {\n"
            + "name:  \"20261104-3\"\ndescription:  \"add_int64(int, int) value-mismatch\"\nexpr:  \"i1 + i1\"\n"
            + "  }\n"
            + "  test {\n"
            + "name:  \"20261104-8\"\ndescription:  \"negate_int64(int) we-error\"\nexpr:  \"-i1\"\n"
            + "  }\n"
            + "}\n",
            pending.Text);
        FindingAssert.Exactly(
            PendingFile.Findings(search.NewSignatures, pending),
            FindingAssert.Error(EngCodes.Eng820, PendingFile.Path, 6, 3, "new divergence signature 'add_int64(int, int) value-mismatch' (S1): cel-go returns int 4, we return int 5; seed 20261104, case 3"),
            FindingAssert.Error(EngCodes.Eng820, PendingFile.Path, 11, 3, "new divergence signature 'negate_int64(int) we-error' (S2): cel-go returns int -2, we return error 'overflow'; seed 20261104, case 8"));
    }

    [Fact]
    public void ValuesInMessages_AreOneLineOfText()
    {
        Value bytes = new() { BytesValue = ByteString.CopyFrom(0x00, 0x41, 0xFF) };
        Value list = new() { ListValue = new ListValue { Values = { new Value { DoubleValue = 1e20 }, new Value { DoubleValue = 2 }, new Value { NullValue = NullValue.NullValue } } } };

        Assert.Equal("string \"a\\\"\\x0a\\x7f\\u20ac\\U0001f600\"", SideOutcome.Of(Str("a\"\n\u007f€😀")).Describe());
        Assert.Equal("bytes b\"\\x00A\\xff\"", SideOutcome.Of(bytes).Describe());
        Assert.Equal("list [1E+20, 2.0, null]", SideOutcome.Of(list).Describe());
        Assert.Equal("map {true: 1u}", SideOutcome.Of(Map((Bool(true), new Value { Uint64Value = 1 }))).Describe());
        Assert.Equal("double NaN", SideOutcome.Of(new Value { DoubleValue = double.NaN }).Describe());
        Assert.Equal("program error 'first'", SideOutcome.CompileError(CompileStage.Program, "first\r\nsecond").Describe());
    }

    // ---- Minimization ----

    [Fact]
    public void Minimization_ShrinksAGeneratedCaseToARootWithoutDivergentChildren()
    {
        // The library diverges on every case that reads the bound variable i1, so the descent ends at i1 itself.
        GeneratedCase original = CaseGenerator.Case(RunSeed, CallReadingI1(RunSeed));
        FakeSide library = new((text, bindings) => SideOutcome.Of(Int(ReadsI1(text, bindings) ? 2 : 1)));
        FakeSide oracle = new((_, _) => SideOutcome.Of(Int(1)));
        DivergenceSearch search = new(library, oracle, new FakeRecorder(), KnownSignatures.Of());

        Divergence? divergence = search.Check(RunSeed, CallReadingI1(RunSeed), original);

        Assert.NotNull(divergence);
        Assert.Equal("i1", ((Ident)divergence.Case.Expression).Name);
        Assert.Equal(Value.KindOneofCase.Int64Value, divergence.Case.Bindings["i1"].KindCase);
        Assert.Equal("ident(int) value-mismatch", divergence.Signature);
        Assert.True(divergence.Case.Text.Length < original.Text.Length);
    }

    // The descent asks only for nodes of the divergent case, with its bindings: it generates no other case.
    [Fact]
    public void Minimization_AsksOnlyForNodesOfTheCaseWithItsBindings()
    {
        GeneratedCase original = CaseGenerator.Case(RunSeed, CallReadingI1(RunSeed));
        HashSet<Expr> nodes = new(Nodes(original.Expression), ReferenceEqualityComparer.Instance);
        List<GeneratedCase> asked = [];

        GeneratedCase minimized = Minimizer.Minimize(original, Verdicts(candidate =>
        {
            asked.Add(candidate);
            return ReadsI1(candidate.Text, candidate.Bindings);
        }));

        Assert.Equal("i1", minimized.Text);
        Assert.NotEmpty(asked);
        Assert.All(asked, candidate => Assert.True(nodes.Contains(candidate.Expression), candidate.Text));
        Assert.All(asked, candidate => Assert.Same(original.Bindings, candidate.Bindings));
        Assert.True(asked.Count < nodes.Count);
    }

    [Fact]
    public void Minimization_MakesADivergentChildOfTheRootTheCase()
    {
        Expr sum = new Call(ProfileTables.Add, [I1, I1]);
        Expr root = new Call(ProfileTables.Negate, [sum]);
        (FakeSide library, FakeSide oracle) = Sides(
            (root, SideOutcome.EvaluationError("overflow"), SideOutcome.Of(Int(-4))),
            (sum, SideOutcome.EvaluationError("overflow"), SideOutcome.Of(Int(4))),
            (I1, SideOutcome.Of(Int(2)), SideOutcome.Of(Int(2))));
        DivergenceSearch search = new(library, oracle, new FakeRecorder(), KnownSignatures.Of());

        Divergence? divergence = search.Check(RunSeed, CaseNumber, Case(root, ("i1", Int(2))));

        Assert.Equal(sum, divergence?.Case.Expression);
        Assert.Equal("add_int64(int, int) we-error", divergence?.Signature);
    }

    [Fact]
    public void Minimization_TakesTheFirstDivergentChild()
    {
        Expr first = new Call(ProfileTables.Add, [I1, I1]);
        Expr second = new Call(ProfileTables.Negate, [I1]);
        Expr root = new Call(ProfileTables.Add, [first, second]);
        GeneratedCase divergent = Case(root, ("i1", Int(2)));

        GeneratedCase minimized = Minimizer.Minimize(divergent, Verdicts(generated => generated.Expression is Call));

        Assert.Same(first, minimized.Expression);
    }

    [Fact]
    public void Minimization_KeepsTheCaseWhenNoChildDiverges()
    {
        Expr root = new Call(ProfileTables.Add, [I1, I1]);
        GeneratedCase divergent = Case(root, ("i1", Int(2)));

        GeneratedCase minimized = Minimizer.Minimize(divergent, Verdicts(generated => generated.Text == "i1 + i1"));

        Assert.Same(divergent, minimized);
    }

    // A side that fails during minimization, such as an oracle that closed the connection, ends it with its exception; the
    // side is not asked again.
    [Fact]
    public void Minimization_ThrowsTheFailureOfASideAndAsksItNoMore()
    {
        GeneratedCase divergent = Case(new Call(ProfileTables.Add, [new Call(ProfileTables.Add, [I1, I1]), I1]), ("i1", Int(2)));
        IOException failure = new("the oracle closed the connection");
        int calls = 0;

        IOException thrown = Assert.Throws<IOException>(() => Minimizer.Minimize(divergent, Verdicts(_ => ++calls == 2 ? throw failure : true)));

        Assert.Same(failure, thrown);
        Assert.Equal(2, calls);
    }

    // ---- Minimization under a child that agrees in an error ----

    // Case 478854 of seed 20261109: the index agrees as an error on both sides (cel-go fails in the value of the entry, the
    // library at the missing key), and || absorbs only the error of the library. The known divergence is in the map literal
    // under the index, and then in x2 in mx under the literal.
    [Fact]
    public void Minimization_UnderAnIndexThatAgreesInError_FindsTheKnownSignature()
    {
        Expr x2 = new Ident("x2");
        Expr mx = new Ident("mx");
        Expr member = new Call(ProfileTables.In, [x2, mx]);
        Expr literal = new MapLiteral([new MapEntry(new StringLiteral("k", new TextForm(Quote.Apostrophe, null, [Escape.Literal])), member)], TrailingComma: false);
        Expr index = new Call(ProfileTables.Index, [literal, new StringLiteral(string.Empty, new TextForm(Quote.Apostrophe, null, []))]);
        Expr root = new Call(ProfileTables.LogicalOr, [index, new BoolLiteral(true)]);
        SideOutcome panic = SideOutcome.EvaluationError("hash of unhashable type types.Bytes");
        (FakeSide library, FakeSide oracle) = Sides(
            (root, SideOutcome.Of(Bool(true)), panic),
            (index, SideOutcome.EvaluationError("no such key"), panic),
            (literal, SideOutcome.Of(Map((Str("k"), Bool(false)))), panic),
            (member, SideOutcome.Of(Bool(false)), panic),
            Agree(x2, Bytes(0xef)),
            Agree(mx, Map()));
        DivergenceSearch search = new(library, oracle, new FakeRecorder(), KnownSignatures.Of("in_map(bytes, map) we-value"));

        Divergence? divergence = search.Check(RunSeed, CaseNumber, Case(root, ("x2", Bytes(0xef))));

        Assert.Equal("x2 in mx", divergence?.Case.Text);
        Assert.Equal("in_map(bytes, map) we-value", divergence?.Signature);
        Assert.Empty(search.NewSignatures);
    }

    // The child agrees in an error and nothing diverges under it: the case stays the root.
    [Fact]
    public void Minimization_UnderAnErrorChildWithNoDivergentNode_KeepsTheRoot()
    {
        Expr child = new Call(ProfileTables.Negate, [I1]);
        Expr root = new Call(ProfileTables.LogicalOr, [child, new BoolLiteral(true)]);
        SideOutcome error = SideOutcome.EvaluationError("integer overflow");
        (FakeSide library, FakeSide oracle) = Sides(
            (root, SideOutcome.Of(Bool(true)), error),
            (child, error, error),
            Agree(I1, Int(2)));
        GeneratedCase divergent = Case(root, ("i1", Int(2)));

        GeneratedCase minimized = Minimizer.Minimize(divergent, Classifier(library, oracle));

        Assert.Same(divergent, minimized);
        Assert.Equal(Texts(child, new BoolLiteral(true), I1), library.Evaluated);
    }

    // A divergent node two levels below the child, under nested nodes that agree in an error, is found.
    [Fact]
    public void Minimization_FindsADivergentNodeUnderNestedErrorNodes()
    {
        Expr deep = new Call(ProfileTables.Add, [I1, new IntLiteral(3, IntegerForm.DecimalDigits)]);
        Expr middle = new Call(ProfileTables.Add, [deep, new IntLiteral(2, IntegerForm.DecimalDigits)]);
        Expr top = new Call(ProfileTables.Add, [middle, new IntLiteral(1, IntegerForm.DecimalDigits)]);
        Expr root = new Call(ProfileTables.LogicalOr, [top, new BoolLiteral(true)]);
        SideOutcome error = SideOutcome.EvaluationError("integer overflow");
        (FakeSide library, FakeSide oracle) = Sides(
            (root, SideOutcome.Of(Bool(true)), error),
            (top, error, error),
            (middle, error, error),
            (deep, SideOutcome.Of(Int(5)), error),
            Agree(I1, Int(2)));

        GeneratedCase minimized = Minimizer.Minimize(Case(root, ("i1", Int(2))), Classifier(library, oracle));

        Assert.Same(deep, minimized.Expression);
    }

    // Of two children that agree in an error the first in the text is searched first, and the search stops at the divergent node
    // under it: nothing under the second is asked.
    [Fact]
    public void Minimization_TakesTheDivergentNodeUnderTheFirstOfTwoErrorChildren()
    {
        Expr firstDeep = new Call(ProfileTables.Negate, [I1]);
        Expr secondDeep = new Call(ProfileTables.Negate, [new Ident("i2")]);
        Expr first = new Call(ProfileTables.Add, [firstDeep, new IntLiteral(1, IntegerForm.DecimalDigits)]);
        Expr second = new Call(ProfileTables.Add, [secondDeep, new IntLiteral(2, IntegerForm.DecimalDigits)]);
        Expr root = new Call(ProfileTables.Equal, [first, second]);
        SideOutcome error = SideOutcome.EvaluationError("integer overflow");
        (FakeSide library, FakeSide oracle) = Sides(
            (first, error, error),
            (second, error, error),
            (firstDeep, SideOutcome.Of(Int(-2)), error),
            (secondDeep, SideOutcome.Of(Int(-3)), error),
            Agree(I1, Int(2)));

        GeneratedCase minimized = Minimizer.Minimize(Case(root), Classifier(library, oracle));

        Assert.Same(firstDeep, minimized.Expression);
        Assert.DoesNotContain(ExprPrinter.Print(secondDeep), library.Evaluated);
        Assert.DoesNotContain(ExprPrinter.Print(secondDeep), oracle.Evaluated);
    }

    // Under a child that agrees in a value nothing is searched, although a node under it diverges.
    [Fact]
    public void Minimization_DoesNotSearchUnderAChildThatAgreesInAValue()
    {
        Expr hidden = new Call(ProfileTables.Negate, [I1]);
        Expr agreeing = new Call(ProfileTables.Add, [hidden, new IntLiteral(1, IntegerForm.DecimalDigits)]);
        Expr root = new Call(ProfileTables.Add, [agreeing, new IntLiteral(2, IntegerForm.DecimalDigits)]);
        (FakeSide library, FakeSide oracle) = Sides(
            (root, SideOutcome.Of(Int(1)), SideOutcome.Of(Int(2))),
            Agree(agreeing, Int(7)),
            (hidden, SideOutcome.Of(Int(-2)), SideOutcome.Of(Int(2))));
        GeneratedCase divergent = Case(root, ("i1", Int(2)));

        GeneratedCase minimized = Minimizer.Minimize(divergent, Classifier(library, oracle));

        Assert.Same(divergent, minimized);
        Assert.DoesNotContain(ExprPrinter.Print(hidden), library.Evaluated);
        Assert.DoesNotContain(ExprPrinter.Print(hidden), oracle.Evaluated);
    }

    // The search goes in the order of the text, depth first, and the sides see every node once: the children of the root, then
    // the children of the first error child with those that agree in an error under it, then the next child of the root.
    [Fact]
    public void Minimization_AsksTheSidesForNoNodeTwice()
    {
        Expr a11 = new Call(ProfileTables.Negate, [new Ident("i2")]);
        Expr a12 = new Call(ProfileTables.Negate, [new Ident("i3")]);
        Expr a1 = new Call(ProfileTables.Add, [a11, a12]);
        Expr a2 = new Call(ProfileTables.Negate, [new Ident("i4")]);
        Expr a = new Call(ProfileTables.Add, [a1, a2]);
        Expr b1 = new Call(ProfileTables.Negate, [I1]);
        Expr b = new Call(ProfileTables.Add, [b1, new IntLiteral(5, IntegerForm.DecimalDigits)]);
        Expr root = new Call(ProfileTables.Equal, [a, b]);
        SideOutcome error = SideOutcome.EvaluationError("integer overflow");
        (FakeSide library, FakeSide oracle) = Sides(
            (root, SideOutcome.Of(Bool(true)), SideOutcome.Of(Bool(false))),
            (a, error, error),
            (b, error, error),
            (a1, error, error),
            Agree(a2, Int(4)),
            Agree(a11, Int(6)),
            Agree(a12, Int(7)),
            (b1, SideOutcome.Of(Int(-2)), error),
            Agree(I1, Int(2)));

        GeneratedCase minimized = Minimizer.Minimize(Case(root), Classifier(library, oracle));

        Assert.Same(b1, minimized.Expression);
        Assert.Equal(Texts(a, b, a1, a2, a11, a12, b1, I1), library.Evaluated);
        Assert.Equal(library.Evaluated, oracle.Evaluated);
    }

    // ---- Minimization under a macro ----

    // Case 526735 of seed 20261108: the divergence _>_(bytes, double) we-error sits in the body of a macro. When the registry
    // knows it, the case is the body with the element in parentheses; when nobody knows it, the case stays the macro, with the
    // signature of the macro.
    [Theory]
    [InlineData(true, "_>_(bytes, double) we-error", "x1 > (d1)", 0)]
    [InlineData(false, "comprehension(list) we-error", "[d1].map(v, x1 > v)", 1)]
    public static void Macro_WithADivergenceInItsBody_IsMinimizedToTheBodyOnlyWhenItsSignatureIsKnown(
        bool registered, string signature, string text, int newSignatures)
    {
        Expr x1 = new Ident("x1");
        Expr d1 = new Ident("d1");
        Expr range = new ListLiteral([d1], TrailingComma: false);
        Expr root = new Comprehension(ProfileTables.MapMacro, range, "v", [new Call(ProfileTables.Greater, [x1, new Ident("v")])]);
        (FakeSide library, FakeSide oracle) = Sides(
            (root, SideOutcome.EvaluationError("no such overload: _>_(bytes, double)"), SideOutcome.Of(ListOf(Bool(false)))),
            (new Call(ProfileTables.Greater, [x1, Parenthesized(d1)]), SideOutcome.EvaluationError("no such overload: _>_(bytes, double)"), SideOutcome.Of(Bool(false))),
            (x1, SideOutcome.Of(Bytes(0x61)), SideOutcome.Of(Bytes(0x61))),
            (Parenthesized(d1), SideOutcome.Of(Dbl(double.NaN)), SideOutcome.Of(Dbl(double.NaN))),
            (range, SideOutcome.Of(ListOf(Dbl(double.NaN))), SideOutcome.Of(ListOf(Dbl(double.NaN)))));
        string[] known = registered ? ["_>_(bytes, double) we-error"] : [];
        DivergenceSearch search = new(library, oracle, new FakeRecorder(), KnownSignatures.Of(known));

        Divergence? divergence = search.Check(RunSeed, CaseNumber, Case(root));

        Assert.Equal(signature, divergence?.Signature);
        Assert.Equal(text, divergence?.Case.Text);
        Assert.Equal(newSignatures, search.NewSignatures.Count);
        Assert.Equal(registered ? 1 : 0, search.KnownSignaturesFound.Count);
    }

    // Case 843970 of seed 20261108: the range is in dyn(), so the element goes in dyn() and keeps the static type of the range.
    [Fact]
    public void Macro_OverDynRange_IsMinimizedToABodyInstanceWithTheElementInDyn()
    {
        Expr x1 = new Ident("x1");
        Expr bytes = new BytesLiteral([0x61], 'b', new TextForm(Quote.Apostrophe, null, [Escape.Literal]));
        Expr range = new Call(ProfileTables.Dyn, [new ListLiteral([x1, I1, bytes], TrailingComma: false)]);
        Expr transform = new IntLiteral(0, IntegerForm.DecimalDigits);
        Expr root = new Comprehension(ProfileTables.MapMacro, range, "v", [new Call(ProfileTables.In, [new Ident("v"), x1]), transform]);
        Expr dynBytes = new Call(ProfileTables.Dyn, [bytes]);
        (FakeSide library, FakeSide oracle) = Sides(
            (root, SideOutcome.Of(ListOf()), SideOutcome.EvaluationError("hash of unhashable type types.Bytes")),
            (new Call(ProfileTables.In, [dynBytes, x1]), SideOutcome.Of(Bool(false)), SideOutcome.EvaluationError("hash of unhashable type types.Bytes")),
            (new Call(ProfileTables.In, [new Call(ProfileTables.Dyn, [x1]), x1]), SideOutcome.Of(Bool(false)), SideOutcome.Of(Bool(false))),
            (new Call(ProfileTables.In, [new Call(ProfileTables.Dyn, [I1]), x1]), SideOutcome.Of(Bool(false)), SideOutcome.Of(Bool(false))),
            (dynBytes, SideOutcome.Of(Bytes(0x61)), SideOutcome.Of(Bytes(0x61))),
            (bytes, SideOutcome.Of(Bytes(0x61)), SideOutcome.Of(Bytes(0x61))),
            (x1, SideOutcome.Of(Map()), SideOutcome.Of(Map())),
            (range, SideOutcome.Of(ListOf(Map(), Int(2), Bytes(0x61))), SideOutcome.Of(ListOf(Map(), Int(2), Bytes(0x61)))));
        DivergenceSearch search = new(library, oracle, new FakeRecorder(), KnownSignatures.Of("in_map(bytes, map) we-value"));

        Divergence? divergence = search.Check(RunSeed, CaseNumber, Case(root));

        Assert.Equal("in_map(bytes, map) we-value", divergence?.Signature);
        Assert.Equal("dyn(b'a') in x1", divergence?.Case.Text);
        Assert.Empty(search.NewSignatures);
    }

    [Fact]
    public void BodyInstances_PutTheElementOfTheRangeInPlaceOfTheVariable()
    {
        Expr positive = new Call(ProfileTables.Greater, [new Ident("v"), new IntLiteral(0, IntegerForm.DecimalDigits)]);
        Expr one = new IntLiteral(1, IntegerForm.DecimalDigits);
        Expr two = new IntLiteral(2, IntegerForm.DecimalDigits);
        Expr key = new StringLiteral("a", new TextForm(Quote.Apostrophe, null, [Escape.Literal]));
        Expr list = new ListLiteral([one, two], TrailingComma: false);
        Expr map = new MapLiteral([new MapEntry(key, one)], TrailingComma: false);

        // The last instance is the closed node 0, the literal of the argument.
        Assert.Equal(["(1) > 0", "(2) > 0", "0"], Printed(new Comprehension(ProfileTables.All, list, "v", [positive])));
        Assert.Equal(["dyn(1) > 0", "dyn(2) > 0", "0"], Printed(new Comprehension(ProfileTables.All, new Call(ProfileTables.Dyn, [list]), "v", [positive])));
        Assert.Equal(["('a') > 0", "dyn('a') > 0", "0"], Printed(new Comprehension(ProfileTables.All, map, "v", [positive])));
        Assert.Equal(["dyn('a') > 0", "0"], Printed(new Comprehension(ProfileTables.All, new Call(ProfileTables.Dyn, [map]), "v", [positive])));
    }

    // An element that is a string literal has one more instance in dyn(), after the instances of all the elements in parentheses;
    // an element that is not a literal, and every element of a range in dyn(), has none.
    [Fact]
    public void BodyInstances_OverStringLiteralElements_EndWithTheElementsInDyn()
    {
        Expr a = Quoted("a");
        Expr b = Quoted("b");
        Expr one = new IntLiteral(1, IntegerForm.DecimalDigits);
        Expr positive = new Call(ProfileTables.Greater, [new Ident("v"), one]);
        Expr strings = new ListLiteral([a, one, b], TrailingComma: false);
        Expr map = new MapLiteral([new MapEntry(a, one), new MapEntry(b, one)], TrailingComma: false);

        Assert.Equal(
            ["('a') > 1", "(1) > 1", "('b') > 1", "dyn('a') > 1", "dyn('b') > 1", "1"],
            Printed(new Comprehension(ProfileTables.All, strings, "v", [positive])));
        Assert.Equal(["('a') > 1", "('b') > 1", "dyn('a') > 1", "dyn('b') > 1", "1"], Printed(new Comprehension(ProfileTables.All, map, "v", [positive])));
        Assert.Equal(
            ["dyn('a') > 1", "dyn(1) > 1", "dyn('b') > 1", "1"],
            Printed(new Comprehension(ProfileTables.All, new Call(ProfileTables.Dyn, [strings]), "v", [positive])));
        Assert.Equal(["(1) > 1", "1"], Printed(new Comprehension(ProfileTables.All, new ListLiteral([one], TrailingComma: false), "v", [positive])));
    }

    [Fact]
    public void BodyInstances_TakeEveryBodyInOrder_OneForEachElementWhenItReadsTheVariableAndOneWhenItDoesNot()
    {
        Expr v = new Ident("v");
        Expr one = new IntLiteral(1, IntegerForm.DecimalDigits);
        Expr two = new IntLiteral(2, IntegerForm.DecimalDigits);
        Expr range = new ListLiteral([one, two], TrailingComma: false);
        Expr predicate = new Call(ProfileTables.Greater, [v, one]);
        Expr transform = new Call(ProfileTables.Add, [v, v]);

        Assert.Equal(
            ["(1) > 1", "(2) > 1", "1", "(1) + (1)", "(2) + (2)"],
            Printed(new Comprehension(ProfileTables.MapMacro, range, "v", [predicate, transform])));
        Assert.Equal(["(1) > 1", "(2) > 1", "1", "1"], Printed(new Comprehension(ProfileTables.MapMacro, range, "v", [predicate, one])));
        Assert.Equal(["1", "(1) > 1", "(2) > 1", "1"], Printed(new Comprehension(ProfileTables.MapMacro, range, "v", [one, predicate])));
        Assert.Equal(["1"], Printed(new Comprehension(ProfileTables.All, range, "v", [one])));
        Assert.Empty(Printed(new Call(ProfileTables.Add, [v, v])));
    }

    // A body that does not read the variable is one instance, the body itself, over a range with elements, with none, with an
    // error, and over a scalar; the library is not asked for the value of the range.
    [Fact]
    public void BodyInstances_OfABodyWithoutTheVariable_AreTheBodyItselfWhateverTheRangeHolds()
    {
        Expr one = new IntLiteral(1, IntegerForm.DecimalDigits);
        Expr independent = new Call(ProfileTables.Greater, [new Ident("x1"), one]);
        Expr[] ranges =
        [
            new ListLiteral([one, one], TrailingComma: false),
            new Call(ProfileTables.Dyn, [new ListLiteral([], TrailingComma: false)]),
            new MapLiteral([], TrailingComma: false),
            new Ident("lx"),
        ];

        foreach (Expr range in ranges)
        {
            foreach (Value? value in new Value?[] { null, Int(3), ListOf(), ListOf(Int(1)) })
            {
                int rangeCalls = 0;
                Expr macro = new Comprehension(ProfileTables.Exists, range, "v", [independent]);

                IEnumerable<string> instances = Minimizer.BodyInstances(macro, _ =>
                {
                    rangeCalls++;
                    return value;
                }).Select(ExprPrinter.Print);

                Assert.Equal(["x1 > 1"], instances);
                Assert.Equal(0, rangeCalls);
            }
        }
    }

    // The range is asked once, however many bodies read the variable, and not for a body that does not.
    [Fact]
    public void BodyInstances_AskTheRangeOnceAndOnlyForABodyThatReadsTheVariable()
    {
        Expr v = new Ident("v");
        Expr one = new IntLiteral(1, IntegerForm.DecimalDigits);
        Expr predicate = new Call(ProfileTables.Greater, [v, one]);
        Expr transform = new Call(ProfileTables.Add, [v, v]);
        Expr macro = new Comprehension(ProfileTables.MapMacro, new Ident("lx"), "v", [predicate, transform]);
        int rangeCalls = 0;

        List<string> instances = [.. Minimizer.BodyInstances(macro, _ =>
        {
            rangeCalls++;
            return ListOf(Int(5));
        }).Select(ExprPrinter.Print)];

        Assert.Equal(["lx[0] > 1", "1", "lx[0] + lx[0]"], instances);
        Assert.Equal(1, rangeCalls);
    }

    // A macro inside the body that declares the same variable keeps its own body; its range reads the outer variable.
    [Fact]
    public void BodyInstances_LeaveTheBodyOfANestedMacroWithTheSameVariable()
    {
        Expr v = new Ident("v");
        Expr one = new IntLiteral(1, IntegerForm.DecimalDigits);
        Expr inner = new Comprehension(ProfileTables.All, new ListLiteral([v], TrailingComma: false), "v", [new Call(ProfileTables.Greater, [v, one])]);
        Expr outer = new Comprehension(ProfileTables.All, new ListLiteral([one], TrailingComma: false), "v", [inner]);

        Assert.Equal(["[(1)].all(v, v > 1)", "1"], Printed(outer));
    }

    // The instances agree: the divergence is in the macro itself, and the case stays the macro.
    [Fact]
    public void Macro_WhoseBodyInstancesAgree_StaysTheMacro()
    {
        Expr range = new ListLiteral([I1], TrailingComma: false);
        Expr root = new Comprehension(ProfileTables.All, range, "v", [new Call(ProfileTables.Greater, [new Ident("v"), new IntLiteral(0, IntegerForm.DecimalDigits)])]);
        (FakeSide library, FakeSide oracle) = Sides(
            (root, SideOutcome.Of(Bool(true)), SideOutcome.Of(Bool(false))),
            (range, SideOutcome.Of(ListOf(Int(2))), SideOutcome.Of(ListOf(Int(2)))));
        DivergenceSearch search = new(library, oracle, new FakeRecorder(), KnownSignatures.Of());

        Divergence? divergence = search.Check(RunSeed, CaseNumber, Case(root, ("i1", Int(2))));

        Assert.Equal(root, divergence?.Case.Expression);
        Assert.Equal("comprehension(list) value-mismatch", divergence?.Signature);
    }

    // Cases 48263 and 84991 of seed 20261109: the divergence is in a body that does not read the variable, which has an instance
    // of its own over a range with elements, with none and with an error, so the case is minimized to the known divergence of
    // the body, and the macro does not hide it.
    [Theory]
    [InlineData("elements")]
    [InlineData("empty")]
    [InlineData("error")]
    public static void Macro_WithADivergenceInABodyWithoutTheVariable_IsMinimizedToTheBodyOverAnyRange(string range)
    {
        Expr x1 = new Ident("x1");
        Expr body = new Call(ProfileTables.Greater, [x1, new Ident("d1")]);
        Expr source = range switch
        {
            "elements" => new ListLiteral([I1], TrailingComma: false),
            "empty" => new Call(ProfileTables.Dyn, [new ListLiteral([], TrailingComma: false)]),
            _ => new Ident("lx"),
        };
        SideOutcome rangeOutcome = range == "error" ? SideOutcome.EvaluationError("no such attribute") : Agreed;

        AssertMinimizedToKnown(
            new Comprehension(ProfileTables.Exists, source, "v", [body]),
            body,
            SideOutcome.EvaluationError("no such overload: _>_(bytes, double)"),
            "_>_(bytes, double) we-error",
            "x1 > d1",
            Agree(x1, Bytes(0x61)),
            Agree(new Ident("d1"), Dbl(double.NaN)),
            (source, rangeOutcome, rangeOutcome));
    }

    // The divergence of a body without the variable that nobody knows stays the macro, with the signature of the macro.
    [Fact]
    public void Macro_WithAnUnknownDivergenceInABodyWithoutTheVariable_StaysTheMacro()
    {
        Expr x1 = new Ident("x1");
        Expr body = new Call(ProfileTables.Greater, [x1, new Ident("d1")]);
        Expr range = new Call(ProfileTables.Dyn, [new ListLiteral([], TrailingComma: false)]);
        Expr root = new Comprehension(ProfileTables.Exists, range, "v", [body]);
        SideOutcome error = SideOutcome.EvaluationError("no such overload: _>_(bytes, double)");
        (FakeSide library, FakeSide oracle) = Sides(
            (root, error, SideOutcome.Of(Bool(false))),
            (body, error, SideOutcome.Of(Bool(false))),
            (range, SideOutcome.Of(ListOf()), SideOutcome.Of(ListOf())));
        DivergenceSearch search = new(library, oracle, new FakeRecorder(), KnownSignatures.Of());

        Divergence? divergence = search.Check(RunSeed, CaseNumber, Case(root));

        Assert.Equal(root, divergence?.Case.Expression);
        Assert.Equal("comprehension(list) we-error", divergence?.Signature);
        Assert.Single(search.NewSignatures);
    }

    // ---- Minimization under a macro: closed nodes and an empty range ----

    // Case 10481 of seed 20261109: the argument of the macro reads the variable, the range is empty, and the divergence is in a
    // node that reads neither v nor w, so no element reaches it. The closed node is an instance of its own, and the case is the
    // known divergence of the node.
    [Fact]
    public void Macro_WithADivergenceInAClosedNodeOfAnArgumentThatReadsTheVariable_IsMinimizedToTheClosedNode()
    {
        Expr subject = Quoted("a");
        Expr pattern = Quoted("b");
        Expr closed = new Call("matches", [subject, pattern]);
        Expr inner = new Comprehension(ProfileTables.Exists, new Ident("v"), "w", [closed]);
        Expr source = new Call(ProfileTables.Dyn, [new ListLiteral([], TrailingComma: false)]);
        SideOutcome error = SideOutcome.EvaluationError("no such overload: matches");

        AssertMinimizedToKnown(
            new Comprehension(ProfileTables.Filter, source, "v", [inner]),
            closed,
            error,
            "matches(string, string) we-error",
            "matches('a', 'b')",
            Agree(subject, Str("a")),
            Agree(pattern, Str("b")),
            Agree(source, ListOf()));
    }

    // The closed nodes are the maximal nodes of the argument that read no variable of a macro above them, in the order of the
    // source: the node under a nested macro counts when it reads neither its variable nor the outer one, and a node that reads
    // the variable of the macro that encloses it is searched for closed nodes in its children.
    [Fact]
    public void BodyInstances_OfAnArgumentThatReadsTheVariable_EndWithItsClosedNodesInTheOrderOfTheSource()
    {
        Expr v = new Ident("v");
        Expr w = new Ident("w");
        Expr one = new IntLiteral(1, IntegerForm.DecimalDigits);
        Expr two = new IntLiteral(2, IntegerForm.DecimalDigits);
        Expr three = new IntLiteral(3, IntegerForm.DecimalDigits);
        Expr closedMacro = new Comprehension(ProfileTables.All, new ListLiteral([two], TrailingComma: false), "w", [new Call(ProfileTables.Greater, [w, three])]);
        Expr inner = new Comprehension(ProfileTables.Exists, v, "w", [new Call(ProfileTables.Greater, [new Call(ProfileTables.Add, [w, one]), v])]);
        Expr body = new Call(ProfileTables.LogicalAnd, [inner, closedMacro]);
        Expr range = new ListLiteral([three], TrailingComma: false);

        Assert.Equal(
            ["(3).exists(w, w + 1 > (3)) && [2].all(w, w > 3)", "1", "[2].all(w, w > 3)"],
            Printed(new Comprehension(ProfileTables.Filter, range, "v", [body])));
    }

    // Case 99729 of seed 20261109: the variable is the key of an empty map of strings, and the argument reads it. The zero
    // literal of string stands in its place, and the instance is the known divergence of the pattern, which no key ever reaches.
    [Fact]
    public void Macro_OverAnEmptyMapVariable_IsMinimizedToTheBodyWithTheZeroLiteralOfTheKeyType()
    {
        Expr pattern = Quoted("(?<=a)b");
        Expr empty = Quoted(string.Empty);
        Expr ms = new Ident("ms");
        Expr root = new Comprehension(ProfileTables.ExistsOne, ms, "v", [new Call("matches", [new Ident("v"), pattern])]);
        SideOutcome error = SideOutcome.EvaluationError("no such overload: matches");

        AssertMinimizedToKnown(
            root,
            new Call("matches", [empty, pattern]),
            error,
            "matches(string, string) we-error",
            "matches('', '(?<=a)b')",
            Agree(empty, Str(string.Empty)),
            Agree(pattern, Str("(?<=a)b")),
            Agree(ms, Map()));
    }

    // A range without elements gives an argument that reads the variable one instance for the zero literal of the type of the
    // variable: the key type of a map, the element type of a list, dyn in dyn(); for a range of an unknown type, none.
    [Fact]
    public void BodyInstances_OverARangeWithoutElements_PutTheZeroLiteralOfTheTypeOfTheVariable()
    {
        Expr v = new Ident("v");
        Expr body = new Call(ProfileTables.Equal, [v, v]);
        Expr dynEmpty = new Call(ProfileTables.Dyn, [new ListLiteral([], TrailingComma: false)]);

        Assert.Equal(["'' == ''"], Printed(new Comprehension(ProfileTables.All, new Ident("ms"), "v", [body]), Map()));
        Assert.Equal(["0 == 0"], Printed(new Comprehension(ProfileTables.All, new Ident("mx"), "v", [body]), Map()));
        Assert.Equal(["0 == 0"], Printed(new Comprehension(ProfileTables.All, new Ident("li"), "v", [body]), ListOf()));
        Assert.Equal(["'' == ''"], Printed(new Comprehension(ProfileTables.All, new Ident("ls"), "v", [body]), ListOf()));
        Assert.Equal(["dyn('') == dyn('')"], Printed(new Comprehension(ProfileTables.All, new Ident("lx"), "v", [body]), ListOf()));
        Assert.Equal(["dyn('') == dyn('')"], Printed(new Comprehension(ProfileTables.All, dynEmpty, "v", [body])));
        Assert.Equal(
            ["dyn('') == dyn('')"],
            Printed(new Comprehension(ProfileTables.All, new Call(ProfileTables.Dyn, [new Ident("ms")]), "v", [body]), Map()));
        Assert.Equal(
            ["dyn('') == dyn('')"],
            Printed(new Comprehension(ProfileTables.All, new Call(ProfileTables.Dyn, [new MapLiteral([], TrailingComma: false)]), "v", [body])));

        // The range is an empty literal that is not in dyn(), a variable that is not a list or a map, or a range of another kind.
        Assert.Empty(Printed(new Comprehension(ProfileTables.All, new ListLiteral([], TrailingComma: false), "v", [body])));
        Assert.Empty(Printed(new Comprehension(ProfileTables.All, new MapLiteral([], TrailingComma: false), "v", [body])));
        Assert.Empty(Printed(new Comprehension(ProfileTables.All, new Ident("unknown"), "v", [body]), ListOf()));
        Assert.Empty(Printed(new Comprehension(ProfileTables.All, new Call(ProfileTables.Add, [new Ident("li"), new Ident("li")]), "v", [body]), ListOf()));
        Assert.Empty(Printed(new Comprehension(ProfileTables.All, new Ident("i1"), "v", [body]), ListOf()));
    }

    // The zero literals of the profile, by the static type of the variable; a type without a zero literal has none.
    [Fact]
    public void ZeroLiteral_IsTheZeroOfEachScalarKindOfTheProfile()
    {
        Assert.Equal("''", ExprPrinter.Print(Minimizer.ZeroLiteral(StaticType.Of(ProfileType.StringType))!));
        Assert.Equal("0", ExprPrinter.Print(Minimizer.ZeroLiteral(StaticType.Of(ProfileType.IntType))!));
        Assert.Equal("0u", ExprPrinter.Print(Minimizer.ZeroLiteral(StaticType.Of(ProfileType.UintType))!));
        Assert.Equal("0.0", ExprPrinter.Print(Minimizer.ZeroLiteral(StaticType.Of(ProfileType.DoubleType))!));
        Assert.Equal("false", ExprPrinter.Print(Minimizer.ZeroLiteral(StaticType.Of(ProfileType.BoolType))!));
        Assert.Equal("b''", ExprPrinter.Print(Minimizer.ZeroLiteral(StaticType.Of(ProfileType.BytesType))!));
        Assert.Equal("dyn('')", ExprPrinter.Print(Minimizer.ZeroLiteral(StaticType.Dyn)!));
        Assert.Null(Minimizer.ZeroLiteral(StaticType.Of(ProfileType.NullType)));
        Assert.Null(Minimizer.ZeroLiteral(StaticType.Of(ProfileType.TypeType)));
        Assert.Null(Minimizer.ZeroLiteral(StaticType.ListOf(StaticType.Dyn)));
        Assert.Null(Minimizer.ZeroLiteral(StaticType.MapOf(StaticType.Dyn, StaticType.Dyn)));
    }

    // An unknown divergence in a closed node or under the zero literal stays the macro, with the signature of the macro: the
    // new instances are judged as every instance is.
    [Theory]
    [InlineData("closed")]
    [InlineData("zero")]
    public static void Macro_WithAnUnknownDivergenceInAClosedNodeOrUnderTheZeroLiteral_StaysTheMacro(string where)
    {
        Expr subject = Quoted("a");
        Expr pattern = Quoted("b");
        Expr closed = new Call("matches", [subject, pattern]);
        Expr source = new Call(ProfileTables.Dyn, [new ListLiteral([], TrailingComma: false)]);
        Expr body = where == "closed"
            ? new Comprehension(ProfileTables.Exists, new Ident("v"), "w", [closed])
            : new Call("matches", [new Ident("v"), pattern]);
        Expr root = new Comprehension(ProfileTables.Filter, source, "v", [body]);
        Expr instance = where == "closed"
            ? closed
            : new Call("matches", [new Call(ProfileTables.Dyn, [Quoted(string.Empty)]), pattern]);
        SideOutcome error = SideOutcome.EvaluationError("no such overload: matches");
        (FakeSide library, FakeSide oracle) = Sides(
            (root, error, SideOutcome.Of(ListOf())),
            (instance, error, SideOutcome.Of(Bool(false))),
            Agree(source, ListOf()));
        DivergenceSearch search = new(library, oracle, new FakeRecorder(), KnownSignatures.Of());

        Divergence? divergence = search.Check(RunSeed, CaseNumber, Case(root));

        Assert.Equal(root, divergence?.Case.Expression);
        Assert.StartsWith("comprehension(list)", divergence?.Signature, StringComparison.Ordinal);
        Assert.Single(search.NewSignatures);
    }

    // ---- Minimization under a macro over a range that is not a literal ----

    // Case 22910 of seed 20261108: the range is a variable, so the elements are those of its value on the library: the index
    // instances are taken from 0 up, and the first that diverges is the case.
    [Fact]
    public void Macro_OverVariable_IsMinimizedToABodyInstanceWithTheFirstDivergentIndex()
    {
        Expr x1 = new Ident("x1");
        Expr lx = new Ident("lx");
        Expr root = new Comprehension(ProfileTables.Exists, lx, "v", [new Call(ProfileTables.Greater, [x1, new Ident("v")])]);
        SideOutcome error = SideOutcome.EvaluationError("no such overload: _>_(bytes, double)");

        AssertMinimizedToKnown(
            root,
            new Call(ProfileTables.Greater, [x1, Index(lx, 1)]),
            error,
            "_>_(bytes, double) we-error",
            "x1 > lx[1]",
            Agree(x1, Bytes(0x61)),
            Agree(Index(lx, 0), Int(2)),
            Agree(Index(lx, 1), Dbl(double.NaN)),
            Agree(lx, ListOf(Int(2), Dbl(double.NaN))));
    }

    // Case 365398 of seed 20261108: the range is dyn(lx + []), so the instance is the index of the whole range in dyn().
    [Fact]
    public void Macro_OverDynOfSum_IsMinimizedToABodyInstanceThatIndexesTheWholeRange()
    {
        Expr d1 = new Ident("d1");
        Expr range = new Call(ProfileTables.Dyn, [new Call(ProfileTables.Add, [new Ident("lx"), new ListLiteral([], TrailingComma: false)])]);
        Expr root = new Comprehension(ProfileTables.Exists, range, "v", [new Call(ProfileTables.LessEqual, [d1, new Ident("v")])]);
        SideOutcome error = SideOutcome.EvaluationError("no such overload: _<=_(double, bool)");

        AssertMinimizedToKnown(
            root,
            new Call(ProfileTables.LessEqual, [d1, Index(range, 0)]),
            error,
            "_<=_(double, bool) we-error",
            "d1 <= dyn(lx + [])[0]",
            Agree(d1, Dbl(double.NaN)),
            Agree(Index(range, 0), Bool(false)),
            Agree(range, ListOf(Bool(false), Int(7), new Value { NullValue = NullValue.NullValue })));
    }

    // A map in a variable: the instances hold the keys in dyn() in the order the library iterates the map, not in an order of
    // their own: both instances diverge, and the first of the library is the case.
    [Fact]
    public void Macro_OverMapVariable_TakesTheKeysInTheOrderOfTheLibrary()
    {
        Expr x1 = new Ident("x1");
        Expr mx = new Ident("mx");
        Expr root = new Comprehension(ProfileTables.All, mx, "v", [new Call(ProfileTables.Greater, [x1, new Ident("v")])]);
        SideOutcome error = SideOutcome.EvaluationError("no such overload: _>_(bytes, double)");
        Value map = Map((Int(7), Int(0)), (Int(3), Int(0)));
        Expr seven = new Call(ProfileTables.Dyn, [new IntLiteral(7, IntegerForm.DecimalDigits)]);
        Expr three = new Call(ProfileTables.Dyn, [new IntLiteral(3, IntegerForm.DecimalDigits)]);
        (FakeSide library, FakeSide oracle) = Sides(
            (root, error, SideOutcome.Of(Bool(false))),
            (new Call(ProfileTables.Greater, [x1, seven]), error, SideOutcome.Of(Bool(false))),
            (new Call(ProfileTables.Greater, [x1, three]), error, SideOutcome.Of(Bool(false))),
            (x1, SideOutcome.Of(Bytes(0x61)), SideOutcome.Of(Bytes(0x61))),
            (seven, SideOutcome.Of(Dbl(1.5)), SideOutcome.Of(Dbl(1.5))),
            (three, SideOutcome.Of(Dbl(1.5)), SideOutcome.Of(Dbl(1.5))),
            (mx, SideOutcome.Of(map), SideOutcome.Of(map)));
        DivergenceSearch search = new(library, oracle, new FakeRecorder(), KnownSignatures.Of("_>_(bytes, double) we-error"));

        Divergence? divergence = search.Check(RunSeed, CaseNumber, Case(root));

        Assert.Equal("x1 > dyn(7)", divergence?.Case.Text);
    }

    // The keys of a map are literals of the profile: int, uint, bool and string, a negative int and a quote included.
    [Fact]
    public void BodyInstances_OverAMapValue_PutTheKeysAsLiteralsInDyn()
    {
        Expr v = new Ident("v");
        Expr body = new Call(ProfileTables.Equal, [v, new IntLiteral(1, IntegerForm.DecimalDigits)]);
        Value map = Map((Int(-1), Int(0)), (new Value { Uint64Value = 2 }, Int(0)), (Bool(true), Int(0)), (Str("it's"), Int(0)));

        Assert.Equal(
            ["dyn(-1) == 1", "dyn(2u) == 1", "dyn(true) == 1", "dyn('it\\'s') == 1", "1"],
            Printed(new Comprehension(ProfileTables.All, new Ident("mx"), "v", [body]), map));
    }

    [Fact]
    public void BodyInstances_OverAListValue_IndexTheRangeFromZero()
    {
        Expr v = new Ident("v");
        Expr body = new Call(ProfileTables.Add, [v, v]);
        Value list = ListOf(Int(5), Dbl(double.NaN), new Value { NullValue = NullValue.NullValue });
        Expr sum = new Call(ProfileTables.Add, [new Ident("lx"), new ListLiteral([], TrailingComma: false)]);

        Assert.Equal(
            ["lx[0] + lx[0]", "lx[1] + lx[1]", "lx[2] + lx[2]"],
            Printed(new Comprehension(ProfileTables.MapMacro, new Ident("lx"), "v", [body]), list));
        Assert.Equal(
            ["(lx + [])[0] + (lx + [])[0]"],
            Printed(new Comprehension(ProfileTables.MapMacro, sum, "v", [body]), ListOf(Int(5))));
    }

    // A range whose value is an error, is unbound or is a scalar gives the argument that reads the variable one instance for each
    // zero literal of the profile, whatever the type of the variable, and then the closed node 0 of the argument; the library is
    // asked for the range once.
    [Fact]
    public void Macro_WhoseRangeIsNeitherAListNorAMap_HasTheZeroLiteralsOfTheProfileAsInstances()
    {
        Expr lx = new Ident("lx");
        Expr root = new Comprehension(ProfileTables.All, lx, "v", [new Call(ProfileTables.Greater, [new Ident("v"), new IntLiteral(0, IntegerForm.DecimalDigits)])]);
        GeneratedCase divergent = Case(root);

        foreach (Value? value in new Value?[] { null, Int(3), Str("abc"), Bool(true), Bytes(0x61), new Value { NullValue = NullValue.NullValue } })
        {
            List<string> asked = [];
            int rangeCalls = 0;

            GeneratedCase minimized = Minimizer.Minimize(
                divergent,
                Verdicts(candidate =>
                {
                    asked.Add(candidate.Text);
                    return candidate.Text == divergent.Text;
                }),
                (_, _) => true,
                candidate =>
                {
                    rangeCalls++;
                    Assert.Equal("lx", candidate.Text);
                    return value;
                });

            Assert.Same(divergent, minimized);
            Assert.Equal(["lx", "'' > 0", "0 > 0", "0u > 0", "0.0 > 0", "false > 0", "b'' > 0", "dyn('') > 0", "0"], asked);
            Assert.Equal(1, rangeCalls);
        }
    }

    // An instance without a macro is smaller than the macro even when its text is longer: the variable read twice is the range
    // read twice.
    [Fact]
    public void BodyInstance_WithoutAMacro_IsAcceptedAlthoughItsTextIsLongerThanTheMacro()
    {
        Expr v = new Ident("v");
        Expr range = new Call(ProfileTables.Dyn, [new Call(ProfileTables.Add, [new Ident("lx"), new ListLiteral([], TrailingComma: false)])]);
        Expr root = new Comprehension(ProfileTables.All, range, "v", [new Call(ProfileTables.Equal, [v, v])]);
        GeneratedCase divergent = Case(root);
        const string instance = "dyn(lx + [])[0] == dyn(lx + [])[0]";

        GeneratedCase minimized = Minimizer.Minimize(
            divergent,
            Verdicts(candidate => candidate.Text == divergent.Text || candidate.Text == instance),
            (_, _) => true,
            _ => ListOf(Int(1)));

        Assert.Equal(instance, minimized.Text);
        Assert.True(minimized.Text.Length > divergent.Text.Length);
        Assert.Equal(0, Minimizer.MacroNodes(minimized.Expression));
        Assert.Equal(1, Minimizer.MacroNodes(divergent.Expression));
    }

    // An instance that copies a range with a macro into two places has as many macros and a longer text, so it is not smaller:
    // it is dropped without asking the sides, although it would diverge.
    [Fact]
    public void BodyInstance_ThatCopiesARangeWithAMacroAndIsNotSmaller_IsDropped()
    {
        Expr v = new Ident("v");
        Expr range = new Comprehension(ProfileTables.Filter, new Ident("lx"), "w", [new Ident("w")]);
        Expr root = new Comprehension(ProfileTables.All, range, "v", [new Call(ProfileTables.Equal, [v, v])]);
        GeneratedCase divergent = Case(root);
        List<string> asked = [];

        GeneratedCase minimized = Minimizer.Minimize(
            divergent,
            Verdicts(candidate =>
            {
                asked.Add(candidate.Text);
                return candidate.Text == divergent.Text || candidate.Text.Contains("[0]", StringComparison.Ordinal);
            }),
            (_, _) => true,
            _ => ListOf(Int(1)));

        Assert.Same(divergent, minimized);
        Assert.Equal(["lx.filter(w, w)"], asked);
        Assert.Equal(2, Minimizer.MacroNodes(root));
        Assert.Equal(2, Minimizer.MacroNodes(Minimizer.BodyInstances(root, _ => ListOf(Int(1))).Single()));
    }

    // ---- Minimization under a macro over a range that is not a list or a map ----

    // The instances of a range whose value is not a list or a map are the zero literals of the profile in the order of
    // ZeroLiteral, whatever the type of the variable and whatever the range holds: no value, a scalar, null.
    [Fact]
    public void BodyInstances_OverARangeThatIsNeitherAListNorAMap_PutEveryZeroLiteralOfTheProfile()
    {
        Expr v = new Ident("v");
        Expr body = new Call(ProfileTables.Equal, [v, v]);
        Expr macro = new Comprehension(ProfileTables.All, new Ident("lx"), "v", [body]);
        string[] expected = ["'' == ''", "0 == 0", "0u == 0u", "0.0 == 0.0", "false == false", "b'' == b''", "dyn('') == dyn('')"];

        foreach (Value? value in new Value?[] { null, Int(3), Str("abc"), new Value { NullValue = NullValue.NullValue } })
        {
            Assert.Equal(expected, Printed(macro, value));
        }
    }

    // Cases 100758, 63618, 775671 and 1274797 of seeds 1028936231 and 168408093: the known divergence matches(string, string)
    // stage-mismatch (the library refuses the constant pattern when the expression is compiled, cel-go fails when the call is
    // evaluated) sits under a macro over a range that gives no element, so the macro is minimized to the instance of its body
    // with the zero literal of the profile. The range is an unbound variable, an index into a map literal whose value is an
    // error, a scalar under dyn(), and, in a nested macro, a conditional whose value is an error.
    [Fact]
    public void Macro_OverAnUnboundVariable_IsMinimizedToTheMatchesWithTheZeroLiteral()
    {
        Expr ls = new Ident("ls");
        SideOutcome unbound = SideOutcome.EvaluationError("no such attribute(s): ls");
        Expr root = new Comprehension(ProfileTables.ExistsOne, ls, "v", [MatchesOf(new Ident("v"))]);

        AssertMinimizedToMatches(root, [root, MatchesOf(Quoted(string.Empty))], "matches('', '(')", (ls, unbound, unbound));
    }

    [Fact]
    public void Macro_OverAnIndexIntoAMapLiteralWhoseValueIsAnError_IsMinimizedToTheMatchesWithTheZeroLiteral()
    {
        Expr ls = new Ident("ls");
        Expr x1 = new Ident("x1");
        Expr map = new MapLiteral([new MapEntry(Quoted("a"), ls), new MapEntry(new IntLiteral(1, IntegerForm.DecimalDigits), x1)], TrailingComma: false);
        Expr range = new Call(ProfileTables.Index, [map, Quoted("k")]);
        SideOutcome unbound = SideOutcome.EvaluationError("no such attribute(s): x1");
        Expr root = new Comprehension(ProfileTables.Exists, range, "v", [MatchesOf(new Ident("v"))]);

        AssertMinimizedToMatches(
            root,
            [root, MatchesOf(Quoted(string.Empty))],
            "matches('', '(')",
            (ls, unbound, unbound),
            (x1, unbound, unbound),
            (map, unbound, unbound),
            (range, unbound, unbound));
    }

    // Case 1191079 of seed 2367977110: the element of the range is a string literal that is no pattern, `?`. In parentheses in
    // place of the variable it is the first argument of matches, which the validator of cel-go reads as a pattern and refuses
    // when the expression is compiled, as the library refuses the constant pattern, so the two sides agree and the instance is
    // dropped; the instance with the element in dyn() is not a literal for the validator, and it diverges by the known signature.
    [Fact]
    public void Macro_OverAListWithAStringLiteralThatIsNoPattern_IsMinimizedToTheMatchesWithTheElementInDyn()
    {
        Expr element = Quoted("?");
        Expr root = new Comprehension(ProfileTables.Exists, new ListLiteral([element], TrailingComma: false), "v", [MatchesOf(new Ident("v"))]);
        Expr inParentheses = MatchesOf(Parenthesized(element));
        Expr inDyn = new Call(ProfileTables.Dyn, [element]);
        SideOutcome refused = SideOutcome.CompileError(CompileStage.Check, "invalid regular expression at offset 0: missing closing )");
        SideOutcome subjectRefused = SideOutcome.CompileError(CompileStage.Check, "invalid matches argument");

        AssertMinimizedToMatches(
            root,
            [root, MatchesOf(inDyn)],
            "matches(dyn('?'), '(')",
            (inParentheses, refused, subjectRefused),
            Agree(inDyn, Str("?")));
    }

    [Fact]
    public void Macro_OverNullUnderDyn_IsMinimizedToTheMatchesWithTheZeroLiteral()
    {
        Expr x2 = new Ident("x2");
        Expr range = new Call(ProfileTables.Dyn, [x2]);
        Value none = new() { NullValue = NullValue.NullValue };
        Expr root = new Comprehension(ProfileTables.Exists, range, "v", [MatchesOf(new Ident("v"))]);

        AssertMinimizedToMatches(root, [root, MatchesOf(Quoted(string.Empty))], "matches('', '(')", Agree(x2, none), Agree(range, none));
    }

    // The body of the outer macro does not read its variable, so it is an instance of itself, and the nested macro has a
    // conditional for a range, whose value is an error.
    [Fact]
    public void NestedMacro_OverAConditionalWhoseValueIsAnError_IsMinimizedToTheMatchesWithTheZeroLiteral()
    {
        Expr ls = new Ident("ls");
        Expr lx = new Ident("lx");
        Expr conditional = new Call(ProfileTables.Conditional, [new Ident("b1"), ls, lx]);
        SideOutcome unbound = SideOutcome.EvaluationError("no such attribute(s): lx");
        Expr inner = new Comprehension(ProfileTables.All, conditional, "w", [MatchesOf(new Ident("w"))]);
        Expr source = new ListLiteral([new Call(ProfileTables.Dyn, [new NullLiteral()]), new UintLiteral(1, IntegerForm.DecimalDigits, 'u')], TrailingComma: false);
        Expr root = new Comprehension(ProfileTables.Filter, source, "v", [inner]);

        AssertMinimizedToMatches(
            root,
            [root, inner, MatchesOf(Quoted(string.Empty))],
            "matches('', '(')",
            (ls, unbound, unbound),
            (lx, unbound, unbound),
            (conditional, unbound, unbound));
    }

    // Case 775671 of seed 168408093: the range of the nested macro is a list literal with elements, and its body reads the
    // variable of the outer macro through the elements, so after the zero literal of the outer macro the descent goes on to the
    // element of the nested range and reaches matches(string, string).
    [Fact]
    public void NestedMacro_UnderANullRangeUnderDyn_IsMinimizedThroughTheZeroLiteralAndTheElement()
    {
        Expr x1 = new Ident("x1");
        Expr x2 = new Ident("x2");
        Expr r = new Ident("r");
        Expr range = new Call(ProfileTables.Dyn, [x2]);
        Value none = new() { NullValue = NullValue.NullValue };
        SideOutcome unbound = SideOutcome.EvaluationError("no such attribute(s): x1");
        Expr NestedRange(Expr first) => new ListLiteral([new Call(ProfileTables.Add, [first, r]), x1], TrailingComma: false);
        Expr Nested(Expr first) => new Comprehension(ProfileTables.Exists, NestedRange(first), "w", [MatchesOf(new Ident("w"))]);
        Expr root = new Comprehension(ProfileTables.Exists, range, "v", [Nested(new Ident("v"))]);
        Expr zeroed = Nested(Quoted(string.Empty));
        Expr sum = Parenthesized(new Call(ProfileTables.Add, [Quoted(string.Empty), r]));

        AssertMinimizedToMatches(
            root,
            [root, zeroed, MatchesOf(sum)],
            "matches(('' + r), '(')",
            Agree(x2, none),
            Agree(range, none),
            (x1, unbound, unbound),
            (NestedRange(Quoted(string.Empty)), unbound, unbound),
            Agree(sum, Str("a")));
    }

    // The instance of a type other than the variable does not compile on either side: the two agree, and it is dropped; the next
    // zero literal in the order is tried, here dyn('').
    [Fact]
    public void ZeroLiteralInstance_ThatAgrees_IsDroppedAndTheNextOneIsTried()
    {
        Expr x2 = new Ident("x2");
        Expr range = new Call(ProfileTables.Dyn, [x2]);
        Value none = new() { NullValue = NullValue.NullValue };
        Expr root = new Comprehension(ProfileTables.Exists, range, "v", [MatchesOf(new Ident("v"))]);
        Expr dynEmpty = new Call(ProfileTables.Dyn, [Quoted(string.Empty)]);

        AssertMinimizedToMatches(
            root,
            [root, MatchesOf(dynEmpty)],
            "matches(dyn(''), '(')",
            Agree(x2, none),
            Agree(range, none),
            Agree(dynEmpty, Str(string.Empty)));
    }

    // A divergence of the instance that nobody knows, and that is not the signature of the macro, stays the macro.
    [Fact]
    public void Macro_OverAnUnboundVariable_WithAnUnknownDivergenceUnderTheBody_StaysTheMacro()
    {
        Expr ls = new Ident("ls");
        SideOutcome unbound = SideOutcome.EvaluationError("no such attribute(s): ls");
        Expr root = new Comprehension(ProfileTables.ExistsOne, ls, "v", [MatchesOf(new Ident("v"))]);

        (Divergence? divergence, DivergenceSearch search) = SearchUnderMacro(
            [],
            root,
            [root, MatchesOf(Quoted(string.Empty))],
            (ls, unbound, unbound),
            Agree(Quoted(string.Empty), Str(string.Empty)),
            Agree(Quoted("("), Str("(")));

        Assert.Equal(root, divergence?.Case.Expression);
        Assert.Equal("comprehension(error) stage-mismatch", divergence?.Signature);
        Assert.Single(search.NewSignatures);
    }

    // ---- ENG822 ----

    [Fact]
    public void AnotherZoneOffset_GivesEng822AtThePathTheTestGives()
    {
        using TempDirectory root = new();
        const string path = "data/zones/zoneinfo.zip";
        WriteZones(root, path, "Europe/Berlin", "Asia/Kolkata", "UTC");
        DateTimeOffset summer = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset winter = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        FakeSide oracle = Zones(("Europe/Berlin", 1), ("Asia/Kolkata", 5.5), ("UTC", 0));
        FakeSide library = Zones(("Europe/Berlin", 2), ("Asia/Kolkata", 5.5), ("UTC", 0));

        IReadOnlyList<Finding> findings = ZoneOffsets.Compare(root.Path, path, [winter, summer], library, oracle);

        FindingAssert.Exactly(
            findings,
            FindingAssert.Error(
                EngCodes.Eng822,
                path,
                1,
                1,
                "time zone offset differs from Go: zone 'Europe/Berlin' at 2026-01-01T12:00:00Z: Go +01:00, we +02:00"));
    }

    [Fact]
    public void EqualZoneOffsets_GiveNoFinding()
    {
        using TempDirectory root = new();
        WriteZones(root, "zones.zip", "Europe/Berlin");
        FakeSide side = Zones(("Europe/Berlin", 1));

        FindingAssert.None(ZoneOffsets.Compare(root.Path, "zones.zip", ZoneOffsets.Instants(RunSeed, 5), side, side));
    }

    [Fact]
    public void Offsets_AreHoursAndMinutesWithSecondsWhenTheyHaveSeconds()
    {
        Assert.Equal("+00:00", ZoneOffsets.Offset(TimeSpan.Zero));
        Assert.Equal("+05:30", ZoneOffsets.Offset(new TimeSpan(5, 30, 0)));
        Assert.Equal("-03:30", ZoneOffsets.Offset(new TimeSpan(-3, -30, 0)));
        Assert.Equal("+00:53:28", ZoneOffsets.Offset(new TimeSpan(0, 53, 28)));
        Assert.Equal("-00:00:36", ZoneOffsets.Offset(TimeSpan.FromSeconds(-36)));
        Assert.Equal("1800-01-01T00:00:00Z", ZoneOffsets.Instant(new DateTimeOffset(1800, 1, 1, 3, 0, 0, TimeSpan.FromHours(3))));
    }

    [Fact]
    public void Instants_DependOnTheSeedAloneAndLieIn1800To2199()
    {
        IReadOnlyList<DateTimeOffset> instants = ZoneOffsets.Instants(RunSeed, 50);

        Assert.Equal(instants, ZoneOffsets.Instants(RunSeed, 50));
        Assert.NotEqual(instants, ZoneOffsets.Instants(RunSeed + 1, 50));
        Assert.All(instants, instant => Assert.InRange(instant.Year, 1800, 2199));
        Assert.Equal(55, instants.Count);
    }

    [Fact]
    public void Instants_EndWithTheFiveFixedInstants()
    {
        IReadOnlyList<DateTimeOffset> instants = ZoneOffsets.Instants(RunSeed, 256);

        Assert.Equal(261, instants.Count);
        Assert.Equal(
            ["1800-01-01T00:00:00Z", "1900-01-01T00:00:00Z", "1970-01-01T00:00:00Z", "2037-01-01T00:00:00Z", "2100-01-01T00:00:00Z"],
            instants.TakeLast(5).Select(ZoneOffsets.Instant));
        Assert.Equal(instants.Take(256), ZoneOffsets.Instants(RunSeed, 256).Take(256));
    }

    [Fact]
    public void ZoneInfoPath_NamesTheZipOfTheRepository()
    {
        Assert.Equal("tests/Great.Parts.Cel.Conformance/ZoneInfo/zoneinfo.zip", ProfileTables.ZoneInfoPath);
        Assert.True(File.Exists(Path.Combine(RepositoryRoot.Find(), ProfileTables.ZoneInfoPath)));
    }

    // ---- Helpers ----

    private static IReadOnlyList<Finding> Run(FakeSide library, FakeSide oracle, KnownSignatures known, GeneratedCase generated)
    {
        DivergenceSearch search = new(library, oracle, new FakeRecorder(), known);
        _ = search.Check(RunSeed, CaseNumber, generated);
        return PendingFile.Findings(search.NewSignatures, PendingFile.Build(search.NewSignatures));
    }

    /// <summary>Whether a case reads the bound variable i1: the fake library of a test diverges on such cases.</summary>
    /// <param name="text">The expression.</param>
    /// <param name="bindings">The bindings.</param>
    internal static bool ReadsI1(string text, IReadOnlyDictionary<string, Value> bindings) =>
        bindings.ContainsKey("i1") && text.Contains("i1", StringComparison.Ordinal);

    /// <summary>
    /// The number of the first generated case of a seed whose root is a call, which reads the bound variable i1, and which has
    /// no macro and no selection, so that the descent reaches i1.
    /// </summary>
    /// <param name="seed">The seed of the run.</param>
    internal static ulong CallReadingI1(ulong seed) => Enumerable.Range(0, 1000).Select(n => (ulong)n)
        .First(n => CaseGenerator.Case(seed, n) is { Expression: Call } generated && ReadsI1(generated.Text, generated.Bindings)
            && !Nodes(generated.Expression).Any(node => node is Comprehension or Select or PresenceTest));

    // The nodes of a case by a walk of its own, not by Minimizer.Children: the set that bounds the descent must not come from the
    // function that the descent uses.
    private static IEnumerable<Expr> Nodes(Expr node) => Children(node).SelectMany(Nodes).Prepend(node);

    private static IEnumerable<Expr> Children(Expr expr) => expr switch
    {
        Call call => call.Args,
        ListLiteral list => list.Elements,
        MapLiteral map => map.Entries.SelectMany(entry => new[] { entry.Key, entry.Value }),
        Select select => [select.Operand],
        PresenceTest test => [test.Operand],
        Comprehension macro => macro.Arguments.Prepend(macro.Range),
        _ => [],
    };

    // A macro whose library gives the error and the oracle the value false, as does its body instance `body`, is minimized to the
    // text, and its signature is the known one; `agreed` are the nodes on which the sides agree.
    private static void AssertMinimizedToKnown(
        Expr root, Expr body, SideOutcome error, string known, string text, params (Expr Node, SideOutcome Library, SideOutcome Oracle)[] agreed)
    {
        (FakeSide library, FakeSide oracle) = Sides([(root, error, SideOutcome.Of(Bool(false))), (body, error, SideOutcome.Of(Bool(false))), .. agreed]);
        DivergenceSearch search = new(library, oracle, new FakeRecorder(), KnownSignatures.Of(known));

        Divergence? divergence = search.Check(RunSeed, CaseNumber, Case(root));

        Assert.Equal(known, divergence?.Signature);
        Assert.Equal(text, divergence?.Case.Text);
        Assert.Empty(search.NewSignatures);
    }

    // matches(subject, '('): a constant pattern that RE2 refuses.
    private static Call MatchesOf(Expr subject) => new Call(ProfileTables.Matches, [subject, Quoted("(")]);

    // The library refuses the constant pattern of matches when the expression is compiled and cel-go fails when the call is
    // evaluated; the nodes that diverge so are `diverging`, `others` are the outcomes of the other nodes, and every node that is
    // not named agrees.
    private static (Divergence? Divergence, DivergenceSearch Search) SearchUnderMacro(
        string[] known, Expr root, Expr[] diverging, params (Expr Node, SideOutcome Library, SideOutcome Oracle)[] others)
    {
        SideOutcome refused = SideOutcome.CompileError(CompileStage.Check, "invalid regular expression at offset 0: missing closing )");
        SideOutcome failed = SideOutcome.EvaluationError("error parsing regexp: missing closing ): `(`");
        (FakeSide library, FakeSide oracle) = Sides([.. diverging.Select(node => (node, refused, failed)), .. others]);
        DivergenceSearch search = new(library, oracle, new FakeRecorder(), KnownSignatures.Of(known));
        return (search.Check(RunSeed, CaseNumber, Case(root)), search);
    }

    // The case is minimized to the text with the known signature of matches, and no signature is new.
    private static void AssertMinimizedToMatches(
        Expr root, Expr[] diverging, string text, params (Expr Node, SideOutcome Library, SideOutcome Oracle)[] others)
    {
        const string known = "matches(string, string) stage-mismatch";
        (Divergence? divergence, DivergenceSearch search) = SearchUnderMacro(
            [known],
            root,
            diverging,
            [.. others, Agree(Quoted(string.Empty), Str(string.Empty)), Agree(Quoted("("), Str("("))]);

        Assert.Equal(known, divergence?.Signature);
        Assert.Equal(text, divergence?.Case.Text);
        Assert.Empty(search.NewSignatures);
        Assert.Equal([known], search.KnownSignaturesFound);
    }

    // The delegate of the minimizer that takes a case that a test names for divergent and any other for agreeing.
    private static Func<GeneratedCase, NodeVerdict> Verdicts(Func<GeneratedCase, bool> diverges) =>
        generated => diverges(generated) ? NodeVerdict.Diverges : NodeVerdict.Agrees;

    // The delegate of the minimizer on fake sides, with the verdict that the search of eng/difftest gives.
    private static Func<GeneratedCase, NodeVerdict> Classifier(FakeSide library, FakeSide oracle) => generated =>
        DivergenceSearch.VerdictOf(library.Evaluate(generated.Text, generated.Bindings), oracle.Evaluate(generated.Text, generated.Bindings));

    // The texts of nodes, as the fake sides are asked for them.
    private static IEnumerable<string> Texts(params Expr[] nodes) => nodes.Select(ExprPrinter.Print);

    // The outcome of a node that both fake sides give alike.
    private static (Expr Node, SideOutcome Library, SideOutcome Oracle) Agree(Expr node, Value value) =>
        (node, SideOutcome.Of(value), SideOutcome.Of(value));

    // A fixed case.
    private static GeneratedCase Case(Expr root, params (string Name, Value Value)[] bindings) =>
        new(root, bindings.ToDictionary(binding => binding.Name, binding => binding.Value));

    // Fake sides that give the outcomes of the named expressions, and agree on every other expression.
    private static (FakeSide Library, FakeSide Oracle) Sides(params (Expr Node, SideOutcome Library, SideOutcome Oracle)[] outcomes)
    {
        Dictionary<string, (SideOutcome Library, SideOutcome Oracle)> table = outcomes.ToDictionary(
            outcome => ExprPrinter.Print(outcome.Node),
            outcome => (outcome.Library, outcome.Oracle));
        return (
            new FakeSide((text, _) => table.TryGetValue(text, out (SideOutcome Library, SideOutcome Oracle) pair) ? pair.Library : Agreed),
            new FakeSide((text, _) => table.TryGetValue(text, out (SideOutcome Library, SideOutcome Oracle) pair) ? pair.Oracle : Agreed));
    }

    private static FakeSide Zones(params (string Zone, double Hours)[] offsets) => new(
        (_, _) => Agreed,
        (zone, _) => TimeSpan.FromHours(offsets.Single(offset => offset.Zone == zone).Hours));

    private static void WriteZones(TempDirectory root, string path, params string[] zones)
    {
        string file = Path.Combine(root.Path, path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        using ZipArchive archive = ZipFile.Open(file, ZipArchiveMode.Create);
        _ = archive.CreateEntry("Europe/");
        foreach (string zone in zones)
        {
            _ = archive.CreateEntry(zone);
        }
    }

    private static IEnumerable<string> Printed(Expr macro, Value? range = null) => Minimizer.BodyInstances(macro, _ => range).Select(ExprPrinter.Print);

    // The index of a range, as the minimizer builds it.
    private static Call Index(Expr range, int index) => new Call(ProfileTables.Index, [range, new IntLiteral(index, IntegerForm.DecimalDigits)]);

    private static Expr Parenthesized(Expr expr) => expr with { Parenthesized = true };

    // A string literal in apostrophes, each character as itself.
    private static StringLiteral Quoted(string value) =>
        new(value, new TextForm(Quote.Apostrophe, null, [.. value.Select(_ => Escape.Literal)]));

    private static Value Int(long value) => new() { Int64Value = value };

    private static Value Dbl(double value) => new() { DoubleValue = value };

    private static Value Bytes(params byte[] value) => new() { BytesValue = ByteString.CopyFrom(value) };

    private static Value Bool(bool value) => new() { BoolValue = value };

    private static Value Str(string value) => new() { StringValue = value };

    private static Value ListOf(params Value[] values)
    {
        ListValue list = new();
        list.Values.AddRange(values);
        return new Value { ListValue = list };
    }

    private static Value Map(params (Value Key, Value Value)[] entries)
    {
        MapValue map = new();
        map.Entries.AddRange(entries.Select(entry => new MapValue.Types.Entry { Key = entry.Key, Value = entry.Value }));
        return new Value { MapValue = map };
    }

    // A side with given outcomes and zone offsets.
    private sealed class FakeSide(
        Func<string, IReadOnlyDictionary<string, Value>, SideOutcome> evaluate,
        Func<string, DateTimeOffset, TimeSpan>? zoneOffset = null) : ICaseSide
    {
        // The expressions that the side was asked for, in order.
        public List<string> Evaluated { get; } = [];

        public SideOutcome Evaluate(string expression, IReadOnlyDictionary<string, Value> bindings)
        {
            Evaluated.Add(expression);
            return evaluate(expression, bindings);
        }

        public TimeSpan ZoneOffset(string zone, DateTimeOffset instant) =>
            zoneOffset is null ? throw new NotSupportedException("no time zones") : zoneOffset(zone, instant);
    }

    // The oracle writing cases: a text with two spaces after each colon, as prototext may give it.
    private sealed class FakeRecorder : ICaseRecorder
    {
        public List<string> Names { get; } = [];

        public string Case(string name, string description, string expression, IReadOnlyDictionary<string, Value> bindings)
        {
            Names.Add(name);
            return $"name:  \"{name}\"\ndescription:  \"{description}\"\nexpr:  \"{expression}\"";
        }
    }
}
