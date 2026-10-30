using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Cel.Expr;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Difftest;

// The case generator and the printer of eng/difftest (docs/design/eng-difftest.md, "Profile of the walking skeleton"). The
// generator tests read the cases 0 to Cases - 1 of fixed seeds; the printer tests print fixed trees.
public sealed class GeneratorTests
{
    private const int Cases = 400;

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
        """";

    // ---- The generator ----

    [Fact]
    public void Negate_NeverHasADynOperand()
    {
        List<Call> calls = [.. AllNodes().OfType<Call>().Where(call => call.Function == ProfileTables.Negate)];

        Assert.NotEmpty(calls);
        Assert.All(calls, call => Assert.False(IsDyn(call.Args[0]), ExprPrinter.Print(call)));
    }

    [Fact]
    public void DynOperandOfAddAndDivide_StandsOnlyNextToAStaticInt()
    {
        List<Call> calls = [.. AllNodes().OfType<Call>().Where(call => call.Function is ProfileTables.Add or ProfileTables.Divide)];
        List<Call> withDyn = [.. calls.Where(call => call.Args.Any(IsDyn))];

        Assert.Contains(withDyn, call => call.Function == ProfileTables.Add);
        Assert.Contains(withDyn, call => call.Function == ProfileTables.Divide);
        Assert.Contains(withDyn, call => IsDyn(call.Args[0]));
        Assert.Contains(withDyn, call => IsDyn(call.Args[1]));
        Assert.All(withDyn, call => Assert.Single(call.Args, IsStaticInt));
        Assert.All(calls, call => Assert.All(call.Args, arg => Assert.True(IsStaticInt(arg) || IsDyn(arg), ExprPrinter.Print(call))));
    }

    [Fact]
    public void LogicalOr_TakesBoolOperands()
    {
        List<Call> calls = [.. AllNodes().OfType<Call>().Where(call => call.Function == ProfileTables.LogicalOr)];

        Assert.NotEmpty(calls);
        Assert.All(calls, call => Assert.All(call.Args, arg => Assert.True(
            arg is BoolLiteral or Ident { Name: "b1" or "b2" or "x1" or "x2" } or Call { Function: ProfileTables.LogicalOr },
            ExprPrinter.Print(call))));
    }

    [Fact]
    public void MapKeys_ArePairwiseUnequalByCelEqualityAndOfTheKeyTypes()
    {
        List<MapLiteral> maps = [.. AllNodes().OfType<MapLiteral>()];

        Assert.Contains(maps, map => map.Entries.Count == ProfileTables.MaxElements);
        Assert.Contains(maps, map => map.Entries.Any(entry => entry.Key is IntLiteral) && map.Entries.Any(entry => entry.Key is UintLiteral));
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
        Assert.Equal(ProfileTables.MaxDepth, cases.Max(generated => Depth(generated.Expression)));
        Assert.All(cases.SelectMany(generated => Nodes(generated.Expression)), node => Assert.InRange(TextLength(node), 0, LimitOf(node)));
        Assert.All(cases.SelectMany(generated => generated.Bindings.Values), value => Assert.InRange(TextLength(value), 0, LimitOf(value)));
        Assert.Contains(cases, generated => generated.Bindings.Values.Any(value => value.KindCase == Value.KindOneofCase.StringValue && TextLength(value) == ProfileTables.MaxCodePoints));
    }

    [Fact]
    public void Bindings_HaveTheTypesOfTheVariablesAndSomeStayUnbound()
    {
        List<GeneratedCase> cases = [.. Sample()];
        Dictionary<string, ProfileType> types = ProfileTables.Variables.ToDictionary(variable => variable.Name, variable => variable.Type);

        Assert.All(cases.SelectMany(generated => generated.Bindings), binding => Assert.True(
            types[binding.Key] == ProfileType.DynType || KindOf(binding.Value) == types[binding.Key],
            binding.Key + " = " + binding.Value));
        Assert.Contains(cases, generated => generated.Bindings.Count < ProfileTables.Variables.Count);
        Assert.Equal(
            ProfileTables.DynKinds.Order(),
            cases.SelectMany(generated => generated.Bindings.Where(binding => binding.Key == "x1")).Select(binding => KindOf(binding.Value)).Distinct().Order());
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
        Assert.Contains(bound, double.IsNaN);
        Assert.Contains(bound, double.IsPositiveInfinity);
        Assert.Contains(bound, double.IsNegativeInfinity);
        Assert.Contains(bound, value => value == 0 && double.IsNegative(value));
        Assert.Contains(doubles, binding => binding.Key is "x1" or "x2" && !double.IsFinite(binding.Value.DoubleValue));
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

    // ---- Helpers ----

    private static IEnumerable<GeneratedCase> Sample()
    {
        foreach (ulong seed in Seeds)
        {
            for (ulong n = 0; n < Cases; n++)
            {
                yield return CaseGenerator.Case(seed, n);
            }
        }
    }

    private static IEnumerable<Expr> AllNodes() => Sample().SelectMany(generated => Nodes(generated.Expression));

    private static IEnumerable<Expr> Children(Expr expr) => expr switch
    {
        Call call => call.Args,
        ListLiteral list => list.Elements,
        MapLiteral map => map.Entries.SelectMany(entry => new[] { entry.Key, entry.Value }),
        _ => [],
    };

    private static IEnumerable<Expr> Nodes(Expr expr) => Children(expr).SelectMany(Nodes).Prepend(expr);

    private static int Depth(Expr expr) => 1 + Children(expr).Select(Depth).DefaultIfEmpty(0).Max();

    private static bool IsDyn(Expr expr) => expr is Ident { Name: "x1" or "x2" };

    private static bool IsStaticInt(Expr expr) =>
        expr is IntLiteral or Ident { Name: "i1" or "i2" } or Call { Function: ProfileTables.Add or ProfileTables.Divide or ProfileTables.Negate };

    // A map key as a .NET value with the equality of CEL for the key types: numbers by value across int and uint.
    private static object CelKey(Expr key) => key switch
    {
        IntLiteral i => new BigInteger(i.Value),
        UintLiteral u => new BigInteger(u.Value),
        BoolLiteral b => b.Value,
        StringLiteral s => s.Value,
        _ => throw new InvalidOperationException("a map key of another kind: " + ExprPrinter.Print(key)),
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
