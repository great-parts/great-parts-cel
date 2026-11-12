using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cel.Expr;
using Great.Parts.Cel.Eng.Difftest.Compare;
using Great.Parts.Cel.Eng.Difftest.Corpus;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;
using Great.Parts.Cel.Eng.Tests.Support;
using Great.Parts.Cel.Values;
using Xunit;
using NullValue = Google.Protobuf.WellKnownTypes.NullValue;

namespace Great.Parts.Cel.Eng.Tests.Difftest;

// The profile of the set optional (docs/design/eng-difftest.md, "Profile of the set optional"): the manifest and the table, the
// forms of the generator and where an optional node may stand, the printer, the optional value in Compare and in the library side, and
// the signatures and the minimizer on the nodes of the set.
public sealed class OptionalProfileTests
{
    // The cases per seed of the tests that read every case.
    private const int Cases = 1000;

    // The cases per seed of the tests of forms that are rarer.
    private const int ManyCases = Cases * 10;

    // The most cases per seed that the search for the rarest forms reads; it ends as soon as every form has occurred.
    private const int SearchLimit = 150_000;

    // The outcome of both fake sides for an expression that a test does not name.
    private static readonly SideOutcome Agreed = SideOutcome.Of(IntValue(0));

    // The overloads of the set in the order of the table of cel-go v0.32.0 (cel/library.go) and of the page of the set.
    private static readonly string[] Overloads =
    [
        "optional_of", "optional_ofNonZeroValue", "optional_none", "optional_value", "optional_hasValue", "optional_or_optional",
        "optional_orValue_value", "select_optional_field", "list_optindex_optional_int", "optional_list_optindex_optional_int",
        "map_optindex_optional_value", "optional_map_optindex_optional_value", "optional_list_index_int", "optional_map_index_value",
        "list_first", "list_last", "optional_unwrap", "optional_unwrapOpt",
    ];

    // ---- The manifest and the table ----

    [Fact]
    public void ManifestOfTheRepository_NamesTheSetOptionalWithTheVersion2AndNoBlock()
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot.Find(), ProfileTables.ManifestPath)));
        List<(string Name, string Version)> declared =
        [
            .. manifest.RootElement.GetProperty("extensions").EnumerateArray()
                .Select(extension => (extension.GetProperty("name").GetString()!, extension.GetProperty("version").GetString()!)),
        ];

        Assert.Equal(("optional", "2"), declared[^1]);
        Assert.Equal(9, declared.Count);
        Assert.Equal(("optional", "2"), ProfileTables.Extensions[^1]);
        Assert.DoesNotContain(declared, set => set.Name == "block");
        Assert.DoesNotContain(ProfileTables.Extensions, set => set.Name == "block");
    }

    [Fact]
    public void TableOfTheSet_HasThe18OverloadsOfCelGoAndNoOther()
    {
        List<ProfileOverload> rows = [.. ProfileTables.ExtensionOverloads.Where(overload => overload.Set == ProfileTables.OptionalSet)];

        Assert.Equal(Overloads, rows.Select(row => row.Id));
        Assert.Equal(Overloads.Length, rows.Select(row => row.Id).Distinct().Count());
        Assert.All(rows, row => Assert.Contains(ProfileTables.Overloads, overload => overload.Id == row.Id && overload.Function == row.Function));

        // Four are called globally and the others on a receiver or as an operator; the printer names a global one by its function.
        Assert.Equal(
            ["optional.none", "optional.of", "optional.ofNonZeroValue", "optional.unwrap"],
            rows.Where(row => !row.Receiver && !row.Function.StartsWith('_')).Select(row => row.Function).Distinct().Order(StringComparer.Ordinal));
        Assert.All(rows.Where(row => !row.Receiver && !row.Function.StartsWith('_')), row => Assert.True(ProfileTables.IsExtensionGlobal(row.Function)));
        Assert.Equal(
            [ProfileTables.OptionalIndex, ProfileTables.OptionalIndex, ProfileTables.OptionalIndex, ProfileTables.OptionalIndex],
            rows.Where(row => row.Function == ProfileTables.OptionalIndex).Select(row => row.Function));
    }

    [Fact]
    public void StaticTypesOfOptionals_AreAssignableByTheirHeldTypes()
    {
        StaticType optionalInt = StaticType.OptionalOf(StaticType.Of(ProfileType.IntType));
        StaticType optionalString = StaticType.OptionalOf(StaticType.Of(ProfileType.StringType));
        StaticType optionalUnknown = StaticType.OptionalOf(null);

        Assert.Equal(Compatibility.Yes, StaticType.Compatible(optionalInt, StaticType.OptionalOf(StaticType.Of(ProfileType.IntType))));
        Assert.Equal(Compatibility.No, StaticType.Compatible(optionalInt, optionalString));
        Assert.Equal(Compatibility.Maybe, StaticType.Compatible(optionalInt, optionalUnknown));
        Assert.Equal(Compatibility.Yes, StaticType.Compatible(optionalInt, StaticType.OptionalOf(StaticType.Dyn)));
        Assert.Equal(Compatibility.No, StaticType.Compatible(optionalInt, StaticType.Of(ProfileType.IntType)));
        Assert.Equal(optionalInt, StaticType.Meet(optionalInt, StaticType.OptionalOf(StaticType.Dyn)));
        Assert.Equal("optional_type", ProfileType.OptionalType.Name());
    }

    // ---- The printer ----

    [Fact]
    public void NodesOfTheSet_PrintAsTheSyntaxWritesThem()
    {
        Expr x1 = new Ident("x1");
        Expr nested = new MapLiteral([new MapEntry(Quoted("a"), new MapLiteral([new MapEntry(Quoted("b"), Lit(1))], TrailingComma: false))], TrailingComma: false);
        Expr optionalSelect = new Select(x1, "a") { Optional = true };
        Expr list = new ListLiteral([Of(Lit(1)), Lit(2), None()], TrailingComma: false) { OptionalPositions = [0, 2] };
        Expr map = new MapLiteral([new MapEntry(Quoted("a"), Of(Lit(1))) { Optional = true }, new MapEntry(Quoted("b"), Lit(2))], TrailingComma: false);
        Expr plain = new ListLiteral([Of(Lit(1))], TrailingComma: false);
        Expr conditional = new Call(ProfileTables.Conditional, [new BoolLiteral(true), Of(Lit(1)), None()]);
        Expr optMap = new Comprehension(ProfileTables.OptMapMacro, Of(Lit(1)), "v", [new Call(ProfileTables.Add, [new Ident("v"), Lit(1)])]);

        Assert.Equal("x1.?a", ExprPrinter.Print(optionalSelect));
        Assert.Equal("x1.?a.b", ExprPrinter.Print(new Select(optionalSelect, "b")));
        Assert.Equal("x1.?a.?b", ExprPrinter.Print(new Select(optionalSelect, "b") { Optional = true }));
        Assert.Equal("{'a': {'b': 1}}.?a.b", ExprPrinter.Print(new Select(new Select(nested, "a") { Optional = true }, "b")));
        Assert.Equal("has(x1.?a.b)", ExprPrinter.Print(new PresenceTest(optionalSelect, "b")));
        Assert.Equal("li[?0]", ExprPrinter.Print(new Call(ProfileTables.OptionalIndex, [new Ident("li"), Lit(0)])));
        Assert.Equal("optional.of(1)[?0]", ExprPrinter.Print(new Call(ProfileTables.OptionalIndex, [Of(Lit(1)), Lit(0)])));
        Assert.Equal("optional.of(1)[0]", ExprPrinter.Print(new Call(ProfileTables.Index, [Of(Lit(1)), Lit(0)])));
        Assert.Equal("[?optional.of(1), 2, ?optional.none()]", ExprPrinter.Print(list));
        Assert.Equal("{?'a': optional.of(1), 'b': 2}", ExprPrinter.Print(map));
        Assert.Equal("[optional.of(1)]", ExprPrinter.Print(plain));
        Assert.Equal("optional.unwrap([optional.of(1)])", ExprPrinter.Print(new Call("optional.unwrap", [plain])));
        Assert.Equal("[optional.of(1)].unwrapOpt()", ExprPrinter.Print(new Call("unwrapOpt", [plain]) { Receiver = true }));
        Assert.Equal("optional.of(1).hasValue()", ExprPrinter.Print(new Call("hasValue", [Of(Lit(1))]) { Receiver = true }));
        Assert.Equal("optional.none().orValue(2)", ExprPrinter.Print(new Call("orValue", [None(), Lit(2)]) { Receiver = true }));
        Assert.Equal("(true ? optional.of(1) : optional.none()).hasValue()", ExprPrinter.Print(new Call("hasValue", [conditional]) { Receiver = true }));
        Assert.Equal("optional.of(1).optMap(v, v + 1)", ExprPrinter.Print(optMap));
        Assert.Equal("type(optional.of(1)) == optional_type", ExprPrinter.Print(new Call(ProfileTables.Equal, [new Call(ProfileTables.TypeOf, [Of(Lit(1))]), new Ident(ProfileTables.OptionalTypeIdentifier)])));
    }

    // ---- Where an optional node stands, and what a case that is one asks the oracle ----

    [Fact]
    public void NodesWhoseValueIsAnOptional_AreTheOnesThatTheTableAndTheSyntaxGive()
    {
        Expr x1 = new Ident("x1");
        Expr entry = new Select(new MapLiteral([new MapEntry(Quoted("foo"), None())], TrailingComma: false), "foo");
        List<(Expr Node, bool Optional)> nodes =
        [
            (Of(Lit(1)), true),
            (None(), true),
            (new Call("optional.ofNonZeroValue", [Lit(0)]) { Overload = "optional_ofNonZeroValue" }, true),
            (new Call("first", [new Ident("li")]) { Receiver = true, Overload = "list_first" }, true),
            (new Call("or", [None(), None()]) { Receiver = true, Overload = "optional_or_optional" }, true),
            (new Select(x1, "a") { Optional = true }, true),
            (new Select(new Select(x1, "a") { Optional = true }, "b"), true),
            (new Call(ProfileTables.OptionalIndex, [new Ident("li"), Lit(0)]) { Overload = "list_optindex_optional_int" }, true),
            (new Call(ProfileTables.Index, [Of(new Ident("li")), Lit(0)]) { Overload = "optional_list_index_int" }, true),
            (new Call(ProfileTables.Conditional, [new BoolLiteral(true), None(), Of(Lit(1))]), true),
            (new Comprehension(ProfileTables.OptMapMacro, Of(Lit(1)), "v", [new Ident("v")]), true),
            (new Comprehension(ProfileTables.OptFlatMapMacro, Of(Lit(1)), "v", [Of(new Ident("v"))]), true),
            (entry, true),
            (new Call(ProfileTables.Conditional, [new BoolLiteral(true), Lit(1), Lit(2)]), false),
            (new Call(ProfileTables.Conditional, [new BoolLiteral(true), None(), Lit(2)]), false),
            (new Select(x1, "a"), false),
            (new Call("value", [Of(Lit(1))]) { Receiver = true, Overload = "optional_value" }, false),
            (new Call("hasValue", [Of(Lit(1))]) { Receiver = true, Overload = "optional_hasValue" }, false),
            (new Call("optional.unwrap", [new ListLiteral([None()], TrailingComma: false)]) { Overload = "optional_unwrap" }, false),
            (new Comprehension("all", new ListLiteral([], TrailingComma: false), "v", [new BoolLiteral(true)]), false),
            (Lit(1), false),
        ];

        Assert.All(nodes, node => Assert.Equal(node.Optional, ProfileTables.IsOptional(node.Node)));
        Assert.True(ProfileTables.HoldsOptional(new ListLiteral([Of(Lit(1)), Lit(2)], TrailingComma: false)));
        Assert.True(ProfileTables.HoldsOptional(new MapLiteral([new MapEntry(Quoted("a"), None())], TrailingComma: false)));
        Assert.False(ProfileTables.HoldsOptional(new ListLiteral([Of(Lit(1))], TrailingComma: false) { OptionalPositions = [0] }));
        Assert.False(ProfileTables.HoldsOptional(new MapLiteral([new MapEntry(Quoted("a"), None()) { Optional = true }], TrailingComma: false)));
        Assert.False(ProfileTables.HoldsOptional(new ListLiteral([Lit(1)], TrailingComma: false)));
        Assert.False(ProfileTables.HoldsOptional(Of(Lit(1))));
    }

    [Fact]
    public void CaseThatIsAnOptionalNode_IsAskedForAsAPairAndTheLiteralThatHoldsOneAsItsSize()
    {
        Dictionary<string, Value> none = [];
        Expr entry = new Select(new MapLiteral([new MapEntry(Quoted("foo"), None())], TrailingComma: false), "foo");

        Assert.Equal(
            "[optional.of(1).hasValue(), optional.of(1).orValue(dyn(null))]",
            new GeneratedCase(Of(Lit(1)), none).Text);
        Assert.Equal(
            "[optional.none().hasValue(), optional.none().orValue(dyn(null))]",
            new GeneratedCase(None(), none).Text);
        Assert.Equal(
            "[{'foo': optional.none()}.foo.hasValue(), {'foo': optional.none()}.foo.orValue(dyn(null))]",
            new GeneratedCase(entry, none).Text);
        Assert.Equal(
            "size([optional.of(1), optional.none()])",
            new GeneratedCase(new ListLiteral([Of(Lit(1)), None()], TrailingComma: false), none).Text);
        Assert.Equal(
            "size({'foo': optional.none()})",
            new GeneratedCase(new MapLiteral([new MapEntry(Quoted("foo"), None())], TrailingComma: false), none).Text);

        // A literal whose optional elements are written ?e holds their values, so the oracle can return it.
        Assert.Equal(
            "[?optional.of(1)]",
            new GeneratedCase(new ListLiteral([Of(Lit(1))], TrailingComma: false) { OptionalPositions = [0] }, none).Text);
        Assert.Equal(
            "{?'a': optional.none()}",
            new GeneratedCase(new MapLiteral([new MapEntry(Quoted("a"), None()) { Optional = true }], TrailingComma: false), none).Text);
        Assert.Equal(
            "optional.of(1).value()",
            new GeneratedCase(new Call("value", [Of(Lit(1))]) { Receiver = true, Overload = "optional_value" }, none).Text);
    }

    // The value that the pair stands for is the one that the oracle gives and the library gives: the pair of the three values that an
    // optional can be, which cel-go tells apart as the three lists.
    [Fact]
    public void PairOfAnOptional_TellsTheEmptyOptionalFromNullAndFromTheHeldValue()
    {
        Dictionary<string, Value> none = [];
        string[] texts =
        [
            new GeneratedCase(None(), none).Text,
            new GeneratedCase(Of(new NullLiteral()), none).Text,
            new GeneratedCase(Of(Lit(1)), none).Text,
        ];

        Assert.Equal(texts.Length, texts.Distinct().Count());
        Assert.All(texts, text => Assert.StartsWith("[", text, StringComparison.Ordinal));
    }

    // ---- The optional value in Compare ----

    [Fact]
    public void OptionalValues_AreWrittenComparedAndNamedByTheirContent()
    {
        Value none = OptionalValue.Empty();
        Value ofNull = OptionalValue.Of(new Value { NullValue = NullValue.NullValue });
        Value ofOne = OptionalValue.Of(IntValue(1));

        Assert.Equal("optional(none)", ValueText.Format(none));
        Assert.Equal("optional(null)", ValueText.Format(ofNull));
        Assert.Equal("optional(1)", ValueText.Format(ofOne));
        Assert.Equal("optional(\"a\")", ValueText.Format(OptionalValue.Of(Str("a"))));
        Assert.Equal("[optional(none), 2]", ValueText.Format(ListOf(none, IntValue(2))));
        Assert.All(new[] { none, ofNull, ofOne }, value => Assert.Equal("optional_type", ValueText.TypeName(value)));
        Assert.Equal("optional_type optional(1)", SideOutcome.Of(ofOne).Describe());

        Assert.True(ValueEquality.Equal(none, OptionalValue.Empty()));
        Assert.True(ValueEquality.Equal(ofNull, OptionalValue.Of(new Value { NullValue = NullValue.NullValue })));
        Assert.True(ValueEquality.Equal(ofOne, OptionalValue.Of(IntValue(1))));
        Assert.False(ValueEquality.Equal(none, ofNull));
        Assert.False(ValueEquality.Equal(ofNull, ofOne));
        Assert.False(ValueEquality.Equal(none, ofOne));
        Assert.False(ValueEquality.Equal(ofOne, OptionalValue.Of(UintValue(1))));
        Assert.False(ValueEquality.Equal(ofOne, IntValue(1)));
        Assert.False(ValueEquality.Equal(IntValue(1), ofOne));
        Assert.True(ValueEquality.Equal(ListOf(none, ofOne), ListOf(OptionalValue.Empty(), OptionalValue.Of(IntValue(1)))));
        Assert.False(ValueEquality.Equal(ListOf(none), ListOf(ofNull)));
    }

    // The value of the library, in the form of OptionalValue: it needs the values of the set in the library, which the part that brings
    // them gives.
    [Fact]
    public void ResultOfTheLibrary_ThatIsAnOptional_IsWrittenInTheFormOfOptionalValue()
    {
        CelValue none = CelValue.FromOptional(default);
        CelValue ofNull = CelValue.FromOptional(new CelOptional<CelValue>(CelValue.Null));
        CelValue ofOne = CelValue.FromOptional(new CelOptional<CelValue>(CelValue.FromInt64(1)));

        Assert.True(ValueEquality.Equal(OptionalValue.Empty(), LibrarySide.ToExprValue(none)));
        Assert.True(ValueEquality.Equal(OptionalValue.Of(new Value { NullValue = NullValue.NullValue }), LibrarySide.ToExprValue(ofNull)));
        Assert.True(ValueEquality.Equal(OptionalValue.Of(IntValue(1)), LibrarySide.ToExprValue(ofOne)));
        Assert.True(ValueEquality.Equal(IntValue(7), LibrarySide.ToExprValue(CelValue.FromInt64(7))));
        Assert.True(ValueEquality.Equal(
            ListOf(OptionalValue.Of(IntValue(1)), IntValue(2)),
            LibrarySide.ToExprValue(CelValue.FromList(CelList.Create([ofOne, CelValue.FromInt64(2)])))));
    }

    // ---- Signatures and the minimizer on the nodes of the set ----

    [Fact]
    public void PresenceOfAnEmptyOptionalFromTheEntryOfAMap_GivesTheKnownSignatureOfTheRegistry()
    {
        Expr entry = new Select(new MapLiteral([new MapEntry(Quoted("foo"), None())], TrailingComma: false), "foo");
        Expr root = new PresenceTest(entry, "bar");
        Dictionary<string, SideOutcome> library = new() { [ExprPrinter.PrintCase(root)] = SideOutcome.Of(Bool(false)) };
        Dictionary<string, SideOutcome> oracle = new() { [ExprPrinter.PrintCase(root)] = SideOutcome.EvaluationError("no such key: bar") };

        (Divergence? divergence, DivergenceSearch search) = Search(library, oracle, root, "has(optional_type) we-value");

        Assert.Equal("has(optional_type) we-value", divergence?.Signature);
        Assert.Equal(["has(optional_type) we-value"], search.KnownSignaturesFound);
        Assert.Empty(search.NewSignatures);
    }

    [Fact]
    public void OrWhoseArgumentIsNoOptional_IsNamedByTheOverloadAndTheKindOfTheArgument()
    {
        Expr argument = new Call(ProfileTables.Dyn, [Lit(1)]);
        Expr root = new Call("or", [None(), argument]) { Receiver = true, Overload = "optional_or_optional" };
        Dictionary<string, SideOutcome> library = new() { [ExprPrinter.PrintCase(root)] = SideOutcome.EvaluationError("no such overload: or(optional_type, int)") };
        Dictionary<string, SideOutcome> oracle = new() { [ExprPrinter.PrintCase(root)] = SideOutcome.Of(ListOf(Bool(true), IntValue(1))) };

        (Divergence? divergence, DivergenceSearch search) = Search(library, oracle, root, "optional_or_optional(optional_type, int) we-error");

        Assert.Equal("optional_or_optional(optional_type, int) we-error", divergence?.Signature);
        Assert.Empty(search.NewSignatures);
    }

    [Fact]
    public void OptionalSelection_IsNamedByTheOverloadOfCelGoAndTheTypeOfItsOperand()
    {
        Expr map = new MapLiteral([new MapEntry(Quoted("a"), Lit(1))], TrailingComma: false);
        Expr root = new Select(map, "a") { Optional = true };
        Dictionary<string, SideOutcome> library = new()
        {
            [ExprPrinter.PrintCase(root)] = SideOutcome.Of(ListOf(Bool(true), IntValue(1))),
            [ExprPrinter.PrintCase(map)] = SideOutcome.Of(new Value { MapValue = new MapValue() }),
        };
        Dictionary<string, SideOutcome> oracle = new()
        {
            [ExprPrinter.PrintCase(root)] = SideOutcome.Of(ListOf(Bool(true), IntValue(2))),
            [ExprPrinter.PrintCase(map)] = SideOutcome.Of(new Value { MapValue = new MapValue() }),
        };

        (Divergence? divergence, _) = Search(library, oracle, root);

        Assert.Equal("select_optional_field(map) value-mismatch", divergence?.Signature);
    }

    // An int key fits the overloads of both optional-of-list and optional-of-map, and a name without parameters would give the first
    // of the pair to both: the index is named by the held type that the generator knows, and its receiver is written with it, so
    // the two divergences have two signatures. An optional that holds dyn keeps the first overload that the key fits.
    [Fact]
    public void IndexOverAnOptional_IsNamedByTheStaticTypeOfTheValueItHolds()
    {
        StaticType integer = StaticType.Of(ProfileType.IntType);
        Expr list = Of(new ListLiteral([Lit(1)], TrailingComma: false));
        Expr map = Of(new MapLiteral([new MapEntry(Lit(1), Lit(2))], TrailingComma: false));
        ScriptedSide side = new(_ => Agreed);

        string Name(Expr receiver, string function, StaticType? held) => Signature.Of(
            new GeneratedCase(
                new Call(function, [receiver, Lit(1)]) { Container = held },
                new Dictionary<string, Value>()),
            SideOutcome.Of(IntValue(1)),
            SideOutcome.Of(IntValue(2)),
            DivergenceKind.ValueMismatch,
            side,
            side);

        Assert.Equal(
            "optional_list_index_int(optional_type(list), int) value-mismatch",
            Name(list, ProfileTables.Index, StaticType.ListOf(integer)));
        Assert.Equal(
            "optional_map_index_value(optional_type(map), int) value-mismatch",
            Name(map, ProfileTables.Index, StaticType.MapOf(integer, integer)));
        Assert.Equal(
            "optional_list_optindex_optional_int(optional_type(list), int) value-mismatch",
            Name(list, ProfileTables.OptionalIndex, StaticType.ListOf(integer)));
        Assert.Equal(
            "optional_map_optindex_optional_value(optional_type(map), int) value-mismatch",
            Name(map, ProfileTables.OptionalIndex, StaticType.MapOf(integer, integer)));
        Assert.Equal(
            "optional_list_index_int(optional_type, int) value-mismatch",
            Name(map, ProfileTables.Index, StaticType.Dyn));
        Assert.Equal(
            "optional_list_index_int(optional_type, int) value-mismatch",
            Name(map, ProfileTables.Index, null));
    }

    // The name of the macro variable stands for the held value, which the optional gives as value(); the instance of the body is asked for
    // as a case like any other.
    [Fact]
    public void BodiesOfOptMap_AreInstancesWithTheHeldValueInPlaceOfTheName()
    {
        Expr body = new Call(ProfileTables.Add, [new Ident("v"), Lit(1)]);
        Expr optMap = new Comprehension(ProfileTables.OptMapMacro, Of(Lit(1)), "v", [body]);
        Expr independent = new Comprehension(ProfileTables.OptFlatMapMacro, Of(Lit(1)), "v", [Of(Lit(2))]);

        Assert.Equal(["optional.of(1).value() + 1", "1"], Printed(optMap));
        Assert.Equal(["optional.of(2)"], Printed(independent));
    }

    // An entry written ?k: v keeps its mark where the minimizer puts a node in place of the variable.
    [Fact]
    public void MinimizerReplacingAVariable_KeepsTheOptionalEntriesOfAMap()
    {
        Expr map = new MapLiteral([new MapEntry(Quoted("k"), Of(new Ident("v"))) { Optional = true }], TrailingComma: false);
        Expr body = new Call(ProfileTables.Greater, [new Call(ProfileTables.Size, [map]), Lit(0)]);
        Expr macro = new Comprehension("exists", new ListLiteral([Lit(1)], TrailingComma: false), "v", [body]);

        Assert.Equal(["size({?'k': optional.of((1))}) > 0", "'k'", "0"], Printed(macro));
    }

    // ---- The generator ----

    [Fact]
    public void OptionalNodes_StandOnlyWhereTheOracleIsNotAskedForThem()
    {
        List<GeneratedCase> cases = [.. Sample()];

        Assert.All(cases, generated => Assert.False(ProfileTables.IsOptional(generated.Expression), "an optional root: " + generated.Text));
        Assert.All(cases, generated => Assert.False(ProfileTables.HoldsOptional(generated.Expression), "a root that holds an optional: " + generated.Text));
        Assert.All(cases, generated => AssertConfined(generated.Expression, []));
        Assert.Contains(cases, generated => GeneratorTests.Nodes(generated.Expression).Any(ProfileTables.IsOptional));
        Assert.Contains(cases, generated => GeneratorTests.Nodes(generated.Expression).Any(ProfileTables.HoldsOptional));
    }

    // The value of an empty optional is built one time in ten, and the call carries the fault that tells the case fails on both sides.
    [Fact]
    public void ValueOfAnEmptyOptional_CarriesTheFaultAndOnlyThatOne()
    {
        List<Call> values = [.. Sample(ManyCases).SelectMany(generated => GeneratorTests.Nodes(generated.Expression)).OfType<Call>()
            .Where(call => call is { Function: "value", Receiver: true })];
        List<Call> faulty = [.. values.Where(call => call.Fault is not null)];

        Assert.NotEmpty(faulty);
        Assert.Contains(values, call => call.Fault is null);
        Assert.All(faulty, call => Assert.Equal("optional-empty", call.Fault));
        Assert.All(faulty, call => Assert.True(
            call.Args[0] is Call { Overload: "optional_none" or "optional_ofNonZeroValue" },
            ExprPrinter.Print(call)));
        Assert.InRange(faulty.Count * 100 / values.Count, 3, 25);
    }

    // The nodes of a case keep the types of the set apart from the others: the held value of an optional is never an optional, and no case
    // names a set other than the nine of the manifest.
    [Fact]
    public void OptionalNodes_HoldNoOptionalAndNoNodeOfAnAbstractType()
    {
        List<GeneratedCase> cases = [.. Sample()];

        foreach (Call of in cases.SelectMany(generated => GeneratorTests.Nodes(generated.Expression)).OfType<Call>()
            .Where(call => call.Overload is "optional_of" or "optional_ofNonZeroValue"))
        {
            Assert.False(ProfileTables.IsOptional(of.Args[0]), ExprPrinter.Print(of));
            Assert.Null(ProfileTables.AbstractResult(of.Args[0]));
        }
    }

    // Every form of the set occurs: the search ends when all have occurred.
    [Fact]
    public void EveryFormOfTheSetOptional_Occurs()
    {
        Dictionary<string, Func<Expr, bool>> forms = [];
        AddMacroAndSelectionForms(forms);
        AddLiteralAndPresenceForms(forms);
        AddCallForms(forms);
        AddPoolForms(forms);

        HashSet<string> missing = [.. forms.Keys];
        for (ulong n = 0; n < SearchLimit && missing.Count > 0; n++)
        {
            foreach (ulong seed in GeneratorTests.Seeds)
            {
                foreach (Expr node in GeneratorTests.Nodes(CaseGenerator.ExtensionCase(seed, n).Expression))
                {
                    _ = missing.RemoveWhere(name => forms[name](node));
                }
            }
        }

        Assert.Empty(missing.Order(StringComparer.Ordinal));
    }

    private static void AddMacroAndSelectionForms(Dictionary<string, Func<Expr, bool>> forms)
    {
        forms["optMap"] = node => node is Comprehension { Macro: ProfileTables.OptMapMacro };
        forms["optFlatMap"] = node => node is Comprehension { Macro: ProfileTables.OptFlatMapMacro };
        forms["optMap that reads its variable"] = node => node is Comprehension { Macro: ProfileTables.OptMapMacro } macro && ReadsItsVariable(macro);
        forms["optFlatMap that reads its variable"] = node => node is Comprehension { Macro: ProfileTables.OptFlatMapMacro } macro && ReadsItsVariable(macro);
        forms["e.?f"] = node => node is Select { Optional: true };
        forms["e.?a.b"] = node => node is Select { Optional: false, Operand: Select { Optional: true } };
        forms["e.?a.?b"] = node => node is Select { Optional: true, Operand: Select { Optional: true } };
        forms["e.f on an optional of a map or of dyn"] = node => node is Select { Optional: false } select && ProfileTables.IsOptional(select.Operand);
    }

    private static void AddLiteralAndPresenceForms(Dictionary<string, Func<Expr, bool>> forms)
    {
        forms["[?e]"] = node => node is ListLiteral { OptionalPositions.Count: > 0 };
        forms["[?e, f]"] = node => node is ListLiteral list && list.OptionalPositions.Count > 0 && list.OptionalPositions.Count < list.Elements.Count;
        forms["{?k: v}"] = node => node is MapLiteral map && map.Entries.Any(entry => entry.Optional);
        forms["{?k: v, k2: v2}"] = node => node is MapLiteral map && map.Entries.Any(entry => entry.Optional) && map.Entries.Any(entry => !entry.Optional);
        forms["has(e.?a.b)"] = node => node is PresenceTest test && ProfileTables.IsOptional(test.Operand);
        forms["has({'k': o}.k.f)"] = node => node is PresenceTest { Operand: Select { Operand: MapLiteral } };
    }

    private static void AddCallForms(Dictionary<string, Func<Expr, bool>> forms)
    {
        forms["type(o) == optional_type"] = node => node is Call { Function: ProfileTables.Equal or ProfileTables.NotEqual } call
            && call.Args.Any(argument => argument is Ident { Name: ProfileTables.OptionalTypeIdentifier })
            && call.Args.Any(argument => argument is Call { Function: ProfileTables.TypeOf });
        forms["type(o)"] = node => node is Call { Function: ProfileTables.TypeOf, Args: [{ } operand] } && ProfileTables.IsOptional(operand);
        forms["o == p and o != p"] = node => node is Call { Function: ProfileTables.Equal or ProfileTables.NotEqual, Args: [{ } left, { } right] }
            && ProfileTables.IsOptional(left) && ProfileTables.IsOptional(right);
        forms["c ? o : p"] = node => node is Call { Function: ProfileTables.Conditional, Args: [_, { } first, { } second] }
            && ProfileTables.IsOptional(first) && ProfileTables.IsOptional(second);
        forms["value() of an empty optional"] = node => node is Call { Function: "value", Fault: "optional-empty" };
        forms["value() of an optional that may have a value"] = node => node is Call { Function: "value", Fault: null };
        forms["orValue"] = node => node is Call { Function: "orValue", Receiver: true };
        forms["hasValue"] = node => node is Call { Function: "hasValue", Receiver: true };
        forms["optional.unwrap of a literal without elements"] = node => node is Call { Function: "optional.unwrap", Args: [ListLiteral { Elements.Count: 0 }] };
        forms["optional.unwrap of a literal of three"] = node => node is Call { Function: "optional.unwrap", Args: [ListLiteral { Elements.Count: 3 }] };
        forms["unwrapOpt"] = node => node is Call { Function: "unwrapOpt", Receiver: true };
    }

    private static void AddPoolForms(Dictionary<string, Func<Expr, bool>> forms)
    {
        foreach ((string name, Func<Expr, bool> pooled) in PooledArguments())
        {
            forms["optional.ofNonZeroValue(" + name + ")"] = node => node is Call { Function: "optional.ofNonZeroValue", Args: [{ } argument] } && pooled(argument);
        }
    }

    private static bool ReadsItsVariable(Comprehension macro) =>
        GeneratorTests.Nodes(macro.Arguments[0]).OfType<Ident>().Any(ident => ident.Name == macro.Variable);

    // The set is in about the same third of a run as the other sets, and the standard cases of a seed and a number are those of before
    // the set: they carry no node of it.
    [Fact]
    public void StandardCases_HaveNoNodeOfTheExtensionSets()
    {
        foreach (ulong seed in GeneratorTests.Seeds)
        {
            for (ulong n = 0; n < Cases; n++)
            {
                GeneratedCase generated = CaseGenerator.StandardCase(seed, n);

                Assert.DoesNotContain(
                    GeneratorTests.Nodes(generated.Expression),
                    node => node is Select { Optional: true } or Comprehension { Macro: ProfileTables.OptMapMacro or ProfileTables.OptFlatMapMacro }
                        or Call { Overload: not null } || (node is ListLiteral { OptionalPositions.Count: > 0 }));
            }
        }
    }

    // ---- Helpers ----

    // The cases of the extension profile of the fixed seeds, as the tests of the other sets read them.
    private static IEnumerable<GeneratedCase> Sample(int cases = Cases) => ExtensionGeneratorTests.Sample(cases);

    // The argument of optional.ofNonZeroValue that the pool of the profile holds, by the text of the page of the set.
    private static IEnumerable<(string Name, Func<Expr, bool> Matches)> PooledArguments() =>
    [
        ("null", argument => argument is NullLiteral),
        ("false", argument => argument is BoolLiteral { Value: false }),
        ("true", argument => argument is BoolLiteral { Value: true }),
        ("0", argument => argument is IntLiteral { Value: 0 }),
        ("1", argument => argument is IntLiteral { Value: 1 }),
        ("0u", argument => argument is UintLiteral { Value: 0 }),
        ("1u", argument => argument is UintLiteral { Value: 1 }),
        ("0.0", argument => argument is DoubleLiteral { Value: 0.0 } literal && !double.IsNegative(literal.Value)),
        ("-0.0", argument => argument is DoubleLiteral { Value: 0.0 } literal && double.IsNegative(literal.Value)),
        ("1.5", argument => argument is DoubleLiteral { Value: 1.5 }),
        ("d1", argument => argument is Ident { Name: "d1" }),
        ("''", argument => argument is StringLiteral { Value: "" }),
        ("'a'", argument => argument is StringLiteral { Value: "a" }),
        ("b''", argument => argument is BytesLiteral { Value.Count: 0 }),
        ("b'a'", argument => argument is BytesLiteral { Value.Count: 1 }),
        ("[]", argument => argument is ListLiteral { Elements.Count: 0 }),
        ("[0]", argument => argument is ListLiteral { Elements: [IntLiteral { Value: 0 }] }),
        ("{}", argument => argument is MapLiteral { Entries.Count: 0 }),
        ("{'a': 0}", argument => argument is MapLiteral { Entries: [{ Value: IntLiteral { Value: 0 } }] }),
        ("duration('0s')", argument => argument is Call { Function: ProfileTables.DurationConversion, Args: [StringLiteral { Value: "0s" }] }),
        ("duration('1s')", argument => argument is Call { Function: ProfileTables.DurationConversion, Args: [StringLiteral { Value: "1s" }] }),
        ("timestamp('0001-01-01T00:00:00Z')", argument => argument is Call { Function: ProfileTables.TimestampConversion, Args: [StringLiteral { Value: "0001-01-01T00:00:00Z" }] }),
        ("timestamp(0)", argument => argument is Call { Function: ProfileTables.TimestampConversion, Args: [IntLiteral { Value: 0 }] }),
    ];

    // An optional node stands only where the oracle is not asked for it; a literal that holds one stands only as the argument of
    // optional.unwrap and unwrapOpt, or as the map of has({'k': o}.k.f). The ancestors are the nodes above, the nearest first.
    private static void AssertConfined(Expr node, List<Expr> ancestors)
    {
        if (ProfileTables.IsOptional(node))
        {
            Assert.True(ancestors.Count > 0 && MayHoldOptional(node, ancestors), "an optional where the oracle is asked for it: " + DescribeParent(ancestors));
        }

        if (ProfileTables.HoldsOptional(node))
        {
            Assert.True(
                ancestors.Count > 0
                    && (ancestors[0] is Call { Function: "optional.unwrap" or "unwrapOpt" }
                        || (node is MapLiteral && ancestors[0] is Select && ancestors.Count > 1 && ancestors[1] is PresenceTest)),
                "a literal that holds an optional where the oracle is asked for it: " + DescribeParent(ancestors));
        }

        List<Expr> inner = [node, .. ancestors];
        foreach (Expr child in ExtensionGeneratorTests.Children(node))
        {
            AssertConfined(child, inner);
        }
    }

    private static bool MayHoldOptional(Expr node, List<Expr> ancestors) => ancestors[0] switch
    {
        Call { Function: "hasValue" or "value" or "orValue" or "or", Receiver: true } => true,
        Call { Function: ProfileTables.Equal or ProfileTables.NotEqual or ProfileTables.TypeOf } => true,
        Call { Function: ProfileTables.Conditional } call => !ReferenceEquals(call.Args[0], node) && ProfileTables.IsOptional(call),
        Call { Function: ProfileTables.Index or ProfileTables.OptionalIndex } call => ReferenceEquals(call.Args[0], node) && ProfileTables.IsOptional(call),
        Select select => ProfileTables.IsOptional(select),
        PresenceTest => true,
        Comprehension { Macro: ProfileTables.OptMapMacro or ProfileTables.OptFlatMapMacro } macro =>
            ReferenceEquals(macro.Range, node) || (macro.Macro == ProfileTables.OptFlatMapMacro && ReferenceEquals(macro.Arguments[0], node)),
        ListLiteral list => list.OptionalPositions.Contains(IndexOf(list.Elements, node))
            || (ancestors.Count > 1 && ancestors[1] is Call { Function: "optional.unwrap" or "unwrapOpt" }),
        MapLiteral map => map.Entries.Any(entry => ReferenceEquals(entry.Value, node)
            && (entry.Optional || (ancestors.Count > 2 && ancestors[1] is Select && ancestors[2] is PresenceTest))),
        _ => false,
    };

    private static int IndexOf(IReadOnlyList<Expr> elements, Expr node)
    {
        for (int index = 0; index < elements.Count; index++)
        {
            if (ReferenceEquals(elements[index], node))
            {
                return index;
            }
        }

        return -1;
    }

    private static string DescribeParent(List<Expr> ancestors) => ancestors.Count == 0 ? "no parent" : ExprPrinter.Print(ancestors[^1]);

    private static IEnumerable<string> Printed(Expr macro) => Minimizer.BodyInstances(macro, _ => null).Select(ExprPrinter.Print);

    // A search on fake sides: the outcomes of the named texts, and an agreeing int for every other text.
    private static (Divergence? Divergence, DivergenceSearch Search) Search(
        Dictionary<string, SideOutcome> library, Dictionary<string, SideOutcome> oracle, Expr root, params string[] known)
    {
        DivergenceSearch search = new(
            new ScriptedSide(text => library.TryGetValue(text, out SideOutcome? outcome) ? outcome : Agreed),
            new ScriptedSide(text => oracle.TryGetValue(text, out SideOutcome? outcome) ? outcome : Agreed),
            new NoRecorder(),
            KnownSignatures.Of(known));
        return (search.Check(20261104, 1207, new GeneratedCase(root, new Dictionary<string, Value>())), search);
    }

    private static Call Of(Expr value) => new("optional.of", [value]) { Overload = "optional_of" };

    private static Call None() => new("optional.none", []) { Overload = "optional_none" };

    private static IntLiteral Lit(long value) => new(value, IntegerForm.DecimalDigits);

    private static StringLiteral Quoted(string text) => new(text, new TextForm(Quote.Apostrophe, null, [.. text.EnumerateRunes().Select(_ => Escape.Literal)]));

    private static Value IntValue(long value) => new() { Int64Value = value };

    private static Value UintValue(ulong value) => new() { Uint64Value = value };

    private static Value Bool(bool value) => new() { BoolValue = value };

    private static Value Str(string value) => new() { StringValue = value };

    private static Value ListOf(params Value[] values)
    {
        ListValue list = new();
        list.Values.AddRange(values);
        return new Value { ListValue = list };
    }

    // A side that gives the outcome that a function gives for the text of an expression.
    private sealed class ScriptedSide(Func<string, SideOutcome> evaluate) : ICaseSide
    {
        public SideOutcome Evaluate(string expression, IReadOnlyDictionary<string, Value> bindings) => evaluate(expression);

        public TimeSpan ZoneOffset(string zone, DateTimeOffset instant) => throw new NotSupportedException("no time zones");
    }

    private sealed class NoRecorder : ICaseRecorder
    {
        public string Case(string name, string description, string expression, IReadOnlyDictionary<string, Value> bindings) => name;
    }
}
