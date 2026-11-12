using Cel.Expr;
using Great.Parts.Cel.Eng.Difftest.Compare;
using Great.Parts.Cel.Eng.Difftest.Corpus;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;
using Xunit;
using static Great.Parts.Cel.Eng.Tests.Difftest.SignatureTests;
using Any = Google.Protobuf.WellKnownTypes.Any;
using Duration = Google.Protobuf.WellKnownTypes.Duration;

namespace Great.Parts.Cel.Eng.Tests.Difftest;

// The minimization under the macros of two variables and the signature of the negation of a duration (docs/design/eng-difftest.md,
// "Minimization", "Known signatures"), on the fake sides and the helpers of SignatureTests, whose class coupling is at its limit.
public sealed class ExtensionMinimizationTests
{
    // The element of a list literal and its position replace the two variables at once, in parentheses: a known divergence of a body that
    // reads the element is minimized to the body with the element in parentheses.
    [Fact]
    public void TwoVariableMacro_OverAListLiteral_WithAKnownDivergenceInABodyThatReadsTheElement_IsMinimizedToTheBodyWithTheElement()
    {
        Expr d1 = new Ident("d1");
        Expr range = new ListLiteral([I1], TrailingComma: false);

        AssertMinimizedToKnown(
            new Comprehension(ProfileTables.All, range, "i", [new Call(ProfileTables.Greater, [new Ident("e"), d1])]) { SecondVariable = "e" },
            new Call(ProfileTables.Greater, [Parenthesized(I1), d1]),
            SideOutcome.EvaluationError("no such overload: _>_(bytes, double)"),
            "_>_(bytes, double) we-error",
            "(i1) > d1",
            Agree(Parenthesized(I1), Bytes(0x61)),
            Agree(d1, Dbl(double.NaN)),
            Agree(range, ListOf(Bytes(0x61))));
    }

    // The key and the value of a map literal replace the variables, and a string literal gets one more instance in dyn(): the validator
    // of regular expression literals of cel-go reads the key in parentheses as a pattern, so only the instance in dyn() diverges.
    [Fact]
    public void TwoVariableMacro_OverAMapLiteral_WithAKnownDivergenceInABodyThatReadsTheKey_IsMinimizedToTheBodyWithTheKeyInDyn()
    {
        Expr key = new Call(ProfileTables.Dyn, [Quoted("k")]);
        Expr range = new MapLiteral([new MapEntry(Quoted("k"), I1)], TrailingComma: false);
        Expr root = new Comprehension(ProfileTables.All, range, "i", [MatchesOf(new Ident("i"))]) { SecondVariable = "e" };

        AssertMinimizedToMatches(root, [root, MatchesOf(key)], "matches(dyn('k'), '(')", Agree(key, Str("k")), Agree(range, Map((Str("k"), Int(0)))));
    }

    // A range that is a variable gives the position and the element at that position, read by index.
    [Fact]
    public void TwoVariableMacro_OverAListVariable_WithAKnownDivergenceInABodyThatReadsTheElement_IsMinimizedToTheBodyWithTheIndex()
    {
        Expr ls = new Ident("ls");
        Expr first = Index(ls, 0);
        Expr root = new Comprehension(ProfileTables.Exists, ls, "i", [MatchesOf(new Ident("e"))]) { SecondVariable = "e" };

        AssertMinimizedToMatches(
            root,
            [root, MatchesOf(first)],
            "matches(ls[0], '(')",
            Agree(ls, ListOf(Str("a"), Str("b"))),
            Agree(first, Str("a")));
    }

    // A map variable gives dyn(key) and the value at the key, in the order of the library.
    [Fact]
    public void TwoVariableMacro_OverAMapVariable_WithAKnownDivergenceInABodyThatReadsTheKey_IsMinimizedToTheBodyWithTheKeyInDyn()
    {
        Expr ms = new Ident("ms");
        Expr key = new Call(ProfileTables.Dyn, [Quoted("a")]);
        Expr root = new Comprehension(ProfileTables.Exists, ms, "i", [MatchesOf(new Ident("i"))]) { SecondVariable = "e" };

        AssertMinimizedToMatches(root, [root, MatchesOf(key)], "matches(dyn('a'), '(')", Agree(ms, Map((Str("a"), Int(1)))), Agree(key, Str("a")));
    }

    // A range that is no list or map gives each pair of the zero literals of the profile, in order: the first pair is '' and ''.
    [Fact]
    public void TwoVariableMacro_OverAnUnboundVariable_WithAKnownDivergenceInABodyThatReadsAVariable_IsMinimizedToTheBodyWithZeroLiterals()
    {
        Expr ls = new Ident("ls");
        SideOutcome unbound = SideOutcome.EvaluationError("no such attribute(s): ls");
        Expr root = new Comprehension(ProfileTables.ExistsOne, ls, "i", [MatchesOf(new Ident("e"))]) { SecondVariable = "e" };

        AssertMinimizedToMatches(root, [root, MatchesOf(Quoted(string.Empty))], "matches('', '(')", (ls, unbound, unbound));
    }

    // A literal without elements gives the same pairs.
    [Fact]
    public void TwoVariableMacro_OverAnEmptyLiteral_WithAKnownDivergenceInABodyThatReadsAVariable_IsMinimizedToTheBodyWithZeroLiterals()
    {
        Expr range = new ListLiteral([], TrailingComma: false);
        Expr root = new Comprehension(ProfileTables.All, range, "i", [MatchesOf(new Ident("e"))]) { SecondVariable = "e" };

        AssertMinimizedToMatches(root, [root, MatchesOf(Quoted(string.Empty))], "matches('', '(')", Agree(range, ListOf()));
    }

    // The negation of a duration, which cel-go gives for an operand whose static type is not known, is named by the function, since no
    // overload of the profile has a duration parameter, and is the signature of the registry.
    [Fact]
    public void NegationOfADuration_IsNamedByTheFunctionAndKnown()
    {
        Expr operand = new Ident("x1");
        Expr root = new Call(ProfileTables.Negate, [operand]);
        Value duration = new() { ObjectValue = Any.Pack(new Duration { Seconds = -1 }) };
        (FakeSide library, FakeSide oracle) = Sides(
            (root, SideOutcome.EvaluationError("no such overload: -_(duration)"), SideOutcome.Of(duration)),
            Agree(operand, duration));
        DivergenceSearch search = new(library, oracle, new FakeRecorder(), KnownSignatures.Of("-_(duration) we-error"));

        Divergence? divergence = search.Check(RunSeed, CaseNumber, Case(root));

        Assert.Equal("-_(duration) we-error", divergence?.Signature);
        Assert.Empty(search.NewSignatures);
    }
}
