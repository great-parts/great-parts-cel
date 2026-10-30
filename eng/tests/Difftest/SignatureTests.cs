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
        ];
        (Expr, SideOutcome, SideOutcome)[] children =
        [
            (one, SideOutcome.Of(Int(1)), SideOutcome.Of(Int(1))),
            (yes, SideOutcome.Of(Bool(true)), SideOutcome.Of(Bool(true))),
            (k, SideOutcome.Of(Str("k")), SideOutcome.Of(Str("k"))),
            (s1, SideOutcome.Of(Str("a")), SideOutcome.Of(Str("a"))),
            (x1, SideOutcome.Of(Str("b")), SideOutcome.Of(Str("b"))),
            (divideByZero, SideOutcome.EvaluationError("division by zero"), SideOutcome.EvaluationError("division by zero")),
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

        GeneratedCase minimized = Minimizer.Minimize(original, candidate =>
        {
            asked.Add(candidate);
            return ReadsI1(candidate.Text, candidate.Bindings);
        });

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

        GeneratedCase minimized = Minimizer.Minimize(divergent, generated => generated.Expression is Call);

        Assert.Same(first, minimized.Expression);
    }

    [Fact]
    public void Minimization_KeepsTheCaseWhenNoChildDiverges()
    {
        Expr root = new Call(ProfileTables.Add, [I1, I1]);
        GeneratedCase divergent = Case(root, ("i1", Int(2)));

        GeneratedCase minimized = Minimizer.Minimize(divergent, generated => generated.Text == "i1 + i1");

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

        IOException thrown = Assert.Throws<IOException>(() => Minimizer.Minimize(divergent, _ => ++calls == 2 ? throw failure : true));

        Assert.Same(failure, thrown);
        Assert.Equal(2, calls);
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
        Assert.Empty(ProfileTables.ZoneDataPath);
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

    /// <summary>The number of the first generated case of a seed whose root is a call and which reads the bound variable i1.</summary>
    /// <param name="seed">The seed of the run.</param>
    internal static ulong CallReadingI1(ulong seed) => Enumerable.Range(0, 1000).Select(n => (ulong)n)
        .First(n => CaseGenerator.Case(seed, n) is { Expression: Call } generated && ReadsI1(generated.Text, generated.Bindings));

    private static IEnumerable<Expr> Nodes(Expr node) => Minimizer.Children(node).SelectMany(Nodes).Prepend(node);

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

    private static Value Int(long value) => new() { Int64Value = value };

    private static Value Bool(bool value) => new() { BoolValue = value };

    private static Value Str(string value) => new() { StringValue = value };

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
        public SideOutcome Evaluate(string expression, IReadOnlyDictionary<string, Value> bindings) => evaluate(expression, bindings);

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
