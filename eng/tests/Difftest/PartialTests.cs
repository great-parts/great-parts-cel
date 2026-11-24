using System;
using System.Collections.Generic;
using System.Linq;
using Cel.Expr;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;
using Great.Parts.Cel.Eng.Difftest.Compare;
using Great.Parts.Cel.Eng.Difftest.Corpus;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Difftest;

// The partial comparison of eng/difftest (docs/design/eng-difftest.md, "Partial evaluation"): the attributes of an expression and the
// patterns built from them, the kinds of divergence of unknown outcomes, and the search with partial sides that give fixed outcomes.
// The sides that evaluate partially are fakes; the library side and the oracle connection are in LibrarySideTests and
// OracleConnectionTests.
public sealed class PartialTests
{
    private static readonly Expr X1 = new Ident("x1");
    private static readonly Expr One = new IntLiteral(1, IntegerForm.DecimalDigits);
    private static readonly Expr Zero = new IntLiteral(0, IntegerForm.DecimalDigits);

    // ---- Attributes and patterns ----

    [Fact]
    public void Attribute_IsWrittenAsCelGoWritesATrail()
    {
        UnknownAttribute attribute = new(
            "x1",
            [
                new AttributeQualifier(QualifierKind.String, "a"),
                new AttributeQualifier(QualifierKind.String, "k z"),
                new AttributeQualifier(QualifierKind.Int, "1"),
                new AttributeQualifier(QualifierKind.Uint, "2"),
                new AttributeQualifier(QualifierKind.Bool, "true"),
                new AttributeQualifier(QualifierKind.Any, string.Empty),
            ]);
        UnknownAttribute asString = new("x1", [new AttributeQualifier(QualifierKind.String, "1")]);
        UnknownAttribute asInt = new("x1", [new AttributeQualifier(QualifierKind.Int, "1")]);

        Assert.Equal("x1.a[\"k z\"][1][2u][true][*]", attribute.ToString());
        Assert.NotEqual(asString.Key, asInt.Key);
        Assert.Equal(new UnknownAttribute("x1", [new AttributeQualifier(QualifierKind.Int, "1")]).Key, asInt.Key);
    }

    [Fact]
    public void Candidates_AreTheVariableThePathsOfTheExpressionAndTheirWildcardForms()
    {
        Expr key = new StringLiteral("k", new TextForm(Quote.Apostrophe, null, [Escape.Literal]));
        Expr x1ab = new PresenceTest(new Select(X1, "a"), "b");
        Expr msK = new Call(ProfileTables.Index, [new Ident("ms"), key]);
        Expr li1 = new Call(ProfileTables.Index, [new Ident("li"), One]);
        Expr expression = new Call(ProfileTables.LogicalAnd, [x1ab, new Call(ProfileTables.Equal, [msK, li1])]);

        IReadOnlyList<(string Variable, IReadOnlyList<UnknownAttribute> Candidates)> candidates = PartialPatterns.Candidates(expression);

        Assert.Equal(["x1", "ms", "li"], candidates.Select(variable => variable.Variable));
        Assert.Equal(["x1", "x1.a.b", "x1.a[*]", "x1.a.b[*]", "x1.a", "x1[*]"], candidates[0].Candidates.Select(candidate => candidate.ToString()));
        Assert.Equal(["ms", "ms.k", "ms[*]", "ms.k[*]"], candidates[1].Candidates.Select(candidate => candidate.ToString()));
        Assert.Equal(["li", "li[1]", "li[*]", "li[1][*]"], candidates[2].Candidates.Select(candidate => candidate.ToString()));
    }

    // An index by a key that is not a literal ends the path: the operand is still an attribute. A name that a macro declares hides
    // the variable of that name in its body.
    [Fact]
    public void Candidates_StopAtAKeyThatIsNotALiteralAndSkipANameThatAMacroHides()
    {
        Expr dynamicKey = new Call(ProfileTables.Index, [new Ident("li"), new Ident("i1")]);
        Expr hidden = new Comprehension(ProfileTables.Exists, new ListLiteral([One], TrailingComma: false), "i2", [new Call(ProfileTables.Greater, [new Ident("i2"), Zero])]);

        IReadOnlyList<(string Variable, IReadOnlyList<UnknownAttribute> Candidates)> candidates = PartialPatterns.Candidates(new Call(ProfileTables.LogicalAnd, [new Call(ProfileTables.Equal, [dynamicKey, One]), hidden]));

        Assert.Equal(["li", "i1"], candidates.Select(variable => variable.Variable));
        Assert.Equal(["li", "li[*]"], candidates[0].Candidates.Select(candidate => candidate.ToString()));
        Assert.Empty(PartialPatterns.Candidates(hidden));
    }

    // One pattern for each of one or two variables, each a candidate of its variable: so no access matches two patterns. The same case
    // gives the same patterns, and a case that reads no variable gives none.
    [Fact]
    public void Patterns_TakeOneCandidateOfOneOrTwoVariablesAndAreTheSameForTheSameCase()
    {
        Expr x1a = new Select(X1, "a");
        Expr expression = new Call(ProfileTables.Add, [new Call(ProfileTables.Add, [x1a, new Ident("i1")]), new Call(ProfileTables.Add, [new Ident("i2"), new Select(X1, "b")])]);
        GeneratedCase generated = SignatureTests.Case(expression);
        HashSet<string> candidates = PartialPatterns.Candidates(expression).SelectMany(variable => variable.Candidates).Select(candidate => candidate.Key).ToHashSet(StringComparer.Ordinal);
        List<int> counts = [];

        for (ulong number = 0; number < 200; number++)
        {
            IReadOnlyList<UnknownAttribute> patterns = PartialPatterns.For(SignatureTests.RunSeed, number, generated);
            counts.Add(patterns.Count);
            Assert.All(patterns, pattern => Assert.Contains(pattern.Key, candidates));
            Assert.Equal(patterns.Count, patterns.Select(pattern => pattern.Variable).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(patterns.Select(pattern => pattern.Key), PartialPatterns.For(SignatureTests.RunSeed, number, generated).Select(pattern => pattern.Key));
        }

        Assert.Contains(1, counts);
        Assert.Contains(2, counts);
        Assert.All(counts, count => Assert.InRange(count, 1, 2));
        Assert.Empty(PartialPatterns.For(SignatureTests.RunSeed, 0, SignatureTests.Case(One)));
    }

    // ---- Kinds, severities and the text of an outcome ----

    [Fact]
    public void UnknownOutcomes_AreComparedByTheirTrailsAndHaveTheirOwnKinds()
    {
        SideOutcome a = Unknown("x1");
        SideOutcome both = Unknown("x1", "i1");
        SideOutcome value = SideOutcome.Of(SignatureTests.Int(1));
        SideOutcome error = SideOutcome.EvaluationError("division by zero");

        Assert.Null(DivergenceKinds.Compare(a, Unknown("x1", "x1")));
        Assert.Null(DivergenceKinds.Compare(both, Unknown("i1", "x1")));
        Assert.Equal(DivergenceKind.UnknownMismatch, DivergenceKinds.Compare(a, both));
        Assert.Equal(DivergenceKind.WeUnknown, DivergenceKinds.Compare(a, value));
        Assert.Equal(DivergenceKind.WeUnknown, DivergenceKinds.Compare(a, error));
        Assert.Equal(DivergenceKind.WeKnown, DivergenceKinds.Compare(value, a));
        Assert.Equal(DivergenceKind.WeKnown, DivergenceKinds.Compare(error, a));
        Assert.Equal(["unknown-mismatch", "we-unknown", "we-known"], new[] { DivergenceKind.UnknownMismatch, DivergenceKind.WeUnknown, DivergenceKind.WeKnown }.Select(kind => kind.Name()));
        Assert.Equal("unknown i1, x1", both.Describe());
        Assert.Equal(NodeVerdict.AgreesInError, DivergenceSearch.VerdictOf(a, Unknown("x1")));
        Assert.Equal(NodeVerdict.Diverges, DivergenceSearch.VerdictOf(a, both));
    }

    // we-known is S1 when the library gives a value where cel-go gives unknown parts, and S2 when it gives an error; the other
    // kinds of partial evaluation are S2.
    [Theory]
    [InlineData("WeKnown", true, "S1")]
    [InlineData("WeKnown", false, "S2")]
    [InlineData("WeUnknown", true, "S2")]
    [InlineData("UnknownMismatch", false, "S2")]
    [InlineData("ValueMismatch", true, "S1")]
    public static void Severity_OfWeKnownDependsOnTheOutcomeOfTheLibrary(string kind, bool libraryValue, string expected)
    {
        SideOutcome library = libraryValue ? SideOutcome.Of(SignatureTests.Int(1)) : SideOutcome.EvaluationError("error");

        Assert.Equal(Enum.Parse<DivergenceSeverity>(expected), Enum.Parse<DivergenceKind>(kind).Severity(library));
    }

    // ---- The search ----

    [Fact]
    public void CheckPartial_GivesThePatternsToBothSidesAndAPartialSignatureThatIsNoCaseOfTheCorpus()
    {
        Expr unknown = new Select(X1, "a");
        Expr root = new Call(ProfileTables.Add, [unknown, One]);
        FakePartialSide library = new(new Dictionary<string, SideOutcome> { [Print(root)] = Unknown("x1.a"), [Print(unknown)] = Unknown("x1.a") });
        FakePartialSide oracle = new(new Dictionary<string, SideOutcome> { [Print(root)] = SideOutcome.Of(SignatureTests.Int(2)), [Print(unknown)] = Unknown("x1.a") });
        DivergenceSearch search = new(library, oracle, new SignatureTests.FakeRecorder(), KnownSignatures.Of());
        UnknownAttribute[] patterns = [new("x1", [new AttributeQualifier(QualifierKind.Any, string.Empty)])];

        Divergence? divergence = search.CheckPartial(SignatureTests.RunSeed, SignatureTests.CaseNumber, SignatureTests.Case(root, ("x1", SignatureTests.Str("v"))), patterns);
        PendingText pending = PendingFile.Build(search.NewSignatures);
        IReadOnlyList<Finding> findings = PendingFile.Findings(search.NewSignatures, pending);

        Assert.True(search.SupportsPartial);
        Assert.Equal("_+_(unknown, int) we-unknown", divergence?.Signature);
        Assert.All(new[] { library, oracle }, side =>
        {
            Assert.NotEmpty(side.Patterns);
            Assert.All(side.Patterns, given => Assert.Equal("x1[*]", Assert.Single(given)));
        });
        Assert.True(Assert.Single(search.NewSignatures).Partial);
        Assert.Equal(PendingFile.EmptyForm, pending.Text);
        FindingAssert.Exactly(
            findings,
            FindingAssert.Error(
                EngCodes.Eng820,
                PendingFile.Path,
                1,
                1,
                "new divergence signature '_+_(unknown, int) we-unknown' (S2): cel-go returns int 2, we return unknown x1.a; "
                + "partial evaluation of expression \"x1.a + 1\" with unknown data x1[*]; seed 20261104, case 1207"));
    }

    // The expected difference of the ADR: an error on the right of an unknown in a binary operator is an error here and unknown at
    // cel-go; the signature names the types of the operands, an unknown and an error, which the descent has compared as children.
    [Fact]
    public void CheckPartial_OfAnErrorOnTheRightOfAnUnknown_IsWeKnownWithErrorOperandsAndS2()
    {
        Expr unknown = new Select(X1, "a");
        Expr divideByZero = new Call(ProfileTables.Divide, [One, Zero]);
        Expr root = new Call(ProfileTables.Add, [unknown, divideByZero]);
        Dictionary<string, SideOutcome> libraryOutcomes = new()
        {
            [Print(root)] = SideOutcome.EvaluationError("division by zero"),
            [Print(unknown)] = Unknown("x1.a"),
            [Print(divideByZero)] = SideOutcome.EvaluationError("division by zero"),
        };
        Dictionary<string, SideOutcome> oracleOutcomes = new(libraryOutcomes) { [Print(root)] = Unknown("x1.a") };
        DivergenceSearch search = new(new FakePartialSide(libraryOutcomes), new FakePartialSide(oracleOutcomes), new SignatureTests.FakeRecorder(), KnownSignatures.Of());

        Divergence? divergence = search.CheckPartial(SignatureTests.RunSeed, SignatureTests.CaseNumber, SignatureTests.Case(root), [new UnknownAttribute("x1", [])]);

        Assert.Equal("_+_(unknown, error) we-known", divergence?.Signature);
        Assert.Equal(DivergenceSeverity.S2, divergence?.Severity);
    }

    [Fact]
    public void CheckPartial_OfAValueWhereCelGoIsUnknown_IsWeKnownS1AndAKnownSignatureIsNotNew()
    {
        Dictionary<string, SideOutcome> libraryOutcomes = new() { [Print(SignatureTests.I1)] = SideOutcome.Of(SignatureTests.Int(1)) };
        Dictionary<string, SideOutcome> oracleOutcomes = new() { [Print(SignatureTests.I1)] = Unknown("i1") };
        GeneratedCase generated = SignatureTests.Case(SignatureTests.I1, ("i1", SignatureTests.Int(1)));
        UnknownAttribute[] patterns = [new("i1", [])];
        DivergenceSearch fresh = new(new FakePartialSide(libraryOutcomes), new FakePartialSide(oracleOutcomes), new SignatureTests.FakeRecorder(), KnownSignatures.Of());
        DivergenceSearch known = new(new FakePartialSide(libraryOutcomes), new FakePartialSide(oracleOutcomes), new SignatureTests.FakeRecorder(), KnownSignatures.Of("ident(int) we-known"));

        Divergence? found = fresh.CheckPartial(SignatureTests.RunSeed, SignatureTests.CaseNumber, generated, patterns);
        _ = known.CheckPartial(SignatureTests.RunSeed, SignatureTests.CaseNumber, generated, patterns);

        Assert.Equal("ident(int) we-known", found?.Signature);
        Assert.Equal(DivergenceSeverity.S1, found?.Severity);
        Assert.Single(fresh.NewSignatures);
        Assert.Empty(known.NewSignatures);
        Assert.Equal(["ident(int) we-known"], known.KnownSignaturesFound);
    }

    [Fact]
    public void CheckPartial_OfSidesThatDoNotEvaluatePartially_IsAnInvalidOperation()
    {
        (SignatureTests.FakeSide library, SignatureTests.FakeSide oracle) = SignatureTests.Sides();
        DivergenceSearch search = new(library, oracle, new SignatureTests.FakeRecorder(), KnownSignatures.Of());

        Assert.False(search.SupportsPartial);
        _ = Assert.Throws<InvalidOperationException>(() => search.CheckPartial(SignatureTests.RunSeed, SignatureTests.CaseNumber, SignatureTests.Case(One), []));
    }

    // The registry writes one row for the cause "cel-go gives the unknown first argument of a call and not the error of the second"
    // (the run 633c7ad found it for 54 functions), and the row covers the signature of every call, but of no other node.
    [Theory]
    [InlineData("_+_(unknown, error) we-known", true)]
    [InlineData("contains(unknown, error) we-known", true)]
    [InlineData("math.bitOr(unknown, error) we-known", true)]
    [InlineData("_[_](unknown, error) we-known", true)]
    [InlineData("_&&_(unknown, error) we-known", false)]
    [InlineData("_||_(unknown, error) we-known", false)]
    [InlineData("list(unknown, error) we-known", false)]
    [InlineData("comprehension(unknown, error) we-known", false)]
    [InlineData("(unknown, error) we-known", false)]
    [InlineData("contains(unknown, int) we-known", false)]
    [InlineData("contains(error, unknown) we-known", false)]
    [InlineData("contains(unknown, error) we-unknown", false)]
    public void KnownSignatures_RowOfAnyCall_CoversTheCauseOfEveryCallOnly(string signature, bool known)
    {
        Assert.Equal(known, KnownSignatures.Of("*(unknown, error) we-known").Contains(signature));
    }

    [Fact]
    public void KnownSignatures_WithoutTheRowOfAnyCall_KnowsTheRowsOnly()
    {
        KnownSignatures known = KnownSignatures.Of("_+_(unknown, error) we-known");

        Assert.True(known.Contains("_+_(unknown, error) we-known"));
        Assert.False(known.Contains("contains(unknown, error) we-known"));
    }

    // ---- Helpers ----

    private static string Print(Expr node) => ExprPrinter.PrintCase(node);

    // An unknown outcome whose trails are variables with no qualifiers, or the path x1.a.
    private static SideOutcome Unknown(params string[] trails) => SideOutcome.Unknown(trails.Select(trail =>
    {
        string[] names = trail.Split('.');
        return new UnknownAttribute(names[0], [.. names.Skip(1).Select(name => new AttributeQualifier(QualifierKind.String, name))]);
    }));

    // A side that evaluates partially with given outcomes for the texts it knows, and int 0 for the others; it keeps the patterns it
    // was given. Its ordinary evaluation is the same table.
    private sealed class FakePartialSide(IReadOnlyDictionary<string, SideOutcome> outcomes) : ICaseSide, IPartialSide
    {
        public List<IReadOnlyList<string>> Patterns { get; } = [];

        public SideOutcome Evaluate(string expression, IReadOnlyDictionary<string, Value> bindings) => Outcome(expression);

        public SideOutcome EvaluatePartial(string expression, IReadOnlyDictionary<string, Value> bindings, IReadOnlyList<UnknownAttribute> patterns)
        {
            Patterns.Add([.. patterns.Select(pattern => pattern.ToString())]);
            return Outcome(expression);
        }

        public TimeSpan ZoneOffset(string zone, DateTimeOffset instant) => throw new NotSupportedException();

        private SideOutcome Outcome(string expression) => outcomes.TryGetValue(expression, out SideOutcome? outcome) ? outcome : SideOutcome.Of(SignatureTests.Int(0));
    }
}
