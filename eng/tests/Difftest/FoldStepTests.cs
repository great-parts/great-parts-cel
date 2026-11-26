using System;
using System.Linq;
using Cel.Expr;
using Great.Parts.Cel.Eng.Difftest.Compare;
using Great.Parts.Cel.Eng.Difftest.Corpus;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Difftest;

// The instance of the step of the fold of map, transformList, transformMapEntry and sortBy (docs/design/eng-difftest.md,
// "Minimization"): the sum of two lists of one instance of the transform (of the key for sortBy) each, the first that is unknown on
// both sides and the next that is an error, in which cel-go and the library part. The element of an instance is named by its first letter: u unknown, e error, v value, o another kind.
public sealed class FoldStepTests
{
    private const string TransformList = "transformList";
    private const string TransformMapEntry = "transformMapEntry";

    // ---- The instance ----

    [Theory]
    [InlineData("v1 u1 v2 e1 u2 e2", "[(u1)] + [(e1)]")]
    [InlineData("u1 u2 e1", "[(u1)] + [(e1)]")]
    [InlineData("u1 e1 e2", "[(u1)] + [(e1)]")]
    [InlineData("e1 u1", null)]
    [InlineData("e1 u1 e2", null)]
    [InlineData("u1 v1", null)]
    [InlineData("v1 e1", null)]
    [InlineData("u1 o1 e1", null)]
    [InlineData("u1 e1 o1", null)]
    [InlineData("u1", null)]
    public static void FoldStep_OfAListLiteral_IsTheFirstUnknownAndTheNextError_AndNoneWhenAnInstanceRulesItOut(string elements, string? expected)
    {
        ArgumentNullException.ThrowIfNull(elements);
        Expr macro = new Comprehension(ProfileTables.MapMacro, Literal(elements), "v", [new Ident("v")]);

        Assert.Equal(expected, Step(macro, _ => null, KindByName));
    }

    [Fact]
    public static void FoldStep_OfARangeInDyn_PutsTheElementsInDyn()
    {
        Expr range = new Call(ProfileTables.Dyn, [Literal("u1 e1")]);
        Expr macro = new Comprehension(ProfileTables.MapMacro, range, "v", [new Ident("v")]);

        Assert.Equal("[dyn(u1)] + [dyn(e1)]", Step(macro, _ => null, KindByName));
    }

    [Fact]
    public static void FoldStep_OfLiteralsOfTwoTypes_TriesTheElementsInDynWhenThoseInParenthesesDoNotCompile()
    {
        Expr range = new ListLiteral([new IntLiteral(1, IntegerForm.DecimalDigits), SignatureTests.Quoted("a")], TrailingComma: false);
        Expr macro = new Comprehension(ProfileTables.MapMacro, range, "v", [new Ident("v")]);

        // The elements in parentheses are no step: a side does not compile them, as the static types of the literals have no join.
        Assert.Equal(
            "[dyn(1)] + [dyn('a')]",
            Step(
                macro,
                _ => null,
                node => ExprPrinter.Print(node) switch
                {
                    "dyn(1)" => StepKind.Unknown,
                    "dyn('a')" => StepKind.Error,
                    _ => StepKind.Other,
                }));
    }

    [Fact]
    public static void FoldStep_OfLiteralsOfOneType_HasNoElementInDyn()
    {
        Expr range = new ListLiteral([new IntLiteral(1, IntegerForm.DecimalDigits), new IntLiteral(2, IntegerForm.DecimalDigits)], TrailingComma: false);
        Expr macro = new Comprehension(ProfileTables.MapMacro, range, "v", [new Ident("v")]);

        Assert.Null(Step(
            macro,
            _ => null,
            node => ExprPrinter.Print(node) switch
            {
                "dyn(1)" => StepKind.Unknown,
                "dyn(2)" => StepKind.Error,
                _ => StepKind.Other,
            }));
    }

    [Fact]
    public static void FoldStep_OfTransformList_PutsTheElementInPlaceOfTheSecondVariable()
    {
        Expr macro = new Comprehension(TransformList, Literal("u1 e1"), "i", [new Ident("v")]) { SecondVariable = "v" };

        Assert.Equal("[(u1)] + [(e1)]", Step(macro, _ => null, KindByName));
    }

    [Fact]
    public static void FoldStep_OfARangeThatIsNoLiteral_IndexesItsListValue()
    {
        Expr macro = new Comprehension(ProfileTables.MapMacro, new Ident("lx"), "v", [new Ident("v")]);
        Value list = SignatureTests.ListOf(SignatureTests.Int(1), SignatureTests.Int(2), SignatureTests.Int(3));

        Assert.Equal(
            "[lx[0]] + [lx[1]]",
            Step(
                macro,
                _ => list,
                node => ExprPrinter.Print(node) switch
                {
                    "lx[0]" => StepKind.Unknown,
                    "lx[1]" => StepKind.Error,
                    _ => StepKind.Value,
                }));
    }

    [Fact]
    public static void FoldStep_OfARangeWhoseValueIsNoListOfTwoElements_IsNone()
    {
        Expr macro = new Comprehension(ProfileTables.MapMacro, new Ident("lx"), "v", [new Ident("v")]);
        Value map = SignatureTests.Map((SignatureTests.Int(1), SignatureTests.Int(2)), (SignatureTests.Int(2), SignatureTests.Int(3)));

        foreach (Value? value in new Value?[] { null, SignatureTests.Int(1), SignatureTests.ListOf(SignatureTests.Int(1)), map })
        {
            Assert.Null(Step(macro, _ => value, node => ExprPrinter.Print(node).EndsWith("[0]", StringComparison.Ordinal) ? StepKind.Unknown : StepKind.Error));
        }
    }

    // cel-go keeps no order of the entries of a map, so the order of two elements is none to rely on.
    [Fact]
    public static void FoldStep_OfAMapLiteral_IsNone()
    {
        Expr range = new MapLiteral(
            [
                new MapEntry(new IntLiteral(1, IntegerForm.DecimalDigits), new Ident("u1")),
                new MapEntry(new IntLiteral(2, IntegerForm.DecimalDigits), new Ident("e1")),
            ],
            TrailingComma: false);
        Expr macro = new Comprehension(ProfileTables.MapMacro, range, "v", [new Ident("v")]);

        Assert.Null(Step(macro, _ => null, KindByName));
    }

    // An optional element leaves no element in the list when the optional is empty, and the descent does not know which.
    [Fact]
    public static void FoldStep_OfAListLiteralWithAnOptionalElement_IsNone()
    {
        Expr range = Literal("u1 e1") with { OptionalPositions = [0] };
        Expr macro = new Comprehension(ProfileTables.MapMacro, range, "v", [new Ident("v")]);

        Assert.Null(Step(macro, _ => null, KindByName));
    }

    [Theory]
    [InlineData("filter")]
    [InlineData("all")]
    [InlineData("exists")]
    [InlineData("exists_one")]
    [InlineData("transformMap")]
    [InlineData("optMap")]
    public static void FoldStep_OfAnotherMacro_IsNone(string name)
    {
        Expr macro = new Comprehension(name, Literal("u1 e1"), "v", [new Ident("v")]);

        Assert.Null(Step(macro, _ => null, KindByName));
        Assert.Null(Step(new Ident("u1"), _ => null, KindByName));
    }

    // transformMap is no macro of the step: its fold calls @mapInsert with three arguments, for which cel-go gives the error before
    // the unknown, so the unknown and the error elements agree with the library and the instance would not diverge. The shape is
    // the same as that of transformMapEntry that has the instance, with both variables and an unknown element before an error one.
    [Fact]
    public static void FoldStep_OfTransformMap_IsNone_WhereTransformMapEntryHasTheInstance()
    {
        Expr transformMap = new Comprehension("transformMap", Literal("u1 e1"), "i", [new Ident("v")]) { SecondVariable = "v" };
        Expr transformMapEntry = new Comprehension(TransformMapEntry, Literal("u1 e1"), "i", [new Ident("v")]) { SecondVariable = "v" };

        Assert.Null(Step(transformMap, _ => null, KindByName));
        Assert.Equal("[(u1)] + [(e1)]", Step(transformMapEntry, _ => null, KindByName));
    }

    // The case 1611731 of seed 20261126: lx.transformMapEntry(i, e, {…: e ? ms.f : x2}) over a list literal; the unknown x2 for one
    // element is followed by an error for the next. The elements stand in place of the second variable, the positions in place of the first.
    [Fact]
    public static void FoldStep_OfTransformMapEntry_PutsTheElementInPlaceOfTheSecondVariable()
    {
        Expr entry = new MapLiteral([new MapEntry(new Ident("i"), new Ident("e"))], TrailingComma: false);
        Expr macro = new Comprehension(TransformMapEntry, Literal("u1 e1 e2"), "i", [entry]) { SecondVariable = "e" };

        Assert.Equal("[{(0): (u1)}] + [{(1): (e1)}]", Step(macro, _ => null, node => KindByName(((MapLiteral)node).Entries[0].Value)));
    }

    // The same over a range that is no literal, whose elements are read as range[i] by their positions.
    [Fact]
    public static void FoldStep_OfTransformMapEntryOverAListValue_IndexesTheList()
    {
        Expr entry = new MapLiteral([new MapEntry(new Ident("i"), new Ident("e"))], TrailingComma: false);
        Expr macro = new Comprehension(TransformMapEntry, new Ident("lx"), "i", [entry]) { SecondVariable = "e" };
        Value list = SignatureTests.ListOf(SignatureTests.Int(1), SignatureTests.Int(2), SignatureTests.Int(3));

        Assert.Equal(
            "[{0: lx[0]}] + [{1: lx[1]}]",
            Step(
                macro,
                _ => list,
                node => ExprPrinter.Print(node) switch
                {
                    "{0: lx[0]}" => StepKind.Unknown,
                    "{1: lx[1]}" => StepKind.Error,
                    _ => StepKind.Value,
                }));
    }

    // The filter of transformMapEntry (the form of three arguments) selects the elements as the predicate of map does.
    [Theory]
    [InlineData("true true", "[(u1)] + [(e1)]")]
    [InlineData("true false", null)]
    [InlineData("none true", null)]
    public static void FoldStep_OfTransformMapEntryWithAFilter_TakesTheSelectedElementsOnly(string answers, string? expected)
    {
        ArgumentNullException.ThrowIfNull(answers);
        string[] flags = answers.Split(' ');
        Expr filter = new Call(ProfileTables.Greater, [new Ident("v"), new IntLiteral(0, IntegerForm.DecimalDigits)]);
        Expr macro = new Comprehension(TransformMapEntry, Literal("u1 e1"), "i", [filter, new Ident("v")]) { SecondVariable = "v" };

        Assert.Equal(
            expected,
            Step(
                macro,
                node => ExprPrinter.Print(node) switch
                {
                    "(u1) > 0" => Flag(flags[0]),
                    "(e1) > 0" => Flag(flags[1]),
                    _ => null,
                },
                KindByName));
    }

    // transformMapEntry over a map has no order that cel-go keeps, so it gives no instance, as map does.
    [Fact]
    public static void FoldStep_OfTransformMapEntryOverAMap_IsNone()
    {
        Expr range = new MapLiteral(
            [
                new MapEntry(new IntLiteral(1, IntegerForm.DecimalDigits), new Ident("u1")),
                new MapEntry(new IntLiteral(2, IntegerForm.DecimalDigits), new Ident("e1")),
            ],
            TrailingComma: false);
        Expr macro = new Comprehension(TransformMapEntry, range, "k", [new Ident("v")]) { SecondVariable = "v" };
        Value map = SignatureTests.Map((SignatureTests.Int(1), SignatureTests.Int(2)), (SignatureTests.Int(2), SignatureTests.Int(3)));
        Expr variable = new Comprehension(TransformMapEntry, new Ident("mx"), "k", [new Ident("v")]) { SecondVariable = "v" };

        Assert.Null(Step(macro, _ => null, KindByName));
        Assert.Null(Step(variable, _ => map, KindByName));
    }

    // The case 1517069 of seed 2026112602: [a == b, true].sortBy(v, x2 && (v || test.r.contains(s1))) with s1 unknown: the key of the
    // first element is unknown, that of the second an error. The keys are made by map inside sortBy, so the step is that of map.
    [Fact]
    public static void FoldStep_OfSortBy_IsTheFirstUnknownKeyAndTheNextErrorKey()
    {
        Expr macro = new Comprehension(ProfileTables.SortByMacro, Literal("u1 e1 v1"), "v", [new Call(ProfileTables.LogicalAnd, [new Ident("x2"), new Ident("v")])]);

        Assert.Equal(
            "[x2 && (u1)] + [x2 && (e1)]",
            Step(macro, _ => null, node => KindByName(((Call)node).Args[1])));
    }

    [Fact]
    public static void FoldStep_OfSortByOverAListValue_IndexesTheList()
    {
        Expr macro = new Comprehension(ProfileTables.SortByMacro, new Ident("lx"), "v", [new Call(ProfileTables.Negate, [new Ident("v")])]);
        Value list = SignatureTests.ListOf(SignatureTests.Int(1), SignatureTests.Int(2));

        Assert.Equal(
            "[-lx[0]] + [-lx[1]]",
            Step(
                macro,
                _ => list,
                node => ExprPrinter.Print(node) switch
                {
                    "-lx[0]" => StepKind.Unknown,
                    "-lx[1]" => StepKind.Error,
                    _ => StepKind.Value,
                }));
    }

    [Fact]
    public static void FoldStep_OfSortByWhoseErrorKeyComesFirst_IsNone()
    {
        Expr macro = new Comprehension(ProfileTables.SortByMacro, Literal("e1 u1"), "v", [new Ident("v")]);

        Assert.Null(Step(macro, _ => null, KindByName));
    }

    // The predicate of map selects the elements: an element that it leaves out adds nothing to the fold, and a predicate that gives no
    // value of bool on the library (an error, unknown parts) makes the step of the element no plain append.
    [Theory]
    [InlineData("true true", "[(u1)] + [(e1)]")]
    [InlineData("true false", null)]
    [InlineData("false true", null)]
    [InlineData("true none", null)]
    [InlineData("none true", null)]
    public static void FoldStep_OfMapWithAPredicate_TakesTheSelectedElementsOnly(string answers, string? expected)
    {
        ArgumentNullException.ThrowIfNull(answers);
        string[] flags = answers.Split(' ');
        Expr predicate = new Call(ProfileTables.Greater, [new Ident("v"), new IntLiteral(0, IntegerForm.DecimalDigits)]);
        Expr macro = new Comprehension(ProfileTables.MapMacro, Literal("u1 e1"), "v", [predicate, new Ident("v")]);

        Assert.Equal(
            expected,
            Step(
                macro,
                node => ExprPrinter.Print(node) switch
                {
                    "(u1) > 0" => Flag(flags[0]),
                    "(e1) > 0" => Flag(flags[1]),
                    _ => null,
                },
                KindByName));
    }

    // ---- The search ----

    // The case 2600357 of seed 20261124: dyn([true, bool]).map(v, (v ? x1 : …).getDayOfMonth()) with x1 unknown. The body for true is
    // unknown on both sides and the body for bool is an error on both, so each agrees; cel-go adds the unknown accumulator and the list
    // of the error, and returns the unknown, where the library returns the error. The sum has the signature _+_(unknown, error) we-known,
    // which the registry knows for every call; with it the case is the sum, without it the case stays the macro.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static void Macro_WhoseFoldStepDivergesBySignature_IsMinimizedToTheStepOnlyWhenItIsKnown(bool registered)
    {
        Expr u1 = new Ident("u1");
        Expr e1 = new Ident("e1");
        Expr range = new ListLiteral([u1, e1], TrailingComma: false);
        Expr root = new Comprehension(ProfileTables.MapMacro, range, "v", [new Ident("v")]);
        Expr unknownList = new ListLiteral([SignatureTests.Parenthesized(u1)], TrailingComma: false);
        Expr errorList = new ListLiteral([SignatureTests.Parenthesized(e1)], TrailingComma: false);
        Expr step = new Call(ProfileTables.Add, [unknownList, errorList]);
        SideOutcome error = SideOutcome.EvaluationError("no such overload: _?_:_(type)");
        (SignatureTests.FakeSide library, SignatureTests.FakeSide oracle) = MapOverUnknownAndError(
            root,
            range,
            error,
            (unknownList, UnknownX1, UnknownX1),
            (errorList, error, error),
            (step, error, UnknownX1));
        DivergenceSearch search = new(library, oracle, new SignatureTests.FakeRecorder(), registered ? KnownSignatures.Of("*(unknown, error) we-known") : KnownSignatures.Of());

        Divergence? divergence = search.Check(SignatureTests.RunSeed, SignatureTests.CaseNumber, SignatureTests.Case(root));

        Assert.Equal(registered ? "_+_(unknown, error) we-known" : "comprehension(list) we-known", divergence?.Signature);
        Assert.Equal(registered ? ExprPrinter.Print(step) : ExprPrinter.Print(root), divergence?.Case.Text);
        Assert.Equal(registered ? 0 : 1, search.NewSignatures.Count);
        Assert.Equal(registered ? 1 : 0, search.KnownSignaturesFound.Count);
    }

    // An error before the unknown is the result of both sides, so the macro has no step, and its divergence stays its own.
    [Fact]
    public static void Macro_WhoseErrorComesBeforeTheUnknown_HasNoFoldStep_AndStaysTheMacro()
    {
        Expr u1 = new Ident("u1");
        Expr e1 = new Ident("e1");
        Expr range = new ListLiteral([e1, u1], TrailingComma: false);
        Expr root = new Comprehension(ProfileTables.MapMacro, range, "v", [new Ident("v")]);
        Expr step = new Call(
            ProfileTables.Add,
            [
                new ListLiteral([SignatureTests.Parenthesized(e1)], TrailingComma: false),
                new ListLiteral([SignatureTests.Parenthesized(u1)], TrailingComma: false),
            ]);
        SideOutcome error = SideOutcome.EvaluationError("no such overload");
        (SignatureTests.FakeSide library, SignatureTests.FakeSide oracle) = MapOverUnknownAndError(root, range, error, (step, error, UnknownX1));
        DivergenceSearch search = new(library, oracle, new SignatureTests.FakeRecorder(), KnownSignatures.Of("*(unknown, error) we-known"));

        Divergence? divergence = search.Check(SignatureTests.RunSeed, SignatureTests.CaseNumber, SignatureTests.Case(root));

        Assert.Equal("comprehension(list) we-known", divergence?.Signature);
        Assert.Equal(root, divergence?.Case.Expression);
        Assert.DoesNotContain(ExprPrinter.Print(step), library.Evaluated);
        Assert.Single(search.NewSignatures);
    }

    // ---- Helpers ----

    // The outcome of the unknown x1, the same on both sides.
    private static readonly SideOutcome UnknownX1 = SideOutcome.Unknown([new UnknownAttribute("x1", [])]);

    // The sides of a map over a list of the unknown u1 and the error e1: the macro is the error on the library and the unknown on
    // cel-go, the range gives two elements, the body of each element agrees, and the rest are the outcomes of the step and its parts.
    private static (SignatureTests.FakeSide Library, SignatureTests.FakeSide Oracle) MapOverUnknownAndError(
        Expr root,
        Expr range,
        SideOutcome error,
        params (Expr Node, SideOutcome Library, SideOutcome Oracle)[] step)
    {
        SideOutcome elements = SideOutcome.Of(SignatureTests.ListOf(SignatureTests.Int(1), SignatureTests.Int(2)));
        return SignatureTests.Sides(
        [
            (root, error, UnknownX1),
            (range, elements, elements),
            (SignatureTests.Parenthesized(new Ident("u1")), UnknownX1, UnknownX1),
            (SignatureTests.Parenthesized(new Ident("e1")), error, error),
            .. step,
        ]);
    }

    // The text of the step of a macro, null when it has none.
    private static string? Step(Expr node, Func<Expr, Value?> rangeValue, Func<Expr, StepKind> kindOf) =>
        Minimizer.FoldStep(node, rangeValue, kindOf) is { } step ? ExprPrinter.Print(step) : null;

    // A list literal of identifiers, one for each name of the text.
    private static ListLiteral Literal(string names) =>
        new([.. names.Split(' ').Select(name => (Expr)new Ident(name))], TrailingComma: false);

    // The kind of an instance by the first letter of its element: u unknown, e error, v value, any other letter another kind.
    private static StepKind KindByName(Expr node) => ExprPrinter.Print(node).Replace("dyn(", string.Empty, StringComparison.Ordinal).TrimStart('(')[0] switch
    {
        'u' => StepKind.Unknown,
        'e' => StepKind.Error,
        'v' => StepKind.Value,
        _ => StepKind.Other,
    };

    // The value of a predicate that the library gives: true, false, or none for an error or unknown parts.
    private static Value? Flag(string answer) => answer switch
    {
        "true" => new Value { BoolValue = true },
        "false" => new Value { BoolValue = false },
        _ => null,
    };
}
