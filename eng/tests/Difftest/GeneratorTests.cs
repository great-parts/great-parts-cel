using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Cel.Expr;
using Great.Parts.Cel.Configuration;
using Great.Parts.Cel.Eng.Difftest.Compare;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Tests.Support;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Difftest;

// The case generator and the printer of eng/difftest (docs/design/eng-difftest.md, "Profile"). The generator tests read the
// cases 0 to Cases - 1 of fixed seeds; the printer tests print fixed trees.
public sealed class GeneratorTests
{
    private const int Cases = 400;

    // The cases per seed of the test of the string arguments of conversions, which are rarer than the other forms.
    private const int ConversionCases = 4000;

    // The cases per seed of the test of the forms rarer still: dyn() around an iteration variable that is the range of a macro.
    private const int RareCases = 50000;

    private static readonly ulong[] Seeds = [1, 20261104, ulong.MaxValue];

    // One literal per line: the kind, the value, the form, and the printed text, or "!" when the printer refuses the form.
    // int | value | form | text; uint | value | form | suffix | text; double | value | form | text;
    // string | JSON value | quote | raw prefix or - | unit forms | text; bytes | hex bytes | prefix | quote | raw prefix or - | unit forms | text.
    private const string LiteralForms = """"
        int | 255 | DecimalDigits | 255
        int | 255 | HexLower | 0xff
        int | 255 | HexUpper | 0xFF
        int | -1 | HexLower | -0x1
        int | -9223372036854775808 | DecimalDigits | -9223372036854775808
        int | -9223372036854775808 | HexUpper | -0x8000000000000000
        uint | 7 | DecimalDigits | u | 7u
        uint | 18446744073709551615 | HexUpper | U | 0xFFFFFFFFFFFFFFFFU
        uint | 10 | HexLower | U | 0xaU
        double | 1500 | Plain | 1500.0
        double | 1500 | Exponent | 1.5e3
        double | 1500 | LeadingDot | .15E+4
        double | 0.015 | Plain | 0.015
        double | 0.015 | Exponent | 1.5e-2
        double | 0.015 | LeadingDot | .015
        double | 0 | Plain | 0.0
        double | 0 | Exponent | 0e0
        double | 0 | LeadingDot | .0
        double | 1E+20 | Plain | 100000000000000000000.0
        double | 0.1 | Exponent | 1e-1
        double | 5E-324 | Exponent | 5e-324
        double | 1.7976931348623157E+308 | Exponent | 1.7976931348623157e308
        double | 123.456 | LeadingDot | .123456E+3
        double | -1.5 | Plain | -1.5
        double | -1.5 | Exponent | -1.5e0
        double | -0.5 | LeadingDot | -.5
        double | -0 | Plain | -0.0
        double | NaN | Plain | !
        double | -Infinity | Exponent | !
        string | "a'b" | Apostrophe | - | Literal Character Literal | 'a\'b'
        string | "\u0007\b\f\n\r\t\u000b\\'\"`?" | QuotationMark | - | Character Character Character Character Character Character Character Character Character Character Character Character | "\a\b\f\n\r\t\v\\\'\"\`\?"
        string | "é€😀" | TripleApostrophe | - | Hex UnicodeShort UnicodeLong | '''\xe9\u20ac\U0001f600'''
        string | "é\u0000" | TripleQuotationMark | - | Octal Octal | """\351\000"""
        string | "\"" | TripleQuotationMark | - | UnicodeShort | """\u0022"""
        string | "a\\b\"" | Apostrophe | r | Literal Literal Literal Literal | r'a\b"'
        string | "x'y" | QuotationMark | R | Literal Literal Literal | R"x'y"
        string | "" | Apostrophe | - |  | ''
        string | "\\" | Apostrophe | - | Literal | !
        string | "'" | TripleApostrophe | - | Literal | !
        string | "\n" | QuotationMark | r | Literal | !
        string | "\r" | TripleQuotationMark | r | Literal | !
        string | "\r" | TripleApostrophe | - | Literal | !
        string | "Ā" | Apostrophe | - | Octal | !
        string | "😀" | Apostrophe | - | UnicodeShort | !
        string | "a" | Apostrophe | - | Character | !
        string | "ab" | Apostrophe | - | Literal | !
        bytes | 61 c3 a9 ff | b | Apostrophe | - | Literal Literal Hex | b'aé\xff'
        bytes | 00 0a 27 | B | QuotationMark | - | Octal Character Character | B"\000\n\'"
        bytes | 5c 22 | b | Apostrophe | r | Literal Literal | br'\"'
        bytes | e2 82 ac | B | TripleQuotationMark | R | Literal | BR"""€"""
        bytes |  | b | QuotationMark | - |  | b""
        bytes | c3 a9 | b | Apostrophe | - | Hex Hex | b'\xc3\xa9'
        bytes | ff | b | Apostrophe | - | Literal | !
        bytes | 61 | b | Apostrophe | - | UnicodeShort | !
        bytes | c3 | b | Apostrophe | r | Literal | !
        bytes | 0d | b | TripleApostrophe | R | Literal | !
        bytes | 0d | B | TripleQuotationMark | - | Character | B"""\r"""
        """";


    // ---- The generator ----

    // The library's type checker is the checker these tests have without Go: every case compiles, and its type is one of the
    // target types of the profile; every target type is the type of some root.
    [Fact]
    public void EveryCase_TypeChecksAndEveryTargetTypeOccurs()
    {
        CelEnvironment environment = Environment();
        HashSet<CelTypeKind> resultKinds = [];

        foreach (GeneratedCase generated in Sample())
        {
            CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(generated.Text);

            Assert.True(compiled.IsSuccess, generated.Text + ": " + string.Join("; ", compiled.Errors.Select(error => error.Message)));
            resultKinds.Add(compiled.Program!.ResultType.Kind);
        }

        // A root that is the variable of the type int_wrapper has the type wrapper(int).
        Assert.Equal(
            new[] { CelTypeKind.Dyn, CelTypeKind.Null, CelTypeKind.Bool, CelTypeKind.Int, CelTypeKind.UInt, CelTypeKind.Double, CelTypeKind.String, CelTypeKind.Bytes, CelTypeKind.List, CelTypeKind.Map, CelTypeKind.Wrapper, CelTypeKind.Type },
            resultKinds.Order());
    }

    [Fact]
    public void EveryFunctionAndOverloadStyleOfTheProfile_OccursAndNoOther()
    {
        List<Call> calls = [.. AllNodes().OfType<Call>()];
        HashSet<string> profile = [.. ProfileTables.Overloads.Select(overload => overload.Function)];

        Assert.All(calls, call => Assert.Contains(call.Function, profile));
        Assert.DoesNotContain(calls, call => call.Function == "matches");
        Assert.Equal(profile.Order(StringComparer.Ordinal), calls.Select(call => call.Function).Distinct().Order(StringComparer.Ordinal));
        Assert.Contains(calls, call => call is { Function: ProfileTables.Size, Receiver: true });
        Assert.Contains(calls, call => call is { Function: ProfileTables.Size, Receiver: false });
        Assert.All(calls.Where(call => call.Function is ProfileTables.Contains or ProfileTables.StartsWith or ProfileTables.EndsWith), call => Assert.True(call.Receiver));
        Assert.All(calls.Where(call => call.Function is ProfileTables.LogicalAnd or ProfileTables.LogicalOr), call => Assert.Equal(2, call.Args.Count));
        Assert.Contains(AllNodes(), node => ChainLength(node, ProfileTables.LogicalAnd) == ProfileTables.MaxChain);
        Assert.Contains(AllNodes(), node => ChainLength(node, ProfileTables.LogicalOr) == ProfileTables.MaxChain);
    }

    [Fact]
    public void EveryNodeKindOfTheProfile_Occurs()
    {
        List<Expr> nodes = [.. AllNodes()];
        List<Comprehension> macros = [.. nodes.OfType<Comprehension>()];

        Assert.Equal(ProfileTables.Macros.Order(StringComparer.Ordinal), macros.Select(macro => macro.Macro).Distinct().Order(StringComparer.Ordinal));
        Assert.Contains(macros, macro => macro is { Macro: ProfileTables.MapMacro, Arguments.Count: 1 });
        Assert.Contains(macros, macro => macro is { Macro: ProfileTables.MapMacro, Arguments.Count: 2 });
        Assert.All(macros.Where(macro => macro.Macro != ProfileTables.MapMacro), macro => Assert.Single(macro.Arguments));
        Assert.Contains(nodes, node => node is Select);
        Assert.Contains(nodes, node => node is PresenceTest);
        Assert.All(nodes.OfType<Select>().Select(select => select.Field).Concat(nodes.OfType<PresenceTest>().Select(test => test.Field)), field => Assert.Contains(field, ProfileTables.FieldNames));
        Assert.Equal(
            ProfileTables.TypeIdentifiers.Order(StringComparer.Ordinal),
            nodes.OfType<Ident>().Select(ident => ident.Name).Where(ProfileTables.TypeIdentifiers.Contains).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(
            ProfileTables.Variables.Select(variable => variable.Name).Order(StringComparer.Ordinal),
            nodes.OfType<Ident>().Select(ident => ident.Name).Where(name => ProfileTables.Variables.Any(variable => variable.Name == name)).Distinct().Order(StringComparer.Ordinal));
        // The variable of a macro inside another macro is read in a rare form, so its sample is the larger one.
        Assert.Equal(
            [.. ProfileTables.IterationVariables],
            Sample(ConversionCases).SelectMany(generated => Nodes(generated.Expression)).OfType<Ident>().Select(ident => ident.Name)
                .Where(ProfileTables.IterationVariables.Contains).Distinct().Order(StringComparer.Ordinal));
        Assert.DoesNotContain(nodes.OfType<Ident>().Select(ident => ident.Name), name =>
            !ProfileTables.TypeIdentifiers.Contains(name) && !ProfileTables.IterationVariables.Contains(name) && ProfileTables.Variables.All(variable => variable.Name != name));
    }

    // A dyn operand stands wherever a function takes one: as an argument of the functions of the profile, as the operand of a
    // selection and of a presence test, and as the range of a macro.
    [Fact]
    public void DynOperands_StandWhereFunctionsTakeThem()
    {
        List<Expr> nodes = [.. AllNodes()];
        List<Call> calls = [.. nodes.OfType<Call>()];

        foreach (string function in new[] { ProfileTables.Negate, ProfileTables.Add, ProfileTables.Divide, ProfileTables.Modulo, ProfileTables.Less, ProfileTables.Size, ProfileTables.IntConversion, ProfileTables.Contains, ProfileTables.LogicalAnd })
        {
            Assert.Contains(calls, call => call.Function == function && call.Args.Any(IsDyn));
        }

        Assert.Contains(nodes, node => node is PresenceTest test && IsDyn(test.Operand));
        Assert.Contains(nodes, node => node is Select select && IsDyn(select.Operand));
        Assert.Contains(nodes, node => node is Comprehension macro && IsDyn(macro.Range));
        Assert.Contains(calls, call => call.Function == ProfileTables.Index && IsDyn(call.Args[0]));
    }

    // An ordering across numeric kinds has a candidate only with a dyn argument: where both arguments are literals or
    // variables of declared types, the kinds are the same.
    [Fact]
    public void OrderingOfLiteralsAndVariables_ComparesOneKind()
    {
        List<Call> orderings = [.. AllNodes().OfType<Call>().Where(call => call.Function is ProfileTables.Less or ProfileTables.LessEqual or ProfileTables.Greater or ProfileTables.GreaterEqual)];

        Assert.All(
            orderings.Where(call => call.Args.All(arg => AtomKind(arg) is not null)),
            call => Assert.Equal(AtomKind(call.Args[0]), AtomKind(call.Args[1])));
        Assert.Contains(orderings, call => call.Args.Any(IsDyn) && call.Args.Any(arg => AtomKind(arg) is ProfileType.DoubleType));
    }

    // Half of the string arguments of int, uint, double and bool are numbers or bools in the text forms of the standard
    // library page.
    [Fact]
    public void StringArgumentsOfConversions_AreOftenTextsOfNumbersAndBools()
    {
        List<Call> conversions = [.. Sample(ConversionCases).SelectMany(generated => Nodes(generated.Expression)).OfType<Call>().Where(call => call.Args is [StringLiteral])];

        foreach (string function in new[] { ProfileTables.IntConversion, ProfileTables.UintConversion, ProfileTables.DoubleConversion })
        {
            Assert.Contains(conversions, call => call.Function == function && ((StringLiteral)call.Args[0]).Value.Any(char.IsAsciiDigit));
        }

        Assert.Contains(conversions, call => call.Function == ProfileTables.DoubleConversion && ((StringLiteral)call.Args[0]).Value.Contains("inf", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(conversions, call => call.Function == ProfileTables.BoolConversion && ((StringLiteral)call.Args[0]).Value is "true" or "True" or "TRUE" or "t" or "T" or "1");
    }

    [Fact]
    public void MapKeys_ArePairwiseUnequalByCelEqualityAndOfTheKeyTypes()
    {
        List<MapLiteral> maps = [.. AllNodes().OfType<MapLiteral>()];

        Assert.Contains(maps, map => map.Entries.Count == ProfileTables.MaxElements);
        Assert.Contains(maps, map => map.Entries.Any(entry => entry.Key is IntLiteral) && map.Entries.Any(entry => entry.Key is UintLiteral));
        Assert.Contains(maps, map => map.Entries.Any(entry => entry.Key is StringLiteral { Value: "a" or "b" or "f" }));
        foreach (MapLiteral map in maps)
        {
            List<object> keys = [.. map.Entries.Select(entry => CelKey(entry.Key))];
            Assert.Equal(keys.Count, keys.Distinct().Count());
        }
    }

    [Fact]
    public void Cases_KeepTheLimits()
    {
        List<GeneratedCase> cases = [.. Sample()];

        Assert.All(cases, generated => Assert.InRange(Depth(generated.Expression), 1, ProfileTables.MaxDepth));
        Assert.All(cases, generated => Assert.InRange(Nodes(generated.Expression).Count(), 1, ProfileTables.MaxNodes));
        Assert.All(cases, generated => Assert.InRange(MacroNesting(generated.Expression), 0, ProfileTables.MaxMacroNesting));
        Assert.Equal(ProfileTables.MaxDepth, cases.Max(generated => Depth(generated.Expression)));
        Assert.Equal(ProfileTables.MaxMacroNesting, cases.Max(generated => MacroNesting(generated.Expression)));
        Assert.All(cases.SelectMany(generated => Nodes(generated.Expression)), node => Assert.InRange(TextLength(node), 0, LimitOf(node)));
        Assert.All(cases.SelectMany(generated => generated.Bindings.Values).SelectMany(Values), value => Assert.InRange(TextLength(value), 0, LimitOf(value)));
        Assert.All(cases.SelectMany(generated => Nodes(generated.Expression)), node => Assert.InRange(Elements(node), 0, ProfileTables.MaxElements));
        Assert.Contains(cases, generated => generated.Bindings.Values.Any(value => value.KindCase == Value.KindOneofCase.StringValue && TextLength(value) == ProfileTables.MaxCodePoints));
    }

    [Fact]
    public void Bindings_HaveTheTypesOfTheVariablesAndSomeStayUnbound()
    {
        List<GeneratedCase> cases = [.. Sample()];
        Dictionary<string, ProfileVariable> variables = ProfileTables.Variables.ToDictionary(variable => variable.Name);
        Dictionary<string, ProfileType> types = ProfileTables.Variables.ToDictionary(variable => variable.Name, variable => variable.Type);
        List<Value> dynValues = [.. cases.SelectMany(generated => generated.Bindings.Where(binding => types[binding.Key] == ProfileType.DynType)).Select(binding => binding.Value)];

        // A wrapper is bound to its kind or to null; the parts of a typed collection are checked by the next test.
        Assert.All(cases.SelectMany(generated => generated.Bindings), binding => Assert.True(
            types[binding.Key] == ProfileType.DynType
                || KindOf(binding.Value) == types[binding.Key]
                || (variables[binding.Key].Wrapper && KindOf(binding.Value) == ProfileType.NullType),
            binding.Key + " = " + binding.Value));
        Assert.Contains(cases, generated => generated.Bindings.Count < ProfileTables.Variables.Count);
        Assert.Equal(
            ProfileTables.DynKinds.Order(),
            cases.SelectMany(generated => generated.Bindings.Where(binding => binding.Key == "x1")).Select(binding => KindOf(binding.Value)).Distinct().Order());
        Assert.Contains(dynValues, value => value.ListValue is { Values.Count: ProfileTables.MaxElements });
        Assert.Contains(dynValues, value => value.MapValue is { Entries.Count: ProfileTables.MaxElements });
        Assert.Contains(dynValues, value => value.MapValue is { } map && map.Entries.Any(entry => entry.Key.StringValue is "a" or "b" or "f"));
        Assert.All(dynValues.Where(value => value.KindCase is Value.KindOneofCase.ListValue or Value.KindOneofCase.MapValue), value =>
        {
            Assert.InRange(value.ListValue?.Values.Count ?? value.MapValue.Entries.Count, 0, ProfileTables.MaxElements);
            Assert.All(Values(value).Skip(1), inner => Assert.Contains(KindOf(inner), ProfileTables.ScalarKinds));
        });
        Assert.All(dynValues.Where(value => value.KindCase == Value.KindOneofCase.MapValue), value =>
        {
            List<object> keys = [.. value.MapValue.Entries.Select(entry => CelKey(entry.Key))];
            Assert.Equal(keys.Count, keys.Distinct().Count());
            Assert.All(value.MapValue.Entries, entry => Assert.Contains(KindOf(entry.Key), ProfileTables.KeyTypes));
        });
    }

    // A typed list has 0–3 elements of its element type, and list<dyn> scalars and null; a typed map has 0–3 entries with
    // distinct keys of its key type — the names of the fields for string keys — and values of its value type; a wrapper is
    // a value of its kind or null.
    [Fact]
    public void TypedVariables_AreBoundToValuesOfTheirTypeParameters()
    {
        List<GeneratedCase> cases = [.. Sample()];
        List<Value> Bound(string name) => [.. cases.Where(generated => generated.Bindings.ContainsKey(name)).Select(generated => generated.Bindings[name])];
        IEnumerable<Value> PartsOf(Value bound) => bound.KindCase == Value.KindOneofCase.ListValue
            ? bound.ListValue.Values
            : bound.MapValue.Entries.Select(entry => entry.Value);
        List<Value> Parts(string name) => [.. Bound(name).SelectMany(PartsOf)];

        foreach (string name in new[] { "li", "ls", "lx" })
        {
            Assert.All(Bound(name), bound => Assert.InRange(bound.ListValue.Values.Count, 0, ProfileTables.MaxElements));
            Assert.Contains(Bound(name), bound => bound.ListValue.Values.Count == ProfileTables.MaxElements);
        }

        Assert.All(Parts("li"), element => Assert.Equal(ProfileType.IntType, KindOf(element)));
        Assert.All(Parts("ls"), element => Assert.Equal(ProfileType.StringType, KindOf(element)));
        Assert.All(Parts("lx"), element => Assert.Contains(KindOf(element), ProfileTables.ScalarKinds));
        Assert.Equal(ProfileTables.ScalarKinds.Order(), Parts("lx").Select(KindOf).Distinct().Order());

        foreach (string name in new[] { "ms", "mx" })
        {
            Assert.All(Bound(name), bound =>
            {
                Assert.InRange(bound.MapValue.Entries.Count, 0, ProfileTables.MaxElements);
                List<object> keys = [.. bound.MapValue.Entries.Select(entry => CelKey(entry.Key))];
                Assert.Equal(keys.Count, keys.Distinct().Count());
            });
            Assert.Contains(Bound(name), bound => bound.MapValue.Entries.Count > 1);
        }

        Assert.All(Bound("ms").SelectMany(bound => bound.MapValue.Entries), entry =>
        {
            Assert.Contains(entry.Key.StringValue, ProfileTables.FieldNames);
            Assert.Equal(ProfileType.IntType, KindOf(entry.Value));
        });
        Assert.All(Bound("mx").SelectMany(bound => bound.MapValue.Entries), entry =>
        {
            Assert.Equal(ProfileType.IntType, KindOf(entry.Key));
            Assert.Contains(KindOf(entry.Value), ProfileTables.ScalarKinds);
        });
        Assert.Equal([ProfileType.IntType, ProfileType.NullType], Bound("wi").Select(KindOf).Distinct().Order());
    }

    // A variable with a qualified name is written through the container: fewer names, or all of them, with or without a leading
    // dot; the identifier keeps the declared name, which is the key of the binding.
    [Fact]
    public void QualifiedVariables_AreWrittenInEveryFormTheContainerResolves()
    {
        List<Ident> idents = [.. AllNodes().OfType<Ident>()];

        foreach (ProfileVariable variable in ProfileTables.Variables)
        {
            Assert.Equal(
                variable.Spellings.Order(StringComparer.Ordinal),
                idents.Where(ident => ident.Name == variable.Name).Select(ident => ident.Spelling ?? ident.Name).Distinct().Order(StringComparer.Ordinal));
        }

        Assert.Equal(["q", "gp.q", ".gp.q"], ProfileTables.Variables.Single(variable => variable.Name == "gp.q").Spellings);
        Assert.Equal(["r", "test.r", "gp.test.r", ".gp.test.r"], ProfileTables.Variables.Single(variable => variable.Name == "gp.test.r").Spellings);
        Assert.Equal(
            ".gp.q + 1|q - 1|test.r.size()|.gp.test.r + 'a'",
            string.Join('|', new Expr[]
            {
                new Call(ProfileTables.Add, [new Ident("gp.q") { Spelling = ".gp.q" }, new IntLiteral(1, IntegerForm.DecimalDigits)]),
                new Call(ProfileTables.Subtract, [new Ident("gp.q") { Spelling = "q" }, new IntLiteral(1, IntegerForm.DecimalDigits)]),
                new Call(ProfileTables.Size, [new Ident("gp.test.r") { Spelling = "test.r" }]) { Receiver = true },
                new Call(ProfileTables.Add, [new Ident("gp.test.r") { Spelling = ".gp.test.r" }, new StringLiteral("a", new TextForm(Quote.Apostrophe, null, [Escape.Literal]))]),
            }.Select(ExprPrinter.Print)));
    }

    // The typed variables are operands of the forms whose parameter types they fit: indexes, size, in, + on lists, selections
    // and presence tests on a map with string keys, macros. Each form occurs; map and filter never take a map variable as
    // the range, since cel-go iterates a map in no fixed order. That each case type checks is the test of the first group.
    [Fact]
    public void TypedVariables_StandWhereTheirTypeFits()
    {
        List<Expr> nodes = [.. AllNodes()];
        List<Comprehension> macros = [.. nodes.OfType<Comprehension>()];
        string[] lists = ["li", "ls", "lx"];
        string[] maps = ["ms", "mx"];

        Assert.Contains(nodes, node => node is Call { Function: ProfileTables.Index, Args: [var container, _] } && IsVariable(container, lists));
        Assert.Contains(nodes, node => node is Call { Function: ProfileTables.Index, Args: [var container, _] } && IsVariable(container, maps));
        Assert.Contains(nodes, node => node is Call { Function: ProfileTables.Size, Args: [var operand] } && IsVariable(operand, [.. lists, .. maps]));
        Assert.Contains(nodes, node => node is Call { Function: ProfileTables.In, Args: [_, var container] } && IsVariable(container, lists));
        Assert.Contains(nodes, node => node is Call { Function: ProfileTables.In, Args: [_, var container] } && IsVariable(container, maps));
        Assert.Contains(nodes, node => node is Call { Function: ProfileTables.Add, Args: [var left, _] } && IsVariable(left, lists));
        Assert.Contains(nodes, node => node is Select { Operand: var operand } && IsVariable(operand, "ms"));
        Assert.Contains(nodes, node => node is PresenceTest { Operand: var operand } && IsVariable(operand, "ms"));
        Assert.Contains(macros, macro => IsVariable(macro.Range, lists));
        Assert.Contains(macros, macro => IsVariable(macro.Range, maps));
        Assert.Contains(macros, macro => macro.Macro is ProfileTables.MapMacro or ProfileTables.Filter && IsVariable(macro.Range, lists));
        Assert.DoesNotContain(macros, macro => macro.Macro is ProfileTables.MapMacro or ProfileTables.Filter && IsVariable(macro.Range, maps));
        Assert.Contains(nodes, node => node is Ident { Name: "wi" });
    }

    [Fact]
    public void Cases_HaveTheFormsOfTheProfile()
    {
        List<GeneratedCase> cases = [.. Sample()];
        List<Expr> nodes = [.. cases.SelectMany(generated => Nodes(generated.Expression))];
        List<double> literals = [.. nodes.OfType<DoubleLiteral>().Select(literal => literal.Value)];
        List<KeyValuePair<string, Value>> doubles = [.. cases.SelectMany(generated => generated.Bindings).Where(binding => binding.Value.KindCase == Value.KindOneofCase.DoubleValue)];
        List<double> bound = [.. doubles.Select(binding => binding.Value.DoubleValue)];

        Assert.All(literals, value => Assert.True(double.IsFinite(value), value.ToString(CultureInfo.InvariantCulture)));
        Assert.Contains(literals, value => value < 0);
        Assert.Contains(nodes, node => node is ListLiteral { Elements.Count: 0, TrailingComma: true });
        Assert.Contains(nodes, node => node is MapLiteral { Entries.Count: 0, TrailingComma: true });
        Assert.Contains(nodes, node => node is MapLiteral { Entries.Count: > 0, TrailingComma: true });
        Assert.Contains(nodes, node => node is PresenceTest { Operand: MapLiteral });
        Assert.Contains(nodes, node => node is Select { Operand: MapLiteral map, Field: var field } && map.Entries.Any(entry => entry.Key is StringLiteral key && key.Value == field));
        Assert.Contains(nodes, node => node is Call { Function: ProfileTables.Index, Args: [MapLiteral, _] });
        Assert.Contains(nodes, node => node is Call { Function: ProfileTables.Index, Args: [ListLiteral, _] });
        Assert.Contains(nodes, node => node is Call { Function: ProfileTables.Dyn });
        Assert.Contains(bound, double.IsNaN);
        Assert.Contains(bound, double.IsPositiveInfinity);
        Assert.Contains(bound, double.IsNegativeInfinity);
        Assert.Contains(bound, value => value == 0 && double.IsNegative(value));
        Assert.Contains(doubles, binding => binding.Key is "x1" or "x2" && !double.IsFinite(binding.Value.DoubleValue));
    }

    // map and filter list the elements in the order of their range, and cel-go iterates a map in no fixed order: their range
    // is a list literal, a typed list variable, a map literal of at most one entry, either literal in dyn(), never a dyn variable
    // or a map variable.
    [Fact]
    public void RangeOfMapAndFilter_HasAFixedOrder()
    {
        List<Comprehension> transforms = [.. AllNodes().OfType<Comprehension>().Where(macro => macro.Macro is ProfileTables.MapMacro or ProfileTables.Filter)];

        Assert.All(transforms, macro => Assert.True(
            macro.Range is ListLiteral or MapLiteral { Entries.Count: <= 1 } or Ident { Name: "li" or "ls" or "lx" }
                or Call { Function: ProfileTables.Dyn, Args: [ListLiteral or MapLiteral { Entries.Count: <= 1 }] },
            ExprPrinter.Print(macro)));
        Assert.Contains(transforms, macro => macro.Range is Call { Function: ProfileTables.Dyn });
        Assert.Contains(transforms, macro => macro.Range is MapLiteral { Entries.Count: 1 });
        Assert.Contains(AllNodes(), node => node is Comprehension { Range: MapLiteral { Entries.Count: > 1 } } macro && macro.Macro is not (ProfileTables.MapMacro or ProfileTables.Filter));
    }

    // A list of types is the range of a macro only inside dyn(): the iteration variable is then dyn, never of the static type type.
    [Fact]
    public void RangeOfTypes_StandsInDynAndNoIterationVariableHasTheTypeType()
    {
        // Lists of types are rare, so the sample is the larger one of the conversion test.
        List<Comprehension> macros = [.. Sample(ConversionCases).SelectMany(generated => Nodes(generated.Expression)).OfType<Comprehension>()];

        Assert.DoesNotContain(macros, macro => macro.Range is ListLiteral { Elements.Count: > 0 } list && list.Elements.All(IsTypeValued));
        Assert.Contains(macros, macro => macro.Range is Call { Function: ProfileTables.Dyn, Args: [ListLiteral { Elements.Count: > 0 } list] } && list.Elements.All(IsTypeValued));
        Assert.Contains(macros, macro => macro.Macro is ProfileTables.MapMacro or ProfileTables.Filter
            && macro.Range is Call { Function: ProfileTables.Dyn, Args: [ListLiteral { Elements.Count: > 0 } list] } && list.Elements.All(IsTypeValued));
    }

    // An empty literal has an element (a key) of a free type variable that the body of the macro may bind to the type type, as
    // in {}.filter(v, v == int), and cel-go then reads v as a type name: the empty literal is the range of a macro only inside
    // dyn(), and so is an iteration variable of an outer macro whose own element type is not known.
    [Fact]
    public void EmptyLiteralAndUntypedVariable_AreTheRangeOfAMacroOnlyInsideDyn()
    {
        List<Comprehension> macros = [.. Sample(ConversionCases).SelectMany(generated => Nodes(generated.Expression)).OfType<Comprehension>()];

        Assert.DoesNotContain(macros, macro => macro.Range is ListLiteral { Elements.Count: 0 } or MapLiteral { Entries.Count: 0 });
        Assert.Contains(macros, macro => macro.Range is Call { Function: ProfileTables.Dyn, Args: [ListLiteral { Elements.Count: 0 }] });
        Assert.Contains(macros, macro => macro.Range is Call { Function: ProfileTables.Dyn, Args: [MapLiteral { Entries.Count: 0 }] });
        Assert.Contains(macros, macro => macro.Macro is ProfileTables.MapMacro or ProfileTables.Filter
            && macro.Range is Call { Function: ProfileTables.Dyn, Args: [ListLiteral { Elements.Count: 0 }] });
        Assert.Contains(macros, macro => macro.Macro is ProfileTables.MapMacro or ProfileTables.Filter
            && macro.Range is Call { Function: ProfileTables.Dyn, Args: [MapLiteral { Entries.Count: 0 }] });

        // dyn(v) as the range of a nested macro is the rarest form: about one case in ten thousand, so its sample is larger.
        Assert.Contains(
            Sample(RareCases).SelectMany(generated => Nodes(generated.Expression)).OfType<Comprehension>(),
            macro => macro.Range is Call { Function: ProfileTables.Dyn, Args: [Ident variable] } && ProfileTables.IterationVariables.Contains(variable.Name));
    }

    // An iteration variable occurs only inside its macro: in the predicate or the transform, never in the range.
    [Fact]
    public void IterationVariables_OccurOnlyInsideTheirMacro()
    {
        foreach (GeneratedCase generated in Sample())
        {
            AssertScoped(generated.Expression, []);
        }

        Assert.Contains(AllNodes(), node => node is Comprehension { Variable: "w" });
    }

    // The children of a node for minimization are the operand of a selection and of a presence test, and the range of a macro
    // only: the other arguments of a macro read the iteration variable; the bodies come as instances (SignatureTests).
    [Fact]
    public void ChildrenOfAMacro_AreItsRangeOnly()
    {
        Expr range = new ListLiteral([new Ident("i1")], TrailingComma: false);
        Expr predicate = new Call(ProfileTables.Greater, [new Ident("v"), new IntLiteral(0, IntegerForm.DecimalDigits)]);
        Expr operand = new Ident("x1");

        Assert.Equal([range], Minimizer.Children(new Comprehension(ProfileTables.All, range, "v", [predicate])));
        Assert.Equal([range], Minimizer.Children(new Comprehension(ProfileTables.MapMacro, range, "v", [predicate, new Ident("v")])));
        Assert.Equal([operand], Minimizer.Children(new Select(operand, "a")));
        Assert.Equal([operand], Minimizer.Children(new PresenceTest(operand, "a")));
    }

    // cel-go turns a carriage return in the source of a literal into a line feed, while the library keeps it: the values of
    // literals hold carriage returns, their text only escapes them.
    [Fact]
    public void StringAndBytesLiterals_HoldCarriageReturnsButNeverPrintOneAsItself()
    {
        List<Expr> literals = [.. AllNodes().Where(node => node is StringLiteral or BytesLiteral)];

        Assert.Contains(literals, literal => literal is StringLiteral { Value: var text } && text.Contains('\r', StringComparison.Ordinal));
        Assert.Contains(literals, literal => literal is BytesLiteral { Value: var bytes } && bytes.Contains((byte)'\r'));
        Assert.All(literals, literal => Assert.DoesNotContain('\r', ExprPrinter.Print(literal)));
    }

    [Fact]
    public void CarriageReturn_CannotBeLiteralInAnyQuotes()
    {
        Quote[] quotes = Enum.GetValues<Quote>();

        Assert.Equal(4, quotes.Length);
        foreach (Quote quote in quotes)
        {
            Assert.False(TextUnits.CanBeLiteral('\r', quote, raw: false), quote.ToString());
            Assert.False(TextUnits.CanBeLiteral('\r', quote, raw: true), quote.ToString());
            Assert.DoesNotContain(Escape.Literal, TextUnits.StringForms('\r', quote, raw: false));
            Assert.DoesNotContain(Escape.Literal, TextUnits.BytesForms([(byte)'\r'], 0, quote, raw: false));
        }
    }

    [Fact]
    public void CaseN_DependsOnTheSeedAndNAlone()
    {
        GeneratedCase first = CaseGenerator.Case(20261104, 17);
        _ = CaseGenerator.Case(20261104, 3);
        _ = CaseGenerator.Case(7, 17);
        GeneratedCase again = CaseGenerator.Case(20261104, 17);

        Assert.Equal(first.Text, again.Text);
        Assert.Equal(first.Bindings, again.Bindings);
        Assert.True(Enumerable.Range(0, 20).Select(n => CaseGenerator.Case(20261104, (ulong)n).Text).Distinct().Count() > 10);
        Assert.NotEqual(
            Enumerable.Range(0, 20).Select(n => CaseGenerator.Case(20261104, (ulong)n).Text),
            Enumerable.Range(0, 20).Select(n => CaseGenerator.Case(20261105, (ulong)n).Text));
    }

    // ---- The printer ----

    [Fact]
    public void Literals_PrintInTheFormTheyCarry()
    {
        foreach (string line in LiteralForms.Split('\n'))
        {
            string[] fields = line.TrimEnd('\r').Split(" | ");
            (Expr literal, string text) = ParseRow(fields);
            if (text == "!")
            {
                Assert.Throws<ArgumentException>(() => ExprPrinter.Print(literal));
            }
            else
            {
                Assert.Equal(text, ExprPrinter.Print(literal));
            }
        }
    }

    [Fact]
    public void TripleQuotes_TakeALineBreakAsItself()
    {
        StringLiteral literal = new("a\nb", new TextForm(Quote.TripleQuotationMark, 'R', [Escape.Literal, Escape.Literal, Escape.Literal]));

        Assert.Equal("R\"\"\"a\nb\"\"\"", ExprPrinter.Print(literal));
    }

    [Fact]
    public void Precedence_AddsParenthesesWhereTheTreeNeedsThem()
    {
        Ident i1 = new("i1");
        Ident i2 = new("i2");
        IntLiteral one = new(1, IntegerForm.DecimalDigits);
        Call sum = new(ProfileTables.Add, [i1, i2]);
        Expr[] trees =
        [
            new Call(ProfileTables.Add, [i1, new Call(ProfileTables.Add, [i2, one])]),
            new Call(ProfileTables.Add, [sum, one]),
            new Call(ProfileTables.Divide, [sum, new Call(ProfileTables.Divide, [i1, one])]),
            new Call(ProfileTables.Add, [one, new Call(ProfileTables.Divide, [i2, i1])]),
            new Call(ProfileTables.Negate, [sum]),
            new Call(ProfileTables.Negate, [new Call(ProfileTables.Negate, [i1])]),
            new Call(ProfileTables.Negate, [new IntLiteral(-5, IntegerForm.DecimalDigits)]),
            new Call(ProfileTables.LogicalOr, [new Call(ProfileTables.LogicalOr, [new Ident("b1"), new Ident("b2")]), new Ident("x1")]),
            new Call(ProfileTables.LogicalOr, [new BoolLiteral(false), new Call(ProfileTables.LogicalOr, [new Ident("x2"), new BoolLiteral(true)])]),
            i1 with { Parenthesized = true },
            new Call(ProfileTables.Add, [sum with { Parenthesized = true }, new Ident("x1")]),
        ];

        Assert.Equal(
            "i1 + (i2 + 1)|i1 + i2 + 1|(i1 + i2) / (i1 / 1)|1 + i2 / i1|-(i1 + i2)|--i1|--5|b1 || b2 || x1|false || (x2 || true)|(i1)|(i1 + i2) + x1",
            string.Join('|', trees.Select(ExprPrinter.Print)));
    }

    [Fact]
    public void ListsAndMaps_PrintTheirElementsAndEntries()
    {
        StringLiteral text = new("a", new TextForm(Quote.Apostrophe, null, [Escape.Literal]));
        ListLiteral inner = new([new IntLiteral(1, IntegerForm.DecimalDigits)], TrailingComma: true);
        IntLiteral one = new(1, IntegerForm.DecimalDigits);
        IntLiteral two = new(2, IntegerForm.DecimalDigits);
        MapLiteral map = new([new MapEntry(new BoolLiteral(true), new NullLiteral()), new MapEntry(text, inner)], TrailingComma: false);
        ListLiteral outer = new([new ListLiteral([], TrailingComma: false), new MapLiteral([], TrailingComma: false), map], TrailingComma: false);
        Expr[] commas =
        [
            new ListLiteral([], TrailingComma: true),
            new MapLiteral([], TrailingComma: true),
            new MapLiteral([new MapEntry(one, two)], TrailingComma: true),
            new MapLiteral([new MapEntry(one, two), new MapEntry(two, one)], TrailingComma: true),
        ];

        Assert.Equal("[[], {}, {true: null, 'a': [1,]}]", ExprPrinter.Print(outer));
        Assert.Equal("[,]|{,}|{1: 2,}|{1: 2, 2: 1,}", string.Join('|', commas.Select(ExprPrinter.Print)));
    }

    [Fact]
    public void OperatorsOfTheProfile_PrintWithThePrecedenceOfCel()
    {
        Ident i1 = new("i1");
        Ident b1 = new("b1");
        Ident b2 = new("b2");
        Ident x1 = new("x1");
        IntLiteral one = new(1, IntegerForm.DecimalDigits);
        Call and = new(ProfileTables.LogicalAnd, [b1, b2]);
        Call or = new(ProfileTables.LogicalOr, [b1, b2]);
        Call less = new(ProfileTables.Less, [i1, one]);
        Call conditional = new(ProfileTables.Conditional, [b1, one, i1]);
        Call product = new(ProfileTables.Multiply, [i1, one]);
        Call difference = new(ProfileTables.Subtract, [i1, one]);
        Expr[] trees =
        [
            new Call(ProfileTables.LogicalOr, [and, b1]),
            new Call(ProfileTables.LogicalAnd, [or, b1]),
            new Call(ProfileTables.LogicalAnd, [b1, and]),
            new Call(ProfileTables.Equal, [less, new BoolLiteral(true)]),
            new Call(ProfileTables.NotEqual, [b1, new Call(ProfileTables.Equal, [b2, b1])]),
            new Call(ProfileTables.In, [one, new ListLiteral([i1], TrailingComma: false)]),
            new Call(ProfileTables.LogicalNot, [new Call(ProfileTables.In, [x1, x1])]),
            new Call(ProfileTables.LogicalNot, [new Call(ProfileTables.LogicalNot, [b1])]),
            new Call(ProfileTables.Modulo, [difference, product]),
            new Call(ProfileTables.Subtract, [i1, difference]),
            new Call(ProfileTables.Conditional, [conditional, conditional, conditional]),
            new Call(ProfileTables.Conditional, [or, and, less]),
            new Call(ProfileTables.Add, [conditional, one]),
        ];

        Assert.Equal(
            "b1 && b2 || b1|(b1 || b2) && b1|b1 && (b1 && b2)|i1 < 1 == true|b1 != (b2 == b1)|1 in [i1]|!(x1 in x1)|!!b1|(i1 - 1) % (i1 * 1)|i1 - (i1 - 1)"
                + "|(b1 ? 1 : i1) ? (b1 ? 1 : i1) : b1 ? 1 : i1|b1 || b2 ? b1 && b2 : i1 < 1|(b1 ? 1 : i1) + 1",
            string.Join('|', trees.Select(ExprPrinter.Print)));
    }

    [Fact]
    public void MembersCallsAndMacros_PrintTheirOperandAsAMember()
    {
        Ident i1 = new("i1");
        Ident s1 = new("s1");
        Ident x1 = new("x1");
        IntLiteral one = new(1, IntegerForm.DecimalDigits);
        StringLiteral a = new("a", new TextForm(Quote.Apostrophe, null, [Escape.Literal]));
        Call sum = new(ProfileTables.Add, [i1, one]);
        MapLiteral map = new([new MapEntry(a, one)], TrailingComma: false);
        Expr[] trees =
        [
            new Select(map, "a"),
            new Select(new Select(x1, "a"), "f"),
            new PresenceTest(map, "b"),
            new PresenceTest(new Call(ProfileTables.Dyn, [map]), "a"),
            new Select(new Call(ProfileTables.Conditional, [new Ident("b1"), map, x1]), "a"),
            new Call(ProfileTables.Index, [new ListLiteral([one], TrailingComma: false), new IntLiteral(0, IntegerForm.DecimalDigits)]),
            new Call(ProfileTables.Index, [new Call(ProfileTables.Add, [x1, x1]), sum]),
            new Call(ProfileTables.Index, [one, one]),
            new Select(new IntLiteral(-1, IntegerForm.DecimalDigits), "a"),
            new Call(ProfileTables.Size, [s1]) { Receiver = true },
            new Call(ProfileTables.Size, [new Call(ProfileTables.Add, [s1, s1])]) { Receiver = true },
            new Call(ProfileTables.Size, [s1]),
            new Call(ProfileTables.Contains, [s1, a]) { Receiver = true },
            new Call(ProfileTables.TypeOf, [sum]),
            new Call(ProfileTables.Dyn, [new Call(ProfileTables.Negate, [i1])]),
            new Call(ProfileTables.DoubleConversion, [a]),
            new Comprehension(ProfileTables.All, new ListLiteral([one], TrailingComma: false), "v", [new Call(ProfileTables.Greater, [new Ident("v"), one])]),
            new Comprehension(ProfileTables.MapMacro, x1, "v", [new BoolLiteral(true), new Ident("v")]),
            new Comprehension(ProfileTables.Filter, new Call(ProfileTables.Add, [x1, x1]), "w", [new BoolLiteral(false)]),
            new Comprehension(ProfileTables.ExistsOne, new Comprehension(ProfileTables.MapMacro, map, "v", [new Ident("v")]), "v", [new Ident("b1")]),
        ];

        Assert.Equal(
            "{'a': 1}.a|x1.a.f|has({'a': 1}.b)|has(dyn({'a': 1}).a)|(b1 ? {'a': 1} : x1).a|[1][0]|(x1 + x1)[i1 + 1]|(1)[1]|(-1).a|s1.size()"
                + "|(s1 + s1).size()|size(s1)|s1.contains('a')|type(i1 + 1)|dyn(-i1)|double('a')|[1].all(v, v > 1)|x1.map(v, true, v)"
                + "|(x1 + x1).filter(w, false)|{'a': 1}.map(v, v).exists_one(v, b1)",
            string.Join('|', trees.Select(ExprPrinter.Print)));
    }

    // ---- Helpers ----

    private static IEnumerable<GeneratedCase> Sample(int cases = Cases)
    {
        foreach (ulong seed in Seeds)
        {
            for (ulong n = 0; n < (ulong)cases; n++)
            {
                yield return CaseGenerator.Case(seed, n);
            }
        }
    }

    // The environment of the run manifest of the repository, as the library side of a run builds it.
    private static CelEnvironment Environment()
    {
        CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);
        Assert.Empty(CelEnvironmentJson.Apply(builder, File.ReadAllBytes(Path.Combine(RepositoryRoot.Find(), ProfileTables.ManifestPath))));
        CelBuildResult built = builder.Build();
        Assert.True(built.IsSuccess);
        return built.Environment!;
    }

    private static IEnumerable<Expr> AllNodes() => Sample().SelectMany(generated => Nodes(generated.Expression));

    // Every child of a node, the arguments of a macro included: a walk of its own, not Minimizer.Children.
    private static IEnumerable<Expr> Children(Expr expr) => expr switch
    {
        Comprehension macro => [macro.Range, .. macro.Arguments],
        Select select => [select.Operand],
        PresenceTest test => [test.Operand],
        Call call => call.Args,
        ListLiteral list => list.Elements,
        MapLiteral map => map.Entries.SelectMany(entry => new[] { entry.Key, entry.Value }),
        _ => [],
    };

    private static IEnumerable<Expr> Nodes(Expr expr) => Children(expr).SelectMany(Nodes).Prepend(expr);

    private static int Depth(Expr expr) => 1 + Children(expr).Select(Depth).DefaultIfEmpty(0).Max();

    // The largest number of macros on a path from the node down.
    private static int MacroNesting(Expr expr) => (expr is Comprehension ? 1 : 0) + Children(expr).Select(MacroNesting).DefaultIfEmpty(0).Max();

    // The number of operands of a chain of one logical operator whose root is the node, nested to the left.
    private static int ChainLength(Expr expr, string function) =>
        expr is Call call && call.Function == function ? ChainLength(call.Args[0], function) + 1 : 1;

    private static int Elements(Expr expr) => expr switch
    {
        ListLiteral list => list.Elements.Count,
        MapLiteral map => map.Entries.Count,
        _ => 0,
    };

    // Each iteration variable is read only inside a macro that declares it, and a macro inside another has another variable.
    private static void AssertScoped(Expr expr, List<string> scope)
    {
        switch (expr)
        {
            case Ident ident when ProfileTables.IterationVariables.Contains(ident.Name):
                Assert.Contains(ident.Name, scope);
                break;
            case Comprehension macro:
                Assert.DoesNotContain(macro.Variable, scope);
                AssertScoped(macro.Range, scope);
                foreach (Expr argument in macro.Arguments)
                {
                    AssertScoped(argument, [.. scope, macro.Variable]);
                }

                break;
            default:
                foreach (Expr child in Children(expr))
                {
                    AssertScoped(child, scope);
                }

                break;
        }
    }

    // A node whose static type is type by its form: a type identifier, a call of type(), or a conditional of such nodes.
    private static bool IsTypeValued(Expr expr) => expr switch
    {
        Ident ident => ProfileTables.TypeIdentifiers.Contains(ident.Name),
        Call { Function: ProfileTables.TypeOf } => true,
        Call { Function: ProfileTables.Conditional, Args: [_, var first, var second] } => IsTypeValued(first) && IsTypeValued(second),
        _ => false,
    };

    // Whether a node is a variable of the manifest with one of the names.
    private static bool IsVariable(Expr expr, params string[] names) => expr is Ident ident && names.Contains(ident.Name);

    // A node whose static type is dyn by its form: a dyn variable or a call of dyn().
    private static bool IsDyn(Expr expr) => expr is Ident { Name: "x1" or "x2" } or Call { Function: ProfileTables.Dyn };

    // The type of a literal or of a variable of the manifest, or null for other nodes.
    private static ProfileType? AtomKind(Expr expr) => expr switch
    {
        IntLiteral => ProfileType.IntType,
        UintLiteral => ProfileType.UintType,
        DoubleLiteral => ProfileType.DoubleType,
        BoolLiteral => ProfileType.BoolType,
        StringLiteral => ProfileType.StringType,
        BytesLiteral => ProfileType.BytesType,
        Ident ident => ProfileTables.Variables.FirstOrDefault(variable => variable.Name == ident.Name) is { Type: not ProfileType.DynType } variable ? variable.Type : null,
        _ => null,
    };

    // A map key as a .NET value with the equality of CEL for the key types: numbers by value across int and uint.
    private static object CelKey(Expr key) => key switch
    {
        IntLiteral i => new BigInteger(i.Value),
        UintLiteral u => new BigInteger(u.Value),
        BoolLiteral b => b.Value,
        StringLiteral s => s.Value,
        _ => throw new InvalidOperationException("a map key of another kind: " + ExprPrinter.Print(key)),
    };

    private static object CelKey(Value key) => key.KindCase switch
    {
        Value.KindOneofCase.Int64Value => new BigInteger(key.Int64Value),
        Value.KindOneofCase.Uint64Value => new BigInteger(key.Uint64Value),
        Value.KindOneofCase.BoolValue => key.BoolValue,
        Value.KindOneofCase.StringValue => key.StringValue,
        _ => throw new InvalidOperationException("a map key of another kind: " + key),
    };

    // A value and the values in it: the elements of a list, the keys and values of a map.
    private static IEnumerable<Value> Values(Value value) => value.KindCase switch
    {
        Value.KindOneofCase.ListValue => value.ListValue.Values.SelectMany(Values).Prepend(value),
        Value.KindOneofCase.MapValue => value.MapValue.Entries.SelectMany(entry => Values(entry.Key).Concat(Values(entry.Value))).Prepend(value),
        _ => [value],
    };

    // The number of code points of a string or of bytes of a bytes value, and the limit of its kind; other nodes have 0 of 0.
    private static int TextLength(Expr node) => node switch
    {
        StringLiteral s => s.Value.EnumerateRunes().Count(),
        BytesLiteral b => b.Value.Count,
        _ => 0,
    };

    private static int LimitOf(Expr node) => node is BytesLiteral ? ProfileTables.MaxBytes : ProfileTables.MaxCodePoints;

    private static int TextLength(Value value) => value.KindCase switch
    {
        Value.KindOneofCase.StringValue => value.StringValue.EnumerateRunes().Count(),
        Value.KindOneofCase.BytesValue => value.BytesValue.Length,
        _ => 0,
    };

    private static int LimitOf(Value value) => value.KindCase == Value.KindOneofCase.BytesValue ? ProfileTables.MaxBytes : ProfileTables.MaxCodePoints;

    private static ProfileType KindOf(Value value) => value.KindCase switch
    {
        Value.KindOneofCase.Int64Value => ProfileType.IntType,
        Value.KindOneofCase.Uint64Value => ProfileType.UintType,
        Value.KindOneofCase.DoubleValue => ProfileType.DoubleType,
        Value.KindOneofCase.BoolValue => ProfileType.BoolType,
        Value.KindOneofCase.StringValue => ProfileType.StringType,
        Value.KindOneofCase.BytesValue => ProfileType.BytesType,
        Value.KindOneofCase.NullValue => ProfileType.NullType,
        Value.KindOneofCase.ListValue => ProfileType.ListType,
        Value.KindOneofCase.MapValue => ProfileType.MapType,
        _ => throw new InvalidOperationException("a binding of another kind: " + value),
    };

    private static (Expr Literal, string Text) ParseRow(string[] fields) => fields[0].Trim() switch
    {
        "int" => (new IntLiteral(long.Parse(fields[1], CultureInfo.InvariantCulture), Enum.Parse<IntegerForm>(fields[2])), fields[3]),
        "uint" => (new UintLiteral(ulong.Parse(fields[1], CultureInfo.InvariantCulture), Enum.Parse<IntegerForm>(fields[2]), fields[3][0]), fields[4]),
        "double" => (new DoubleLiteral(double.Parse(fields[1], CultureInfo.InvariantCulture), Enum.Parse<DoubleForm>(fields[2])), fields[3]),
        "string" => (new StringLiteral(JsonSerializer.Deserialize<string>(fields[1])!, Form(fields[2], fields[3], fields[4])), fields[5]),
        "bytes" => (new BytesLiteral(Convert.FromHexString(fields[1].Replace(" ", string.Empty, StringComparison.Ordinal)), fields[2][0], Form(fields[3], fields[4], fields[5])), fields[6]),
        _ => throw new InvalidOperationException("a row of another kind: " + string.Join(" | ", fields)),
    };

    private static TextForm Form(string quote, string raw, string units) => new(
        Enum.Parse<Quote>(quote),
        raw == "-" ? null : raw[0],
        [.. units.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<Escape>)]);
}
